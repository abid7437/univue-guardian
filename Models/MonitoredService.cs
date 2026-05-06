namespace UnivueGuardian.Models;

public class MonitoredService
{
    public string ServiceName { get; set; } = "";        // e.g. "RabbitMQ"
    public string DisplayName { get; set; } = "";        // e.g. "RabbitMQ Service"
    public bool AutoRestart { get; set; } = true;
    public int MaxRestarts { get; set; } = 3;
    public int RestartCount { get; set; } = 0;
    public DateTime? LastRestartTime { get; set; }

    // Runtime state (not saved to JSON)
    public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;
    public double CpuPercent { get; set; } = 0;
    public long MemoryMb { get; set; } = 0;
    public int? Pid { get; set; }
}

public enum ServiceStatus
{
    Unknown,
    Running,
    Stopped,
    StartPending,
    StopPending,
    Warning   // High CPU or RAM
}
