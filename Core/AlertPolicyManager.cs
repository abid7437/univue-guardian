using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

public class AlertPolicyManager
{
    private readonly List<AlertPolicy>       _policies;
    private readonly List<MaintenanceWindow> _windows;
    private readonly Dictionary<string, DateTime> _lastAlertTime = new();
    private readonly Dictionary<string, int>      _alertCount    = new();

    public event Action<string, AlertSeverity, string>? EscalatedAlert;

    public AlertPolicyManager(List<AlertPolicy> policies, List<MaintenanceWindow> windows)
    {
        _policies = policies;
        _windows  = windows;
    }

    /// <summary>Returns true if alert should be sent (not suppressed)</summary>
    public bool ShouldSend(string source, AlertSeverity severity, string message)
    {
        // Check maintenance window
        if (IsInMaintenanceWindow()) return false;

        // Check silence rules
        var policy = _policies.FirstOrDefault(p =>
            source.Contains(p.SourcePattern, StringComparison.OrdinalIgnoreCase));

        if (policy != null)
        {
            if (policy.IsSilenced) return false;

            // Deduplication — don't repeat same alert within cooldown
            string key = $"{source}:{severity}";
            if (_lastAlertTime.TryGetValue(key, out var last))
            {
                if (DateTime.Now - last < TimeSpan.FromSeconds(policy.CooldownSeconds))
                    return false;
            }
            _lastAlertTime[key] = DateTime.Now;

            // Escalation count
            if (!_alertCount.ContainsKey(key)) _alertCount[key] = 0;
            _alertCount[key]++;

            // Escalate after threshold
            if (_alertCount[key] >= policy.EscalateAfterCount &&
                severity != AlertSeverity.Critical)
            {
                EscalatedAlert?.Invoke(source, AlertSeverity.Critical,
                    $"[ESCALATED] {message} (occurred {_alertCount[key]} times)");
            }
        }

        return true;
    }

    public bool IsInMaintenanceWindow()
    {
        var now = DateTime.Now;
        return _windows.Any(w =>
            w.IsActive &&
            now.DayOfWeek == w.DayOfWeek &&
            now.TimeOfDay >= w.StartTime &&
            now.TimeOfDay <= w.EndTime);
    }

    public void ResetCount(string source, AlertSeverity severity)
    {
        string key = $"{source}:{severity}";
        _alertCount.Remove(key);
    }
}
