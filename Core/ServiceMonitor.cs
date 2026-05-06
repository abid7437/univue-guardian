using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;
using UnivueGuardian.Data;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class ServiceMonitor
{
    private readonly AppSettings _settings;

    public event Action<MonitoredService, ServiceStatus, string>? ServiceStateChanged;
    public event Action<string, AlertSeverity, string>? AlertRaised;

    public ServiceMonitor(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>Check all monitored services. Called by main timer.</summary>
    public void CheckAll()
    {
        foreach (var svc in _settings.Services)
            CheckService(svc);
    }

    public void CheckService(MonitoredService svc)
    {
        try
        {
            using var sc = new ServiceController(svc.ServiceName);
            var newStatus = MapStatus(sc.Status);

            // Enrich with CPU/RAM from process
            EnrichWithProcessStats(svc, sc);

            // High CPU threshold override
            if (newStatus == ServiceStatus.Running &&
                svc.CpuPercent > _settings.CpuAlertPercent)
                newStatus = ServiceStatus.Warning;

            bool changed = newStatus != svc.Status;
            var oldStatus = svc.Status;
            svc.Status = newStatus;

            if (!changed) return;

            // Raise UI event
            string detail = newStatus == ServiceStatus.Warning
                ? $"High CPU {svc.CpuPercent:F1}%"
                : sc.Status.ToString();
            ServiceStateChanged?.Invoke(svc, newStatus, detail);

            // ── Auto-restart logic ─────────────────────────────
            if (newStatus == ServiceStatus.Stopped && svc.AutoRestart)
            {
                if (svc.RestartCount < svc.MaxRestarts)
                {
                    bool ok = TryRestartService(svc.ServiceName);
                    svc.RestartCount++;
                    svc.LastRestartTime = DateTime.Now;

                    if (ok)
                    {
                        svc.RestartCount = 0;
                        AlertRaised?.Invoke(svc.ServiceName, AlertSeverity.Info,
                            $"{svc.DisplayName} auto-restarted successfully.");
                    }
                    else
                    {
                        AlertRaised?.Invoke(svc.ServiceName, AlertSeverity.Critical,
                            $"{svc.DisplayName} stopped — restart failed ({svc.RestartCount}/{svc.MaxRestarts}).");
                    }
                }
                else
                {
                    AlertRaised?.Invoke(svc.ServiceName, AlertSeverity.Critical,
                        $"{svc.DisplayName} stopped — max restart attempts reached ({svc.MaxRestarts}).");
                }
            }
            else if (newStatus == ServiceStatus.Running && oldStatus == ServiceStatus.Stopped)
            {
                svc.RestartCount = 0;
                if (_settings.AlertServiceRecovered)
                    AlertRaised?.Invoke(svc.ServiceName, AlertSeverity.Info,
                        $"{svc.DisplayName} recovered and is running.");
            }
            else if (newStatus == ServiceStatus.Warning && _settings.AlertHighCpu)
            {
                AlertRaised?.Invoke(svc.ServiceName, AlertSeverity.Warning,
                    $"{svc.DisplayName} high CPU: {svc.CpuPercent:F1}% (threshold {_settings.CpuAlertPercent}%).");
            }
        }
        catch (InvalidOperationException)
        {
            // Service not installed — mark unknown
            if (svc.Status != ServiceStatus.Unknown)
            {
                svc.Status = ServiceStatus.Unknown;
                ServiceStateChanged?.Invoke(svc, ServiceStatus.Unknown, "Service not found");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ServiceMonitor] Error checking {svc.ServiceName}: {ex.Message}");
        }
    }

    public bool StartService(string serviceName, int timeoutMs = 15000)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running) return true;
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(timeoutMs));
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ServiceMonitor] Start failed for {serviceName}: {ex.Message}");
            return false;
        }
    }

    public bool StopService(string serviceName, int timeoutMs = 15000)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped) return true;
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(timeoutMs));
            return sc.Status == ServiceControllerStatus.Stopped;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ServiceMonitor] Stop failed for {serviceName}: {ex.Message}");
            return false;
        }
    }

    public bool TryRestartService(string serviceName)
    {
        try
        {
            bool stopped = StopService(serviceName);
            if (!stopped) return false;
            Thread.Sleep(1000);
            return StartService(serviceName);
        }
        catch
        {
            return false;
        }
    }

    private static ServiceStatus MapStatus(ServiceControllerStatus s) => s switch
    {
        ServiceControllerStatus.Running => ServiceStatus.Running,
        ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
        ServiceControllerStatus.StartPending => ServiceStatus.StartPending,
        ServiceControllerStatus.StopPending => ServiceStatus.StopPending,
        _ => ServiceStatus.Unknown
    };

    private static void EnrichWithProcessStats(MonitoredService svc, ServiceController sc)
    {
        try
        {
            if (sc.Status != ServiceControllerStatus.Running) return;

            // Get process ID via WMI
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT ProcessId FROM Win32_Service WHERE Name='{sc.ServiceName}'");
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                int pid = Convert.ToInt32(obj["ProcessId"]);
                if (pid == 0) return;
                svc.Pid = pid;

                using var proc = Process.GetProcessById(pid);
                svc.MemoryMb = proc.WorkingSet64 / (1024 * 1024);
                // CPU requires two samples; use WorkingSet as proxy
                break;
            }
        }
        catch { }
    }
}
