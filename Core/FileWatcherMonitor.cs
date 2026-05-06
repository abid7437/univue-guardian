using System.Runtime.Versioning;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class FileWatcherMonitor : IDisposable
{
    private readonly List<FileWatcherEntry> _entries = new();
    private readonly object _lock = new();

    public event Action<string, string>? NewLineReceived;

    public void AddFile(string filePath)
    {
        lock (_lock)
        {
            if (_entries.Any(e => e.FilePath == filePath)) return;

            var entry = new FileWatcherEntry { FilePath = filePath };

            try
            {
                if (File.Exists(filePath))
                {
                    var lines = ReadLastLines(filePath, 50);
                    entry.RecentLines.AddRange(lines.Select(l => ParseLine(l)));
                    entry.LastSize = new FileInfo(filePath).Length;
                }
            }
            catch { }

            try
            {
                var dir = Path.GetDirectoryName(filePath) ?? ".";
                var file = Path.GetFileName(filePath);

                entry.Watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                entry.Watcher.Changed += (s, e) => OnFileChanged(filePath);
            }
            catch { }

            _entries.Add(entry);
        }
    }

    public void RemoveFile(string filePath)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.FilePath == filePath);
            if (entry == null) return;
            entry.Watcher?.Dispose();
            _entries.Remove(entry);
        }
    }

    public List<FileWatcherEntry> GetEntries()
    {
        lock (_lock) return _entries.ToList();
    }

    public List<LogLine> GetLines(string filePath, string levelFilter = "All", string search = "")
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.FilePath == filePath);
            if (entry == null) return new();

            return entry.RecentLines
                .Where(l => levelFilter == "All" || l.Level == levelFilter)
                .Where(l => string.IsNullOrEmpty(search) ||
                            l.Raw.Contains(search, StringComparison.OrdinalIgnoreCase))
                .TakeLast(500)
                .ToList();
        }
    }

    private void OnFileChanged(string filePath)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.FilePath == filePath);
            if (entry == null) return;

            try
            {
                var fi = new FileInfo(filePath);
                if (fi.Length <= entry.LastSize) return;

                using var fs = new FileStream(filePath, FileMode.Open,
                                       FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(entry.LastSize, SeekOrigin.Begin);
                using var reader = new StreamReader(fs);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var logLine = ParseLine(line);
                    entry.RecentLines.Add(logLine);
                    if (entry.RecentLines.Count > 2000)
                        entry.RecentLines.RemoveAt(0);
                    NewLineReceived?.Invoke(filePath, line);
                }
                entry.LastSize = fi.Length;
            }
            catch { }
        }
    }

    private static List<string> ReadLastLines(string filePath, int count)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open,
                                   FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var all = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                if (!string.IsNullOrWhiteSpace(line)) all.Add(line);
            return all.TakeLast(count).ToList();
        }
        catch { return new(); }
    }

    public static LogLine ParseLine(string raw)
    {
        var line = new LogLine { Raw = raw, Time = DateTime.Now };

        if (raw.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase))
            line.Level = "ERROR";
        else if (raw.Contains("WARN", StringComparison.OrdinalIgnoreCase) ||
                 raw.Contains("WARNING", StringComparison.OrdinalIgnoreCase))
            line.Level = "WARN";
        else if (raw.Contains("DEBUG", StringComparison.OrdinalIgnoreCase))
            line.Level = "DEBUG";
        else
            line.Level = "INFO";

        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(raw,
                @"\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}");
            if (match.Success && DateTime.TryParse(match.Value, out var dt))
                line.Time = dt;
        }
        catch { }

        return line;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var e in _entries)
                e.Watcher?.Dispose();
            _entries.Clear();
        }
    }
}