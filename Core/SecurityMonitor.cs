using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class SecurityMonitor
{
    private readonly Dictionary<string, string> _fileHashes = new();
    private int _lastFailedLoginCount = 0;
    private DateTime _lastSecurityCheck = DateTime.MinValue;

    public event Action<string, AlertSeverity, string>? AlertRaised;

    // ── Suspicious process names to watch ────────────────
    private static readonly HashSet<string> SuspiciousNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mimikatz", "pwdump", "fgdump", "wce", "gsecdump",
        "procdump", "meterpreter", "nc", "ncat", "netcat",
        "psexec", "wmiexec", "smbexec", "crackmapexec"
    };

    public SecurityMetrics Collect(List<string> watchedFiles)
    {
        var m = new SecurityMetrics();

        m.FailedLogins      = GetFailedLoginCount();
        m.NewFailedLogins   = Math.Max(0, m.FailedLogins - _lastFailedLoginCount);
        _lastFailedLoginCount = m.FailedLogins;

        m.SuspiciousProcesses = DetectSuspiciousProcesses();
        m.FileIntegrityAlerts = CheckFileIntegrity(watchedFiles);
        m.RecentSecurityEvents = GetRecentSecurityEvents(20);
        m.LogonSessions       = GetActiveLogonSessions();

        // Alerts
        if (m.NewFailedLogins >= 5)
            AlertRaised?.Invoke("Security", AlertSeverity.Critical,
                $"{m.NewFailedLogins} failed login attempts detected in last check.");

        foreach (var p in m.SuspiciousProcesses)
            AlertRaised?.Invoke("Security", AlertSeverity.Critical,
                $"Suspicious process detected: {p.Name} (PID {p.Pid})");

        foreach (var f in m.FileIntegrityAlerts)
            AlertRaised?.Invoke("File Integrity", AlertSeverity.Warning,
                $"File changed: {f.FilePath}");

        return m;
    }

    private static int GetFailedLoginCount()
    {
        try
        {
            using var log = new System.Diagnostics.EventLog("Security");
            var since = DateTime.Now.AddHours(-1);
            return log.Entries.Cast<System.Diagnostics.EventLogEntry>()
                .Where(e => e.TimeWritten >= since && e.InstanceId == 4625) // 4625 = failed logon
                .Count();
        }
        catch { return 0; }
    }

    private static List<SuspiciousProcess> DetectSuspiciousProcesses()
    {
        var list = new List<SuspiciousProcess>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (SuspiciousNames.Contains(proc.ProcessName))
                    {
                        list.Add(new SuspiciousProcess
                        {
                            Name      = proc.ProcessName,
                            Pid       = proc.Id,
                            MemoryMb  = proc.WorkingSet64 / (1024 * 1024),
                            StartTime = proc.StartTime
                        });
                    }
                }
                catch { }
            }
        }
        catch { }
        return list;
    }

    public List<FileIntegrityAlert> CheckFileIntegrity(List<string> filePaths)
    {
        var alerts = new List<FileIntegrityAlert>();
        foreach (var path in filePaths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                string hash = ComputeHash(path);

                if (_fileHashes.TryGetValue(path, out string? oldHash))
                {
                    if (oldHash != hash)
                    {
                        alerts.Add(new FileIntegrityAlert
                        {
                            FilePath    = path,
                            ChangedAt   = DateTime.Now,
                            OldHash     = oldHash,
                            NewHash     = hash
                        });
                        _fileHashes[path] = hash;
                    }
                }
                else
                {
                    _fileHashes[path] = hash; // First time — baseline
                }
            }
            catch { }
        }
        return alerts;
    }

    private static string ComputeHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes);
    }

    private static List<SecurityEvent> GetRecentSecurityEvents(int count)
    {
        var list = new List<SecurityEvent>();
        try
        {
            using var log = new System.Diagnostics.EventLog("Security");
            int total = log.Entries.Count;
            int start = Math.Max(0, total - count);

            for (int i = start; i < total; i++)
            {
                try
                {
                    var e = log.Entries[i];
                    list.Add(new SecurityEvent
                    {
                        Time      = e.TimeWritten,
                        EventId   = (int)e.InstanceId,
                        Level     = e.EntryType == EventLogEntryType.Error ? "ERROR" : "INFO",
                        Message   = e.Message.Length > 150 ? e.Message[..150] + "…" : e.Message,
                        EventType = MapSecurityEventType((int)e.InstanceId)
                    });
                }
                catch { }
            }
        }
        catch { }
        return list.OrderByDescending(e => e.Time).ToList();
    }

    private static List<LogonSession> GetActiveLogonSessions()
    {
        var list = new List<LogonSession>();
        try
        {
            var output = RunCommand("query session");
            foreach (var line in output.Split('\n').Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    list.Add(new LogonSession
                    {
                        Username = parts[0],
                        SessionId = parts.Length > 2 ? parts[2] : "—",
                        State    = parts.Length > 3 ? parts[3] : "Active"
                    });
                }
            }
        }
        catch { }
        return list;
    }

    private static string RunCommand(string cmd)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c {cmd}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            return output;
        }
        catch { return ""; }
    }

    private static string MapSecurityEventType(int id) => id switch
    {
        4624 => "Logon Success",
        4625 => "Logon Failed",
        4634 => "Logoff",
        4648 => "Logon with Explicit Credentials",
        4720 => "User Account Created",
        4722 => "User Account Enabled",
        4723 => "Password Change Attempt",
        4724 => "Password Reset",
        4725 => "User Account Disabled",
        4726 => "User Account Deleted",
        4740 => "Account Locked Out",
        4756 => "Member Added to Group",
        4776 => "Credential Validation",
        _    => $"Event {id}"
    };
}
