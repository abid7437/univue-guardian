namespace UnivueGuardian.Models;

public class MonitoredDomain
{
    public string Url { get; set; } = "";           // e.g. "https://domain1.com"
    public string DisplayName { get; set; } = "";   // friendly name

    // Runtime state
    public DomainStatus Status { get; set; } = DomainStatus.Unknown;
    public int HttpStatusCode { get; set; } = 0;
    public long ResponseMs { get; set; } = 0;
    public double Uptime24h { get; set; } = 100.0;
    public int SslDaysRemaining { get; set; } = -1;
    public DateTime? SslExpiry { get; set; }
    public string LastError { get; set; } = "";
    public DateTime? LastChecked { get; set; }

    // Uptime tracking (circular buffer)
    private readonly Queue<bool> _uptimeHistory = new(1440); // 24h at 1-min checks
    public void RecordCheck(bool success)
    {
        if (_uptimeHistory.Count >= 1440) _uptimeHistory.Dequeue();
        _uptimeHistory.Enqueue(success);
        if (_uptimeHistory.Count > 0)
            Uptime24h = Math.Round(_uptimeHistory.Count(x => x) * 100.0 / _uptimeHistory.Count, 1);
    }
}

public enum DomainStatus
{
    Unknown,
    Online,
    Slow,
    Down,
    SslExpiring,
    SslExpired
}
