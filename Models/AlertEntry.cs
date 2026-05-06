namespace UnivueGuardian.Models;

public class AlertEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public bool EmailSent { get; set; } = false;
    public bool Resolved { get; set; } = false;

    public string TimeDisplay => Time.ToString("HH:mm");
    public string SeverityDisplay => Severity.ToString();
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public class ProcessInfo
{
    public string Name { get; set; } = "";
    public int Pid { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryMb { get; set; }
    public string Status { get; set; } = "Normal"; // "Normal", "High CPU", "High RAM"
}

public class EventLogEntry
{
    public DateTime Time { get; set; }
    public string Level { get; set; } = "";   // ERROR, WARN, INFO, AUTO
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
}

public class ServerMetrics
{
    public double CpuPercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamPercent => RamTotalGb > 0 ? RamUsedGb / RamTotalGb * 100 : 0;

    public Dictionary<string, DiskMetric> Disks { get; set; } = new();
    public double NetworkInMbps { get; set; }
    public double NetworkOutMbps { get; set; }
    public double DiskIoMbps { get; set; }

    public int HealthScore { get; set; } = 100;
    public TimeSpan Uptime { get; set; }
}

public class DiskMetric
{
    public string Drive { get; set; } = "";
    public long FreeGb { get; set; }
    public long TotalGb { get; set; }
    public double UsedPercent => TotalGb > 0 ? (1.0 - (double)FreeGb / TotalGb) * 100 : 0;
}
