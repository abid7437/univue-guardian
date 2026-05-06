using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class NetworkMonitor : IDisposable
{
    private PerformanceCounter? _netInCounter;
    private PerformanceCounter? _netOutCounter;
    private readonly Queue<double> _inHistory  = new(60);
    private readonly Queue<double> _outHistory = new(60);

    public event Action<string, AlertSeverity, string>? AlertRaised;

    public NetworkMonitor()
    {
        try
        {
            var adapter = FindActiveAdapter();
            if (adapter != null)
            {
                _netInCounter  = new PerformanceCounter("Network Interface", "Bytes Received/sec", adapter);
                _netOutCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", adapter);
                _netInCounter.NextValue();
                _netOutCounter.NextValue();
            }
        }
        catch { }
    }

    public NetworkMetrics Collect(List<MonitoredPort> monitoredPorts)
    {
        var m = new NetworkMetrics();

        // Bandwidth
        try
        {
            m.InboundMbps  = Math.Round((_netInCounter?.NextValue()  ?? 0) * 8 / 1_000_000, 2);
            m.OutboundMbps = Math.Round((_netOutCounter?.NextValue() ?? 0) * 8 / 1_000_000, 2);
        }
        catch { }

        // History
        Enqueue(_inHistory,  m.InboundMbps);
        Enqueue(_outHistory, m.OutboundMbps);
        m.InboundHistory  = _inHistory.ToArray();
        m.OutboundHistory = _outHistory.ToArray();

        // Active connections count
        try
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();
            m.TcpConnectionCount = props.GetActiveTcpConnections().Length;
            m.UdpListenerCount   = props.GetActiveUdpListeners().Length;
        }
        catch { }

        // Port checks
        m.PortResults = CheckPorts(monitoredPorts);

        // Top bandwidth processes (via netstat approximation)
        m.TopConnections = GetTopConnections();

        // Alerts
        foreach (var pr in m.PortResults.Where(p => !p.IsOpen))
            AlertRaised?.Invoke($"Port {pr.Port}", AlertSeverity.Critical, $"Port {pr.Port} ({pr.Label}) on {pr.Host} is CLOSED/unreachable.");

        return m;
    }

    private static List<PortCheckResult> CheckPorts(List<MonitoredPort> ports)
    {
        var results = new List<PortCheckResult>();
        foreach (var p in ports)
        {
            var result = new PortCheckResult
            {
                Host      = p.Host,
                Port      = p.Port,
                Label     = p.Label,
                Protocol  = p.Protocol
            };

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                if (p.Protocol == "UDP")
                {
                    using var udp = new UdpClient();
                    udp.Connect(p.Host, p.Port);
                    result.IsOpen      = true;
                    result.ResponseMs  = sw.ElapsedMilliseconds;
                }
                else
                {
                    using var tcp = new TcpClient();
                    var task = tcp.ConnectAsync(p.Host, p.Port);
                    result.IsOpen     = task.Wait(3000);
                    result.ResponseMs = sw.ElapsedMilliseconds;
                }
            }
            catch { result.IsOpen = false; }

            results.Add(result);
        }
        return results;
    }

    private static List<ConnectionInfo> GetTopConnections()
    {
        var list = new List<ConnectionInfo>();
        try
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();
            var conns = props.GetActiveTcpConnections();

            list = conns
                .GroupBy(c => c.RemoteEndPoint.Address.ToString())
                .Select(g => new ConnectionInfo
                {
                    RemoteAddress = g.Key,
                    Count         = g.Count(),
                    State         = g.First().State.ToString()
                })
                .OrderByDescending(c => c.Count)
                .Take(10)
                .ToList();
        }
        catch { }
        return list;
    }

    private static string? FindActiveAdapter()
    {
        try
        {
            var cat = new PerformanceCounterCategory("Network Interface");
            return cat.GetInstanceNames()
                .FirstOrDefault(n => !n.Contains("Loopback") && !n.Contains("isatap"));
        }
        catch { return null; }
    }

    private static void Enqueue(Queue<double> q, double val)
    {
        if (q.Count >= 60) q.Dequeue();
        q.Enqueue(val);
    }

    public void Dispose()
    {
        _netInCounter?.Dispose();
        _netOutCounter?.Dispose();
    }
}
