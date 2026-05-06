namespace UnivueGuardian.Models;

public class NetworkMetrics
{
    public double   InboundMbps       { get; set; }
    public double   OutboundMbps      { get; set; }
    public int      TcpConnectionCount { get; set; }
    public int      UdpListenerCount   { get; set; }
    public double[] InboundHistory    { get; set; } = Array.Empty<double>();
    public double[] OutboundHistory   { get; set; } = Array.Empty<double>();
    public List<PortCheckResult>  PortResults     { get; set; } = new();
    public List<ConnectionInfo>   TopConnections  { get; set; } = new();
}

public class MonitoredPort
{
    public string Host     { get; set; } = "localhost";
    public int    Port     { get; set; }
    public string Label    { get; set; } = "";
    public string Protocol { get; set; } = "TCP";  // TCP or UDP
}

public class PortCheckResult
{
    public string Host      { get; set; } = "";
    public int    Port      { get; set; }
    public string Label     { get; set; } = "";
    public string Protocol  { get; set; } = "TCP";
    public bool   IsOpen    { get; set; }
    public long   ResponseMs { get; set; }
}

public class ConnectionInfo
{
    public string RemoteAddress { get; set; } = "";
    public int    Count         { get; set; }
    public string State         { get; set; } = "";
}
