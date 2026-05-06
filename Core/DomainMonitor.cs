using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using UnivueGuardian.Data;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

[SupportedOSPlatform("windows")]
public class DomainMonitor
{
    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;

    public event Action<MonitoredDomain, DomainStatus>? DomainStateChanged;
    public event Action<string, AlertSeverity, string>? AlertRaised;

    public DomainMonitor(AppSettings settings)
    {
        _settings = settings;

        // Don't throw on bad SSL — we want to report it
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "UnivueGuardian/2.0");
    }

    public async Task CheckAllAsync()
    {
        var tasks = _settings.Domains.Select(d => CheckDomainAsync(d));
        await Task.WhenAll(tasks);
    }

    public async Task CheckDomainAsync(MonitoredDomain domain)
    {
        var oldStatus = domain.Status;
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await _httpClient.GetAsync(domain.Url);
            sw.Stop();

            domain.HttpStatusCode = (int)response.StatusCode;
            domain.ResponseMs = sw.ElapsedMilliseconds;
            domain.LastChecked = DateTime.Now;
            domain.LastError = "";

            bool success = (int)response.StatusCode is >= 200 and < 400;
            domain.RecordCheck(success);

            // Determine status
            if (!success)
                domain.Status = DomainStatus.Down;
            else if (domain.ResponseMs > _settings.DomainSlowMs)
                domain.Status = DomainStatus.Slow;
            else
                domain.Status = DomainStatus.Online;

            // SSL check (async, every check)
            await CheckSslAsync(domain);

            // Override status if SSL expiring soon
            if (domain.SslDaysRemaining >= 0 && domain.SslDaysRemaining < _settings.SslWarnDays
                && domain.Status == DomainStatus.Online)
            {
                domain.Status = domain.SslDaysRemaining <= 0
                    ? DomainStatus.SslExpired
                    : DomainStatus.SslExpiring;
            }
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            domain.Status = DomainStatus.Down;
            domain.ResponseMs = sw.ElapsedMilliseconds;
            domain.LastError = ex.Message;
            domain.RecordCheck(false);
        }
        catch (TaskCanceledException)
        {
            domain.Status = DomainStatus.Down;
            domain.LastError = "Timeout";
            domain.RecordCheck(false);
        }
        catch (Exception ex)
        {
            domain.Status = DomainStatus.Down;
            domain.LastError = ex.Message;
            domain.RecordCheck(false);
        }

        // Raise events on change
        if (domain.Status != oldStatus)
        {
            DomainStateChanged?.Invoke(domain, domain.Status);
            RaiseAlertIfNeeded(domain, oldStatus);
        }
    }

    private async Task CheckSslAsync(MonitoredDomain domain)
    {
        try
        {
            var uri = new Uri(domain.Url);
            if (uri.Scheme != "https") return;

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 443);
            using var ssl = new SslStream(tcp.GetStream(), false,
                (_, cert, _, _) =>
                {
                    if (cert is X509Certificate2 cert2)
                    {
                        domain.SslExpiry = cert2.NotAfter;
                        domain.SslDaysRemaining = (int)(cert2.NotAfter - DateTime.Now).TotalDays;
                    }
                    return true;
                });
            await ssl.AuthenticateAsClientAsync(uri.Host);
        }
        catch
        {
            domain.SslDaysRemaining = -1;
        }
    }

    private void RaiseAlertIfNeeded(MonitoredDomain domain, DomainStatus oldStatus)
    {
        var name = domain.DisplayName.Length > 0 ? domain.DisplayName : domain.Url;

        if (domain.Status == DomainStatus.Down && _settings.AlertDomainDown)
        {
            AlertRaised?.Invoke(name, AlertSeverity.Critical,
                $"{name} is DOWN. Last HTTP: {domain.HttpStatusCode}. {domain.LastError}");
        }
        else if (domain.Status == DomainStatus.Online &&
                 oldStatus == DomainStatus.Down && _settings.AlertServiceRecovered)
        {
            AlertRaised?.Invoke(name, AlertSeverity.Info,
                $"{name} recovered. Response: {domain.ResponseMs}ms.");
        }
        else if (domain.Status is DomainStatus.SslExpiring or DomainStatus.SslExpired
                 && _settings.AlertSslExpiry)
        {
            var severity = domain.SslDaysRemaining <= 7
                ? AlertSeverity.Critical : AlertSeverity.Warning;
            AlertRaised?.Invoke(name, severity,
                $"SSL certificate for {name} expires in {domain.SslDaysRemaining} days ({domain.SslExpiry:yyyy-MM-dd}).");
        }
        else if (domain.Status == DomainStatus.Slow)
        {
            AlertRaised?.Invoke(name, AlertSeverity.Warning,
                $"{name} is SLOW: {domain.ResponseMs}ms (threshold {_settings.DomainSlowMs}ms).");
        }
    }

    public void Dispose() => _httpClient?.Dispose();
}
