
namespace UnivueGuardian.Models;

public class FileWatcherEntry
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public long LastSize { get; set; } = 0;
    public List<LogLine> RecentLines { get; set; } = new();
    public FileSystemWatcher? Watcher { get; set; }
    public bool IsActive => Watcher?.EnableRaisingEvents ?? false;
}

public class LogLine
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Level { get; set; } = "INFO";
    public string Raw { get; set; } = "";
}