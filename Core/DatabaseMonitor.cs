using System.Data;
using Microsoft.Data.SqlClient;
using System.Runtime.Versioning;
using UnivueGuardian.Models;
using MySqlConnector;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class DatabaseMonitor
{
    public event Action<string, AlertSeverity, string>? AlertRaised;

    public async Task<List<DatabaseMetrics>> CollectAllAsync(List<DatabaseConnection> connections)
    {
        var results = new List<DatabaseMetrics>();
        var tasks = connections.Select(c => CollectAsync(c));
        var all = await Task.WhenAll(tasks);
        return all.ToList();
    }

    private async Task<DatabaseMetrics> CollectAsync(DatabaseConnection conn)
    {
        var m = new DatabaseMetrics
        {
            Name     = conn.Name,
            DbType   = conn.DbType,
            Host     = conn.Host
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            switch (conn.DbType)
            {
                case DbType2.SqlServer:
                    await CollectSqlServerAsync(conn, m);
                    break;
                case DbType2.MySql:
                    await CollectMySqlAsync(conn, m);
                    break;
                case DbType2.PostgreSql:
                    await CollectPostgreSqlAsync(conn, m);
                    break;
            }
            m.IsOnline      = true;
            m.ResponseMs    = sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            m.IsOnline   = false;
            m.LastError  = ex.Message;
            AlertRaised?.Invoke(conn.Name, AlertSeverity.Critical,
                $"Database '{conn.Name}' is OFFLINE: {ex.Message}");
        }

        // Alert on slow queries
        foreach (var q in m.SlowQueries)
            AlertRaised?.Invoke(conn.Name, AlertSeverity.Warning,
                $"Slow query detected ({q.DurationMs}ms): {q.QueryText[..Math.Min(80, q.QueryText.Length)]}");

        return m;
    }

    // ── SQL Server ────────────────────────────────────────
    private static async Task CollectSqlServerAsync(DatabaseConnection conn, DatabaseMetrics m)
    {
        using var sqlConn = new SqlConnection(conn.ConnectionString);
        await sqlConn.OpenAsync();

        // Active connections
        m.ActiveConnections = await ScalarAsync<int>(sqlConn,
            "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1");

        // Database size
        m.DatabaseSizeMb = await ScalarAsync<double>(sqlConn,
            "SELECT SUM(size * 8.0 / 1024) FROM sys.database_files");

        // Slow queries (> 1 sec)
        m.SlowQueries = await GetSqlServerSlowQueriesAsync(sqlConn);

        // Connection pool usage
        m.MaxConnections = await ScalarAsync<int>(sqlConn,
            "SELECT value_in_use FROM sys.configurations WHERE name = 'max connections'");

        // Blocked queries
        m.BlockedQueries = await ScalarAsync<int>(sqlConn,
            "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id > 0");

        // Cache hit ratio
        m.CacheHitRatio = await ScalarAsync<double>(sqlConn, @"
            SELECT CAST(cntr_value AS FLOAT) /
                   NULLIF((SELECT cntr_value FROM sys.dm_os_performance_counters
                           WHERE counter_name = 'Buffer cache hit ratio base'
                           AND object_name LIKE '%Buffer Manager%'), 0) * 100
            FROM sys.dm_os_performance_counters
            WHERE counter_name = 'Buffer cache hit ratio'
            AND object_name LIKE '%Buffer Manager%'");
    }

    private static async Task<List<SlowQuery>> GetSqlServerSlowQueriesAsync(SqlConnection conn)
    {
        var list = new List<SlowQuery>();
        try
        {
            const string sql = @"
                SELECT TOP 10
                    qs.total_elapsed_time / qs.execution_count / 1000 AS avg_ms,
                    qs.execution_count,
                    SUBSTRING(qt.text, (qs.statement_start_offset/2)+1,
                        ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(qt.text)
                          ELSE qs.statement_end_offset END - qs.statement_start_offset)/2)+1) AS query_text
                FROM sys.dm_exec_query_stats qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
                WHERE qs.total_elapsed_time / qs.execution_count > 1000000
                ORDER BY avg_ms DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SlowQuery
                {
                    DurationMs     = reader.GetInt64(0),
                    ExecutionCount = reader.GetInt64(1),
                    QueryText      = reader.GetString(2)
                });
            }
        }
        catch { }
        return list;
    }

    // ── MySQL (via MySqlConnector if available, else skip) ─
    private static async Task CollectMySqlAsync(DatabaseConnection conn, DatabaseMetrics m)
    {
        using var mysqlConn = new MySqlConnection(conn.ConnectionString);
        await mysqlConn.OpenAsync();

        // Active connections
        m.ActiveConnections = await MySqlScalarAsync<int>(mysqlConn,
            "SELECT COUNT(*) FROM information_schema.PROCESSLIST");

        // Max connections
        m.MaxConnections = await MySqlScalarAsync<int>(mysqlConn,
            "SELECT @@max_connections");

        // Database size MB
        m.DatabaseSizeMb = await MySqlScalarAsync<double>(mysqlConn,
            @"SELECT ROUND(SUM(data_length + index_length) / 1024 / 1024, 1)
          FROM information_schema.TABLES");

        // Slow queries
        m.SlowQueries = await GetMySqlSlowQueriesAsync(mysqlConn);

        // Blocked queries
        m.BlockedQueries = await MySqlScalarAsync<int>(mysqlConn,
            "SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_state = 'LOCK WAIT'");
    }

    private static async Task<List<SlowQuery>> GetMySqlSlowQueriesAsync(MySqlConnection conn)
    {
        var list = new List<SlowQuery>();
        try
        {
            const string sql = @"
            SELECT 
                ROUND(timer_wait / 1000000000, 0) AS avg_ms,
                count_star AS execution_count,
                SUBSTRING(digest_text, 1, 200) AS query_text
            FROM performance_schema.events_statements_summary_by_digest
            WHERE timer_wait > 1000000000
            ORDER BY timer_wait DESC
            LIMIT 10";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new SlowQuery
                {
                    DurationMs = reader.GetInt64(0),
                    ExecutionCount = reader.GetInt64(1),
                    QueryText = reader.GetString(2)
                });
            }
        }
        catch { }
        return list;
    }

    private static async Task<T> MySqlScalarAsync<T>(MySqlConnection conn, string sql)
    {
        using var cmd = new MySqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value) return default!;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    // ── PostgreSQL (via Npgsql if available, else skip) ───
    private static async Task CollectPostgreSqlAsync(DatabaseConnection conn, DatabaseMetrics m)
    {
        await Task.Delay(10);
        m.LastError = "Install Npgsql NuGet for full PostgreSQL monitoring";
        m.IsOnline  = await TcpPingAsync(conn.Host, conn.Port);
    }

    private static async Task<bool> TcpPingAsync(string host, int port)
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            await tcp.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(3));
            return true;
        }
        catch { return false; }
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection conn, string sql)
    {
        using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value) return default!;
        return (T)Convert.ChangeType(result, typeof(T));
    }
}
