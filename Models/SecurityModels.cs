namespace UnivueGuardian.Models;

public class SecurityMetrics
{
    public int  FailedLogins       { get; set; }
    public int  NewFailedLogins    { get; set; }
    public List<SuspiciousProcess>  SuspiciousProcesses  { get; set; } = new();
    public List<FileIntegrityAlert> FileIntegrityAlerts  { get; set; } = new();
    public List<SecurityEvent>      RecentSecurityEvents { get; set; } = new();
    public List<LogonSession>       LogonSessions        { get; set; } = new();
}

public class SuspiciousProcess
{
    public string   Name      { get; set; } = "";
    public int      Pid       { get; set; }
    public long     MemoryMb  { get; set; }
    public DateTime StartTime { get; set; }
}

public class FileIntegrityAlert
{
    public string   FilePath  { get; set; } = "";
    public DateTime ChangedAt { get; set; }
    public string   OldHash   { get; set; } = "";
    public string   NewHash   { get; set; } = "";
}

public class SecurityEvent
{
    public DateTime Time      { get; set; }
    public int      EventId   { get; set; }
    public string   Level     { get; set; } = "";
    public string   Message   { get; set; } = "";
    public string   EventType { get; set; } = "";
}

public class LogonSession
{
    public string Username  { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string State     { get; set; } = "";
}
