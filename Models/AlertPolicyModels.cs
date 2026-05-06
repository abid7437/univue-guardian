namespace UnivueGuardian.Models;

public class AlertPolicy
{
    public string        Name              { get; set; } = "";
    public string        SourcePattern     { get; set; } = ""; // matches source name
    public bool          IsSilenced        { get; set; } = false;
    public int           CooldownSeconds   { get; set; } = 300; // 5 min default
    public int           EscalateAfterCount { get; set; } = 3;  // escalate after 3 same alerts
    public AlertSeverity MinSeverity       { get; set; } = AlertSeverity.Warning;
    public List<string>  NotifyEmails      { get; set; } = new();
}

public class MaintenanceWindow
{
    public string      Name       { get; set; } = "";
    public bool        IsActive   { get; set; } = true;
    public DayOfWeek   DayOfWeek  { get; set; } = DayOfWeek.Sunday;
    public TimeSpan    StartTime  { get; set; } = new TimeSpan(2, 0, 0);  // 2:00 AM
    public TimeSpan    EndTime    { get; set; } = new TimeSpan(4, 0, 0);  // 4:00 AM
    public string      Reason     { get; set; } = "Scheduled maintenance";
}
