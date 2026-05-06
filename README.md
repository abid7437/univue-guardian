# 🛡️ Univue Guardian

> **Production-grade Windows Server Monitoring System** — Built with .NET 8 WinForms

A powerful, DataDog-inspired server monitoring application for Windows Server environments. Monitor services, domains, IIS, databases, network, security events, and live log files — all from a single beautiful desktop app.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![Windows](https://img.shields.io/badge/Windows-Server%202022-blue)
![License](https://img.shields.io/badge/License-MIT-green)
![Version](https://img.shields.io/badge/Version-2.0-orange)

---

## ✨ Features

### 📊 Dashboard
- Real-time Health Score, CPU, RAM, Disk metrics
- Live animated charts (5-minute rolling window)
- Top RAM consuming processes
- Recent alerts summary + server uptime

### ⚙️ Services Monitor
- Monitor any Windows Service
- **Auto-restart** on crash with configurable max attempts
- Start / Stop / Restart from UI (right-click)
- Email alert on service down / recovered

### 🌐 Domain Monitor
- HTTP/HTTPS availability + response time
- 24-hour uptime percentage
- **SSL certificate expiry** monitoring
- Right-click → Check Now / Remove

### 🖧 IIS Monitor
- App Pool status (Running / Stopped)
- IIS Sites with requests/sec
- 4xx / 5xx error counts + live chart

### 🔌 Network Monitor
- Inbound / Outbound bandwidth live charts
- **Port Monitor** — TCP/UDP (RabbitMQ, MySQL, Redis, etc.)
- Top active TCP connections

### 🔒 Security Monitor
- Failed login attempts (Event ID 4625)
- **Suspicious process detection** (mimikatz, netcat, psexec, etc.)
- File integrity monitoring (SHA-256)
- Active logon sessions

### 🗄️ Database Monitor
- **SQL Server** — slow queries, connection pool, blocked queries, cache hit ratio
- **MySQL** — connections, slow queries via performance_schema
- **PostgreSQL** — availability check

### 📄 Log Files — Real-time
- Add any `.log` / `.txt` file
- **Real-time tail** via FileSystemWatcher
- Color-coded: ERROR / WARN / INFO / DEBUG
- Filter + search, multiple files

### 📋 Event Log
- Windows Event Log live tail
- Filter by source / level / search
- Export to CSV

### 🔔 Alerts
- Alert history with severity levels
- Email sent status per alert
- Critical / Warning / Info counters

### 📑 Reports
- Weekly HTML Report
- Daily Email Report via SMTP
- Security Event CSV Export

### ⚙️ Settings
- SMTP with test email button
- Alert rules toggle (per rule on/off)
- Monitor thresholds configuration
- Password encrypted with **Windows DPAPI**

---

## 🚀 Getting Started

### Requirements

| Requirement | Details |
|-------------|---------|
| OS | Windows 10 / 11 / Server 2016–2022 |
| Runtime | [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Permissions | **Run as Administrator** |

### Quick Start

```bash
# Clone
git clone https://github.com/yourusername/univue-guardian.git
cd univue-guardian

# Restore & Build
dotnet restore
dotnet build -c Release

# Run
dotnet run
```

### Deploy to Server

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish\
```

Copy `publish\` to your server → Run `UnivueGuardian.exe` as Administrator.

---

## 📦 Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| MailKit | 4.16.0 | SMTP email alerts |
| Newtonsoft.Json | 13.0.3 | Settings serialization |
| System.Management | 8.0.0 | WMI queries |
| System.ServiceProcess.ServiceController | 8.0.0 | Service control |
| System.Security.Cryptography.ProtectedData | 8.0.0 | DPAPI encryption |
| Microsoft.Data.SqlClient | 5.2.2 | SQL Server |
| MySqlConnector | 2.3.7 | MySQL |

---

## 📁 Project Structure

```
UnivueGuardian/
├── Program.cs
├── app.manifest
├── Forms/
│   ├── MainForm.cs
│   ├── AddServiceForm.cs
│   ├── AddDomainForm.cs
│   ├── AddPortForm.cs
│   └── AddDatabaseForm.cs
├── Core/
│   ├── SystemMetricsCollector.cs
│   ├── ServiceMonitor.cs
│   ├── DomainMonitor.cs
│   ├── IisMonitor.cs
│   ├── NetworkMonitor.cs
│   ├── SecurityMonitor.cs
│   ├── DatabaseMonitor.cs
│   ├── FileWatcherMonitor.cs
│   ├── AlertPolicyManager.cs
│   ├── EmailSender.cs
│   ├── EventLogger.cs
│   └── ReportGenerator.cs
├── Models/
│   ├── MonitoredService.cs
│   ├── MonitoredDomain.cs
│   ├── AlertEntry.cs
│   ├── IisModels.cs
│   ├── NetworkModels.cs
│   ├── SecurityModels.cs
│   ├── DatabaseModels.cs
│   ├── AlertPolicyModels.cs
│   └── FileWatcherModels.cs
└── Data/
    └── AppSettings.cs
```

---

## ⚙️ Configuration

Settings stored at:
```
%APPDATA%\UnivueGuardian\settings.json
```

### SMTP Setup (Gmail)
1. Go to **Settings → Email / SMTP**
2. Host: `smtp.gmail.com`, Port: `587`
3. Use a [Gmail App Password](https://support.google.com/accounts/answer/185833)
4. Click **Send Test Email** to verify

---

## 🔌 Common Ports to Monitor

| Service | Port |
|---------|------|
| RabbitMQ | 5672 |
| MySQL | 3306 |
| SQL Server | 1433 |
| Redis | 6379 |
| Elasticsearch | 9200 |
| HTTP | 80 |
| HTTPS | 443 |

---

## 🗄️ Database Connection Strings

**SQL Server:**
```
Server=localhost;Database=master;User Id=sa;Password=pass;TrustServerCertificate=True;
```

**MySQL:**
```
Server=localhost;Port=3306;Database=mydb;Uid=root;Pwd=pass;
```

---

## 🔒 Security Notes

- Requires **Administrator** for service control + security event log
- SMTP password encrypted with Windows DPAPI — never plain text
- Fully local — no data sent to external servers

---

## 🤝 Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/AmazingFeature`
3. Commit: `git commit -m 'Add AmazingFeature'`
4. Push: `git push origin feature/AmazingFeature`
5. Open a Pull Request

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

---

<div align="center">
  <strong>Univue Guardian v2.0</strong><br/>
  Production Windows Server Monitoring — Made for server administrators
</div>
