using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class IisMonitor : IDisposable
{
    // ── PerformanceCounters ───────────────────────────────
    private PerformanceCounter? _totalRequestsCounter;
    private PerformanceCounter? _activeConnectionsCounter;
    private PerformanceCounter? _bytesReceivedCounter;
    private PerformanceCounter? _bytesSentCounter;
    private PerformanceCounter? _error4xxCounter;
    private PerformanceCounter? _error5xxCounter;

    private double _lastRequestsPerSec = 0;
    private readonly Queue<double> _requestHistory = new(60);

    public event Action<string, AlertSeverity, string>? AlertRaised;

    public IisMonitor()
    {
        InitCounters();
    }

    private void InitCounters()
    {
        try { _totalRequestsCounter    = new PerformanceCounter("Web Service", "Total Method Requests/sec", "_Total"); _totalRequestsCounter.NextValue(); } catch { }
        try { _activeConnectionsCounter = new PerformanceCounter("Web Service", "Current Connections", "_Total"); } catch { }
        try { _bytesReceivedCounter     = new PerformanceCounter("Web Service", "Bytes Received/sec", "_Total"); _bytesReceivedCounter.NextValue(); } catch { }
        try { _bytesSentCounter         = new PerformanceCounter("Web Service", "Bytes Sent/sec", "_Total"); _bytesSentCounter.NextValue(); } catch { }
        try { _error4xxCounter          = new PerformanceCounter("Web Service", "Total Not Found Errors", "_Total"); } catch { }
        try { _error5xxCounter          = new PerformanceCounter("Web Service", "Total Runtime Errors", "_Total"); } catch { }
    }

    // ─────────────────────────────────────────────────────
    //  Main Collect
    // ─────────────────────────────────────────────────────
    public IisMetrics Collect()
    {
        var m = new IisMetrics();

        try { m.RequestsPerSec      = Math.Round(_totalRequestsCounter?.NextValue() ?? 0, 1); } catch { }
        try { m.ActiveConnections   = (long)(_activeConnectionsCounter?.NextValue() ?? 0); } catch { }
        try { m.BytesReceivedPerSec = Math.Round((_bytesReceivedCounter?.NextValue() ?? 0) / 1024, 1); } catch { }
        try { m.BytesSentPerSec     = Math.Round((_bytesSentCounter?.NextValue() ?? 0) / 1024, 1); } catch { }
        try { m.Error4xxCount       = (long)(_error4xxCounter?.NextValue() ?? 0); } catch { }
        try { m.Error5xxCount       = (long)(_error5xxCounter?.NextValue() ?? 0); } catch { }

        // App Pools
        m.AppPools = GetAppPools();

        // Sites
        m.Sites = GetSites();

        // History
        if (_requestHistory.Count >= 60) _requestHistory.Dequeue();
        _requestHistory.Enqueue(m.RequestsPerSec);
        m.RequestHistory = _requestHistory.ToArray();

        // Alerts
        CheckAlerts(m);

        _lastRequestsPerSec = m.RequestsPerSec;
        return m;
    }

    // ─────────────────────────────────────────────────────
    //  App Pools via WMI
    // ─────────────────────────────────────────────────────
    private static List<AppPoolInfo> GetAppPools()
    {
        var list = new List<AppPoolInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WebAdministration",
                "SELECT Name, ManagedRuntimeVersion, Enable32BitAppOnWin64 FROM ApplicationPool");

            foreach (ManagementObject obj in searcher.Get())
            {
                var pool = new AppPoolInfo
                {
                    Name           = obj["Name"]?.ToString() ?? "",
                    RuntimeVersion = obj["ManagedRuntimeVersion"]?.ToString() ?? "v4.0",
                    State          = GetAppPoolState(obj["Name"]?.ToString() ?? "")
                };
                pool.WorkerProcesses = GetWorkerProcesses(pool.Name);
                list.Add(pool);
            }
        }
        catch
        {
            // IIS not installed or WMI namespace not available
            list.Add(new AppPoolInfo { Name = "IIS not detected", State = AppPoolState.Unknown });
        }
        return list;
    }

    private static AppPoolState GetAppPoolState(string poolName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WebAdministration",
                $"SELECT State FROM ApplicationPool WHERE Name='{poolName}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                int state = Convert.ToInt32(obj["State"]);
                return state switch { 1 => AppPoolState.Running, 2 => AppPoolState.Stopped, _ => AppPoolState.Unknown };
            }
        }
        catch { }
        return AppPoolState.Unknown;
    }

    private static List<WorkerProcessInfo> GetWorkerProcesses(string poolName)
    {
        var list = new List<WorkerProcessInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WebAdministration",
                $"SELECT ProcessId, AppPoolName FROM WorkerProcess WHERE AppPoolName='{poolName}'");

            foreach (ManagementObject obj in searcher.Get())
            {
                int pid = Convert.ToInt32(obj["ProcessId"]);
                try
                {
                    var proc = Process.GetProcessById(pid);
                    list.Add(new WorkerProcessInfo
                    {
                        Pid       = pid,
                        MemoryMb  = proc.WorkingSet64 / (1024 * 1024),
                        AppPool   = poolName
                    });
                }
                catch { }
            }
        }
        catch { }
        return list;
    }

    // ─────────────────────────────────────────────────────
    //  Sites via WMI
    // ─────────────────────────────────────────────────────
    private static List<IisSiteInfo> GetSites()
    {
        var list = new List<IisSiteInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WebAdministration",
                "SELECT Name, ServerAutoStart, Id FROM Site");

            foreach (ManagementObject obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "";
                var site = new IisSiteInfo
                {
                    Name      = name,
                    SiteId    = Convert.ToInt32(obj["Id"]),
                    State     = GetSiteState(name),
                    Bindings  = GetSiteBindings(name)
                };

                // Per-site counters
                try
                {
                    using var reqCounter = new PerformanceCounter("Web Service", "Total Method Requests/sec", name);
                    site.RequestsPerSec = Math.Round(reqCounter.NextValue(), 1);
                }
                catch { }

                list.Add(site);
            }
        }
        catch
        {
            list.Add(new IisSiteInfo { Name = "IIS not detected", State = SiteState.Unknown });
        }
        return list;
    }

    private static SiteState GetSiteState(string siteName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WebAdministration",
                $"SELECT State FROM Site WHERE Name='{siteName}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                int s = Convert.ToInt32(obj["State"]);
                return s switch { 1 => SiteState.Running, 2 => SiteState.Stopped, _ => SiteState.Unknown };
            }
        }
        catch { }
        return SiteState.Unknown;
    }

    private static List<string> GetSiteBindings(string siteName)
    {
        var bindings = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WebAdministration",
                $"SELECT BindingInformation, Protocol FROM Site WHERE Name='{siteName}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var info = obj["BindingInformation"]?.ToString() ?? "";
                var proto = obj["Protocol"]?.ToString() ?? "http";
                bindings.Add($"{proto}://{info}");
            }
        }
        catch { }
        return bindings;
    }

    private void CheckAlerts(IisMetrics m)
    {
        // App pool stopped
        foreach (var pool in m.AppPools.Where(p => p.State == AppPoolState.Stopped && p.Name != "IIS not detected"))
            AlertRaised?.Invoke(pool.Name, AlertSeverity.Critical, $"App Pool '{pool.Name}' is STOPPED.");

        // High error rate
        if (m.Error5xxCount > 50)
            AlertRaised?.Invoke("IIS", AlertSeverity.Warning, $"High 5xx error count: {m.Error5xxCount}");
    }

    public void Dispose()
    {
        _totalRequestsCounter?.Dispose();
        _activeConnectionsCounter?.Dispose();
        _bytesReceivedCounter?.Dispose();
        _bytesSentCounter?.Dispose();
        _error4xxCounter?.Dispose();
        _error5xxCounter?.Dispose();
    }
}
