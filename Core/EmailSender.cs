using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using UnivueGuardian.Data;
using UnivueGuardian.Models;

namespace UnivueGuardian.Core;

public class EmailSender
{
    private readonly AppSettings _settings;

    public EmailSender(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<bool> SendAlertAsync(AlertEntry alert)
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpFrom) ||
            string.IsNullOrWhiteSpace(_settings.SmtpTo)) return false;

        string subject = alert.Severity switch
        {
            AlertSeverity.Critical => $"🚨 CRITICAL — {alert.Source}",
            AlertSeverity.Warning  => $"⚠️ WARNING — {alert.Source}",
            _                      => $"ℹ️ INFO — {alert.Source}"
        };

        string body = BuildAlertHtml(alert);
        return await SendAsync(subject, body);
    }

    public async Task<bool> SendDailyReportAsync(
        ServerMetrics metrics,
        IEnumerable<MonitoredService> services,
        IEnumerable<MonitoredDomain> domains,
        IEnumerable<AlertEntry> todayAlerts)
    {
        string body = BuildDailyReportHtml(metrics, services, domains, todayAlerts);
        return await SendAsync($"📊 Daily Server Report — {DateTime.Now:yyyy-MM-dd}", body);
    }

    public async Task<bool> SendTestEmailAsync()
    {
        return await SendAsync(
            "✅ Univue Guardian — Test Email",
            "<p style='font-family:sans-serif'>SMTP configuration is working correctly.</p>");
    }

    private async Task<bool> SendAsync(string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_settings.SmtpFrom));

            foreach (var recipient in _settings.SmtpTo.Split(';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }

            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort,
                SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpFrom, _settings.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EmailSender] {ex.Message}");
            return false;
        }
    }

    private static string BuildAlertHtml(AlertEntry alert)
    {
        var color = alert.Severity switch
        {
            AlertSeverity.Critical => "#c62828",
            AlertSeverity.Warning  => "#e65100",
            _                      => "#1565c0"
        };
        var icon = alert.Severity switch
        {
            AlertSeverity.Critical => "🚨",
            AlertSeverity.Warning  => "⚠️",
            _                      => "ℹ️"
        };

        return $"""
        <div style="font-family:sans-serif;max-width:560px;margin:auto;border:1px solid #eee;border-radius:8px;overflow:hidden;">
          <div style="background:{color};padding:16px 20px;color:#fff;">
            <h2 style="margin:0;font-size:18px;">{icon} {alert.Severity.ToString().ToUpper()} Alert</h2>
            <p style="margin:4px 0 0;font-size:13px;opacity:.85;">Univue Guardian — {alert.Time:yyyy-MM-dd HH:mm:ss}</p>
          </div>
          <div style="padding:20px;">
            <table style="width:100%;border-collapse:collapse;font-size:13px;">
              <tr><td style="padding:6px;color:#777;width:120px;">Source</td><td style="padding:6px;font-weight:500;">{alert.Source}</td></tr>
              <tr style="background:#f9f9f9;"><td style="padding:6px;color:#777;">Message</td><td style="padding:6px;">{alert.Message}</td></tr>
              <tr><td style="padding:6px;color:#777;">Time</td><td style="padding:6px;">{alert.Time:HH:mm:ss}</td></tr>
            </table>
          </div>
          <div style="background:#f5f5f5;padding:10px 20px;font-size:11px;color:#aaa;">
            Sent by Univue Guardian v2.0 — SERVER-01
          </div>
        </div>
        """;
    }

    private static string BuildDailyReportHtml(
        ServerMetrics metrics,
        IEnumerable<MonitoredService> services,
        IEnumerable<MonitoredDomain> domains,
        IEnumerable<AlertEntry> alerts)
    {
        var svcRows = string.Concat(services.Select(s =>
            $"<tr><td style='padding:5px 8px;'>{s.DisplayName}</td>" +
            $"<td style='padding:5px 8px;color:{(s.Status == ServiceStatus.Running ? "#2e7d32" : "#c62828")};font-weight:500;'>{s.Status}</td>" +
            $"<td style='padding:5px 8px;'>{s.CpuPercent:F1}%</td>" +
            $"<td style='padding:5px 8px;'>{s.MemoryMb} MB</td></tr>"));

        var domainRows = string.Concat(domains.Select(d =>
            $"<tr><td style='padding:5px 8px;'>{d.DisplayName}</td>" +
            $"<td style='padding:5px 8px;color:{(d.Status == DomainStatus.Online ? "#2e7d32" : "#c62828")};font-weight:500;'>{d.Status}</td>" +
            $"<td style='padding:5px 8px;'>{d.ResponseMs}ms</td>" +
            $"<td style='padding:5px 8px;'>{d.Uptime24h}%</td>" +
            $"<td style='padding:5px 8px;'>{d.SslDaysRemaining}d</td></tr>"));

        var alertRows = string.Concat(alerts.Take(20).Select(a =>
            $"<tr><td style='padding:4px 8px;color:#777;'>{a.Time:HH:mm}</td>" +
            $"<td style='padding:4px 8px;'>{a.Severity}</td>" +
            $"<td style='padding:4px 8px;font-weight:500;'>{a.Source}</td>" +
            $"<td style='padding:4px 8px;'>{a.Message}</td></tr>"));

        return $"""
        <div style="font-family:sans-serif;max-width:700px;margin:auto;">
          <div style="background:#1a237e;padding:18px 24px;color:#fff;border-radius:8px 8px 0 0;">
            <h2 style="margin:0;">📊 Daily Server Health Report</h2>
            <p style="margin:4px 0 0;opacity:.7;">{DateTime.Now:dddd, MMMM dd yyyy} — SERVER-01</p>
          </div>
          <div style="border:1px solid #e0e0e0;border-top:none;padding:20px;border-radius:0 0 8px 8px;">
            <h3 style="color:#333;font-size:14px;margin:0 0 10px;">System Metrics</h3>
            <table style="font-size:12px;">
              <tr><td style="color:#777;padding:3px 8px;">CPU</td><td style="font-weight:500;">{metrics.CpuPercent}%</td></tr>
              <tr><td style="color:#777;padding:3px 8px;">RAM</td><td style="font-weight:500;">{metrics.RamUsedGb}GB / {metrics.RamTotalGb}GB ({metrics.RamPercent:F0}%)</td></tr>
              <tr><td style="color:#777;padding:3px 8px;">Health Score</td><td style="font-weight:500;color:#2e7d32;">{metrics.HealthScore}%</td></tr>
              <tr><td style="color:#777;padding:3px 8px;">Uptime</td><td style="font-weight:500;">{metrics.Uptime.Days}d {metrics.Uptime.Hours}h {metrics.Uptime.Minutes}m</td></tr>
            </table>
            <hr style="border:none;border-top:1px solid #eee;margin:16px 0;"/>
            <h3 style="color:#333;font-size:14px;margin:0 0 10px;">Services</h3>
            <table style="width:100%;border-collapse:collapse;font-size:12px;border:1px solid #eee;">
              <tr style="background:#f5f5f5;"><th style="padding:6px 8px;text-align:left;">Service</th><th>Status</th><th>CPU</th><th>RAM</th></tr>
              {svcRows}
            </table>
            <hr style="border:none;border-top:1px solid #eee;margin:16px 0;"/>
            <h3 style="color:#333;font-size:14px;margin:0 0 10px;">Domains</h3>
            <table style="width:100%;border-collapse:collapse;font-size:12px;border:1px solid #eee;">
              <tr style="background:#f5f5f5;"><th style="padding:6px 8px;text-align:left;">Domain</th><th>Status</th><th>Response</th><th>Uptime 24h</th><th>SSL</th></tr>
              {domainRows}
            </table>
            <hr style="border:none;border-top:1px solid #eee;margin:16px 0;"/>
            <h3 style="color:#333;font-size:14px;margin:0 0 10px;">Today's Alerts</h3>
            <table style="width:100%;border-collapse:collapse;font-size:12px;border:1px solid #eee;">
              <tr style="background:#f5f5f5;"><th style="padding:5px 8px;text-align:left;">Time</th><th>Severity</th><th>Source</th><th>Message</th></tr>
              {alertRows}
            </table>
          </div>
        </div>
        """;
    }
}
