namespace UnivueGuardian.Models;

public class IisMetrics
{
    public double RequestsPerSec      { get; set; }
    public long   ActiveConnections   { get; set; }
    public double BytesReceivedPerSec { get; set; }  // KB/s
    public double BytesSentPerSec     { get; set; }  // KB/s
    public long   Error4xxCount       { get; set; }
    public long   Error5xxCount       { get; set; }
    public double[] RequestHistory    { get; set; } = Array.Empty<double>();

    public List<AppPoolInfo>  AppPools { get; set; } = new();
    public List<IisSiteInfo>  Sites    { get; set; } = new();
}

public class AppPoolInfo
{
    public string               Name             { get; set; } = "";
    public string               RuntimeVersion   { get; set; } = "";
    public AppPoolState         State            { get; set; }
    public List<WorkerProcessInfo> WorkerProcesses { get; set; } = new();
    public long TotalMemoryMb => WorkerProcesses.Sum(w => w.MemoryMb);
}

public class WorkerProcessInfo
{
    public int    Pid      { get; set; }
    public long   MemoryMb { get; set; }
    public string AppPool  { get; set; } = "";
}

public class IisSiteInfo
{
    public string        Name           { get; set; } = "";
    public int           SiteId         { get; set; }
    public SiteState     State          { get; set; }
    public double        RequestsPerSec { get; set; }
    public List<string>  Bindings       { get; set; } = new();
    public string        BindingDisplay => Bindings.FirstOrDefault() ?? "—";
}

public enum AppPoolState { Unknown, Running, Stopped, Starting, Stopping }
public enum SiteState    { Unknown, Running, Stopped, Starting, Stopping }
