namespace UnivueGuardian.Models;

public class DatabaseConnection
{
    public string   Name             { get; set; } = "";
    public DbType2  DbType           { get; set; } = DbType2.SqlServer;
    public string   Host             { get; set; } = "localhost";
    public int      Port             { get; set; } = 1433;
    public string   ConnectionString { get; set; } = "";
}

public class DatabaseMetrics
{
    public string   Name              { get; set; } = "";
    public DbType2  DbType            { get; set; }
    public string   Host              { get; set; } = "";
    public bool     IsOnline          { get; set; }
    public long     ResponseMs        { get; set; }
    public string   LastError         { get; set; } = "";
    public int      ActiveConnections  { get; set; }
    public int      MaxConnections     { get; set; }
    public int      BlockedQueries     { get; set; }
    public double   DatabaseSizeMb     { get; set; }
    public double   CacheHitRatio      { get; set; }
    public List<SlowQuery> SlowQueries { get; set; } = new();

    public double ConnectionUsagePercent =>
        MaxConnections > 0 ? ActiveConnections * 100.0 / MaxConnections : 0;
}

public class SlowQuery
{
    public long   DurationMs     { get; set; }
    public long   ExecutionCount { get; set; }
    public string QueryText      { get; set; } = "";
}

public enum DbType2 { SqlServer, MySql, PostgreSql }
