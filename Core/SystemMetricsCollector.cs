using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class SystemMetricsCollector : IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _netInCounter;
    private readonly PerformanceCounter _netOutCounter;
    private readonly PerformanceCounter _diskReadCounter;
    private readonly PerformanceCounter _diskWriteCounter;

    // Rolling buffer for charts (last 6 minutes at 10-sec intervals = 36 points)
    private readonly Queue<double> _cpuHistory = new(36);
    private readonly Queue<double> _ramHistory = new(36);

    private long _prevNetIn = 0;
    private long _prevNetOut = 0;
    private DateTime _bootTime;

    public SystemMetricsCollector()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        // Find first active network adapter
        var netAdapter = FindActiveNetworkAdapter();
        if (netAdapter != null)
        {
            _netInCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", netAdapter);
            _netOutCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", netAdapter);
        }
        else
        {
            _netInCounter = new PerformanceCounter();
            _netOutCounter = new PerformanceCounter();
        }

        _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

        // Warm up CPU counter (first read is always 0)
        _cpuCounter.NextValue();

        _bootTime = GetBootTime();
    }

    public ServerMetrics Collect()
    {
        var metrics = new ServerMetrics();

        // CPU
        metrics.CpuPercent = Math.Round(_cpuCounter.NextValue(), 1);

        // RAM via WMI (most accurate)
        (metrics.RamUsedGb, metrics.RamTotalGb) = GetRamInfo();

        // Disks
        metrics.Disks = GetDiskInfo();

        // Network (bytes/sec → Mbps)
        var netIn = _netInCounter.NextValue();
        var netOut = _netOutCounter.NextValue();
        metrics.NetworkInMbps = Math.Round(netIn * 8 / 1_000_000.0, 1);
        metrics.NetworkOutMbps = Math.Round(netOut * 8 / 1_000_000.0, 1);

        // Disk I/O
        var diskRead = _diskReadCounter.NextValue();
        var diskWrite = _diskWriteCounter.NextValue();
        metrics.DiskIoMbps = Math.Round((diskRead + diskWrite) / 1_000_000.0, 1);

        // Uptime
        metrics.Uptime = DateTime.Now - _bootTime;

        // Health score
        metrics.HealthScore = CalculateHealthScore(metrics);

        // Rolling history
        EnqueueHistory(_cpuHistory, metrics.CpuPercent);
        EnqueueHistory(_ramHistory, metrics.RamPercent);

        return metrics;
    }

    public double[] GetCpuHistory() => _cpuHistory.ToArray();
    public double[] GetRamHistory() => _ramHistory.ToArray();

    private static (double usedGb, double totalGb) GetRamInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                double totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                double freeKb = Convert.ToDouble(obj["FreePhysicalMemory"]);
                double usedKb = totalKb - freeKb;
                return (Math.Round(usedKb / 1_048_576, 1), Math.Round(totalKb / 1_048_576, 1));
            }
        }
        catch { }
        return (0, 0);
    }

    private static Dictionary<string, DiskMetric> GetDiskInfo()
    {
        var result = new Dictionary<string, DiskMetric>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            try
            {
                result[drive.Name] = new DiskMetric
                {
                    Drive = drive.Name,
                    FreeGb = drive.AvailableFreeSpace / (1024L * 1024 * 1024),
                    TotalGb = drive.TotalSize / (1024L * 1024 * 1024)
                };
            }
            catch { }
        }
        return result;
    }

    private static string? FindActiveNetworkAdapter()
    {
        try
        {
            var category = new PerformanceCounterCategory("Network Interface");
            return category.GetInstanceNames()
                .FirstOrDefault(n => !n.Contains("Loopback") && !n.Contains("isatap"));
        }
        catch { return null; }
    }

    private static DateTime GetBootTime()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = obj["LastBootUpTime"]?.ToString();
                if (raw != null)
                    return ManagementDateTimeConverter.ToDateTime(raw);
            }
        }
        catch { }
        return DateTime.Now;
    }

    private static int CalculateHealthScore(ServerMetrics m)
    {
        int score = 100;
        if (m.CpuPercent > 90) score -= 25;
        else if (m.CpuPercent > 70) score -= 10;
        if (m.RamPercent > 90) score -= 20;
        else if (m.RamPercent > 75) score -= 8;
        foreach (var disk in m.Disks.Values)
        {
            if (disk.UsedPercent > 95) score -= 20;
            else if (disk.UsedPercent > 85) score -= 10;
        }
        return Math.Max(0, score);
    }

    private static void EnqueueHistory(Queue<double> q, double value)
    {
        if (q.Count >= 36) q.Dequeue();
        q.Enqueue(value);
    }

    public List<ProcessInfo> GetTopProcesses(int count = 10)
    {
        return Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    return new ProcessInfo
                    {
                        Name = p.ProcessName,
                        Pid = p.Id,
                        MemoryMb = p.WorkingSet64 / (1024 * 1024),
                        CpuPercent = 0 // We update CPU separately
                    };
                }
                catch { return null; }
            })
            .Where(p => p != null)
            .OrderByDescending(p => p!.MemoryMb)
            .Take(count)
            .Select(p => p!)
            .ToList();
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _netInCounter?.Dispose();
        _netOutCounter?.Dispose();
        _diskReadCounter?.Dispose();
        _diskWriteCounter?.Dispose();
    }
}
