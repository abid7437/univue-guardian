using System.Diagnostics;
using System.Runtime.Versioning;
using UnivueGuardian.Models;
using EventLogEntry = UnivueGuardian.Models.EventLogEntry;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class EventLogReader : IDisposable
{
    private readonly List<EventLogEntry> _entries = new();
    private readonly object _lock = new();
    private readonly EventLog _appLog;
    private readonly EventLog _sysLog;
    private int _lastAppIndex = -1;
    private int _lastSysIndex = -1;

    public event Action<EventLogEntry>? NewEntryReceived;

    public EventLogReader()
    {
        _appLog = new EventLog("Application");
        _sysLog = new EventLog("System");

        // Start from current end — don't flood with old entries
        _lastAppIndex = _appLog.Entries.Count - 1;
        _lastSysIndex = _sysLog.Entries.Count - 1;

        // Load last 50 recent entries for initial display
        LoadRecent(50);
    }

    /// <summary>Called by main timer to poll for new Windows Event Log entries.</summary>
    public void Poll()
    {
        PollLog(_appLog, ref _lastAppIndex, "Application");
        PollLog(_sysLog, ref _lastSysIndex, "System");
    }

    private void PollLog(EventLog log, ref int lastIndex, string source)
    {
        try
        {
            int current = log.Entries.Count - 1;
            if (current <= lastIndex) return;

            for (int i = lastIndex + 1; i <= current; i++)
            {
                try
                {
                    var raw = log.Entries[i];
                    var entry = new EventLogEntry
                    {
                        Time = raw.TimeWritten,
                        Level = MapLevel(raw.EntryType),
                        Source = $"{source} › {raw.Source}",
                        Message = raw.Message.Length > 200
                            ? raw.Message[..200] + "…"
                            : raw.Message
                    };
                    lock (_lock) _entries.Add(entry);
                    NewEntryReceived?.Invoke(entry);
                }
                catch { }
            }
            lastIndex = current;
        }
        catch { }
    }

    private void LoadRecent(int count)
    {
        try
        {
            var entries = new List<EventLogEntry>();
            LoadFromLog(_appLog, "Application", count / 2, entries);
            LoadFromLog(_sysLog, "System", count / 2, entries);
            lock (_lock)
            {
                _entries.AddRange(entries.OrderBy(e => e.Time));
            }
        }
        catch { }
    }

    private static void LoadFromLog(EventLog log, string source, int count, List<EventLogEntry> target)
    {
        int total = log.Entries.Count;
        int start = Math.Max(0, total - count);
        for (int i = start; i < total; i++)
        {
            try
            {
                var raw = log.Entries[i];
                target.Add(new EventLogEntry
                {
                    Time = raw.TimeWritten,
                    Level = MapLevel(raw.EntryType),
                    Source = $"{source} › {raw.Source}",
                    Message = raw.Message.Length > 200
                        ? raw.Message[..200] + "…"
                        : raw.Message
                });
            }
            catch { }
        }
    }

    public void AddGuardianEntry(string level, string message)
    {
        var entry = new EventLogEntry
        {
            Time = DateTime.Now,
            Level = level,
            Source = "Univue Guardian",
            Message = message
        };
        lock (_lock) _entries.Add(entry);
        NewEntryReceived?.Invoke(entry);
    }

    public IEnumerable<EventLogEntry> GetFiltered(
        string source = "All",
        string level = "All",
        string search = "")
    {
        lock (_lock)
        {
            return _entries
                .AsEnumerable()
                .Where(e => source == "All" || e.Source.Contains(source, StringComparison.OrdinalIgnoreCase))
                .Where(e => level == "All" || e.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                .Where(e => string.IsNullOrEmpty(search) ||
                            e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            e.Source.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.Time)
                .Take(500)
                .ToList();
        }
    }

    public string ExportCsv(IEnumerable<EventLogEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Time,Level,Source,Message");
        foreach (var e in entries)
            sb.AppendLine($"\"{e.Time:yyyy-MM-dd HH:mm:ss}\",\"{e.Level}\",\"{e.Source}\",\"{e.Message.Replace("\"", "''")}\"");
        return sb.ToString();
    }

    private static string MapLevel(EventLogEntryType t) => t switch
    {
        EventLogEntryType.Error   => "ERROR",
        EventLogEntryType.Warning => "WARN",
        _                         => "INFO"
    };

    public void Dispose()
    {
        _appLog.Dispose();
        _sysLog.Dispose();
    }
}
