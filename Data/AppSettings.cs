using Newtonsoft.Json;
using UnivueGuardian.Models;
using System.Security.Cryptography;

namespace UnivueGuardian.Data;

public class AppSettings
{
    // ── SMTP ──────────────────────────────────────────────
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpFrom { get; set; } = "";
    public string SmtpPassword { get; set; } = "";          // stored encrypted below
    public string SmtpPasswordEncrypted { get; set; } = ""; // DPAPI encrypted
    public string SmtpTo { get; set; } = "";                // semicolon separated

    // ── Alert Rules ───────────────────────────────────────
    public bool AlertServiceDown { get; set; } = true;
    public bool AlertServiceRecovered { get; set; } = true;
    public bool AlertRestartFailed { get; set; } = true;
    public bool AlertDomainDown { get; set; } = true;
    public bool AlertSslExpiry { get; set; } = true;
    public bool AlertHighCpu { get; set; } = false;
    public bool AlertDailyReport { get; set; } = true;
    public TimeSpan DailyReportTime { get; set; } = new TimeSpan(8, 0, 0);

    // ── Thresholds ────────────────────────────────────────
    public int CheckIntervalSeconds { get; set; } = 10;
    public double CpuAlertPercent { get; set; } = 80;
    public long RamAlertMb { get; set; } = 500;
    public int DomainSlowMs { get; set; } = 3000;
    public int SslWarnDays { get; set; } = 30;
    public int MaxRestarts { get; set; } = 3;

    // ── Lists ─────────────────────────────────────────────
    public List<MonitoredService>   Services           { get; set; } = new();
    public List<MonitoredDomain>    Domains            { get; set; } = new();
    public List<MonitoredPort>      Ports              { get; set; } = new();
    public List<DatabaseConnection> Databases          { get; set; } = new();
    public List<string>             WatchedFiles       { get; set; } = new();
    public List<AlertPolicy>        AlertPolicies      { get; set; } = new();
    public List<MaintenanceWindow>  MaintenanceWindows { get; set; } = new();
 

    // ── Persistence ───────────────────────────────────────
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UnivueGuardian", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return CreateDefault();

            var json = File.ReadAllText(ConfigPath);
            var settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? CreateDefault();

            // Decrypt password
            if (!string.IsNullOrEmpty(settings.SmtpPasswordEncrypted))
            {
                try
                {
                    var encrypted = Convert.FromBase64String(settings.SmtpPasswordEncrypted);
                    var decrypted = System.Security.Cryptography.ProtectedData.Unprotect(
                        encrypted, null, System.Security.Cryptography.DataProtectionScope.LocalMachine);
                    settings.SmtpPassword = System.Text.Encoding.UTF8.GetString(decrypted);
                }
                catch { settings.SmtpPassword = ""; }
            }

            return settings;
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

            // Encrypt password before saving
            if (!string.IsNullOrEmpty(SmtpPassword))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(SmtpPassword);
                var encrypted = System.Security.Cryptography.ProtectedData.Protect(
                    bytes, null, System.Security.Cryptography.DataProtectionScope.LocalMachine);
                SmtpPasswordEncrypted = Convert.ToBase64String(encrypted);
            }

            // Don't save plain password to disk
            var clone = (AppSettings)MemberwiseClone();
            clone.SmtpPassword = "";

            var json = JsonConvert.SerializeObject(clone, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Settings save failed: {ex.Message}", ex);
        }
    }

    private static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Services = new List<MonitoredService>
            {
                new() { ServiceName = "RabbitMQ", DisplayName = "RabbitMQ Service", AutoRestart = true },
                new() { ServiceName = "Elasticsearch", DisplayName = "Elasticsearch", AutoRestart = true },
                new() { ServiceName = "MySQL80", DisplayName = "MySQL 8.0", AutoRestart = true },
                new() { ServiceName = "W3SVC", DisplayName = "IIS (W3SVC)", AutoRestart = true },
            }
        };
    }
}
