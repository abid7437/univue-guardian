using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using UnivueGuardian.Core;
using UnivueGuardian.Data;
using UnivueGuardian.Models;

namespace UnivueGuardian.Forms;

[SupportedOSPlatform("windows")]
public partial class MainForm : Form
{
    // ── Core ──────────────────────────────────────────────
    private AppSettings _settings = null!;
    private SystemMetricsCollector _metrics = null!;
    private ServiceMonitor _svcMonitor = null!;
    private DomainMonitor _domainMonitor = null!;
    private EmailSender _emailSender = null!;
    private EventLogReader _eventLog = null!;

    // ── Timers ────────────────────────────────────────────
    private System.Windows.Forms.Timer _mainTimer = null!;
    private System.Windows.Forms.Timer _uiTimer = null!;
    private DateTime _startTime = DateTime.Now;
    private int _nextCheckCountdown = 0;

    // ── Alert store ───────────────────────────────────────
    private readonly List<AlertEntry> _alerts = new();

    // ── Chart data ────────────────────────────────────────
    private double[] _cpuChartData = Array.Empty<double>();
    private double[] _ramChartData = Array.Empty<double>();

    // ── Current metrics ───────────────────────────────────
    private ServerMetrics _currentMetrics = new();
    private DateTime _lastDailyReport = DateTime.MinValue;

    // ── Navigation ────────────────────────────────────────
    private int _activeTabIndex = 0;
    private List<Label> _navButtons = new();
    private List<Panel> _sideIcons = new();
    private List<Panel> _pages = new();
    private Panel _contentPanel = null!;

    // ── Dashboard controls ────────────────────────────────
    private Label lblHealthScore = null!, lblCpu = null!, lblRam = null!, lblDisk = null!;
    private ProgressBar pbHealth = null!, pbCpu = null!, pbRam = null!, pbDisk = null!;
    private Label lblUptime = null!;
    private Panel pnlCpuChart = null!, pnlRamChart = null!;
    private ListView lvAlertsDash = null!, lvRamProcs = null!;

    // ── Infra controls ────────────────────────────────────
    private Label lblInfraCpu = null!, lblInfraRam = null!, lblInfraNet = null!, lblInfraDisk = null!;
    private Panel pnlInfraCpuChart = null!, pnlInfraRamChart = null!;
    private ListView lvProcesses = null!;

    // ── Services controls ─────────────────────────────────
    private ListView lvServices = null!;
    private Label lblSvcRunning = null!, lblSvcStopped = null!, lblSvcWarn = null!, lblLastCheck = null!;

    // ── Domains controls ──────────────────────────────────
    private ListView lvDomains = null!;

    // ── Event Log controls ────────────────────────────────
    private RichTextBox rtbLog = null!;
    private ComboBox cboLogSource = null!, cboLogLevel = null!;
    private TextBox txtLogSearch = null!;

    // ── Alerts controls ───────────────────────────────────
    private Label lblAlertTotal = null!, lblAlertCrit = null!, lblAlertWarn = null!, lblAlertResolved = null!;
    private ListView lvAlerts = null!;

    // ── Status bar ────────────────────────────────────────
    private Label lblStatusMonitor = null!, lblStatusSmtp = null!;
    private Label lblStatusCritical = null!, lblStatusWarn = null!, lblStatusRight = null!;


    // ── New module monitors ───────────────────────────────
    private IisMonitor _iisMonitor = null!;
    private NetworkMonitor _networkMonitor = null!;
    private SecurityMonitor _securityMonitor = null!;
    private DatabaseMonitor _dbMonitor = null!;
    private AlertPolicyManager _alertPolicy = null!;
    private ReportGenerator _reportGen = null!;

    // ── New module metrics ────────────────────────────────
    private IisMetrics _iisMetrics = new();
    private NetworkMetrics _networkMetrics = new();
    private SecurityMetrics _securityMetrics = new();
    private List<DatabaseMetrics> _dbMetrics = new();
    private readonly List<ServerMetrics> _metricsHistory = new();


    // ── IIS controls ─────────────────────────────────────
    private Label lblIisReqSec = null!, lblIisConn = null!, lblIis4xx = null!, lblIis5xx = null!;
    private ListView lvAppPools = null!, lvSites = null!;
    private Panel pnlIisChart = null!;

    // ── Network controls ──────────────────────────────────
    private Label lblNetIn = null!, lblNetOut = null!, lblTcpConn = null!;
    private ListView lvPorts = null!, lvConnections = null!;
    private Panel pnlNetInChart = null!, pnlNetOutChart = null!;

    // ── Security controls ─────────────────────────────────
    private Label lblFailedLogins = null!, lblSuspProcs = null!, lblFileChanges = null!;
    private ListView lvSecEvents = null!, lvLogonSessions = null!, lvSuspProcs = null!;

    // ── Database controls ─────────────────────────────────
    private ListView lvDatabases = null!, lvSlowQueries = null!;

    private FileWatcherMonitor _fileWatcher = null!;
    private string _selectedWatchFile = "";
    private ListView lvWatchFiles = null!;
    private RichTextBox rtbFileLog = null!;
    private ComboBox cboFileLevel = null!;
    private TextBox txtFileSearch = null!;
    private Label lblFileStatus = null!;


    // ─────────────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────────────
    public MainForm()
    {
        InitializeComponent();
        SetupTheme();
        LoadSettings();
        InitCore();
        BuildUI();
        StartTimers();
    }

    private void InitializeComponent() { }

    private void SetupTheme()
    {
        Text = "Univue Guardian";
        Size = new Size(1000, 700);
        MinimumSize = new Size(860, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 245, 249);
        FormBorderStyle = FormBorderStyle.Sizable;
        Font = new Font("Segoe UI", 9);
    }

    private void LoadSettings()
    {
        try { _settings = AppSettings.Load(); }
        catch { _settings = new AppSettings(); }
    }

    private void InitCore()
    {
        try { _metrics = new SystemMetricsCollector(); } catch { _metrics = new SystemMetricsCollector(); }
        _svcMonitor    = new ServiceMonitor(_settings);
        _domainMonitor = new DomainMonitor(_settings);
        _emailSender   = new EmailSender(_settings);
        try { _eventLog = new EventLogReader(); } catch { _eventLog = new EventLogReader(); }

        _svcMonitor.ServiceStateChanged   += OnServiceStateChanged;
        _svcMonitor.AlertRaised           += OnAlertRaised;
        _domainMonitor.DomainStateChanged += OnDomainStateChanged;
        _domainMonitor.AlertRaised        += OnAlertRaised;
        _eventLog.NewEntryReceived        += OnNewEventLogEntry;

        try { _iisMonitor = new IisMonitor(); } catch { _iisMonitor = new IisMonitor(); }
        try { _networkMonitor = new NetworkMonitor(); } catch { _networkMonitor = new NetworkMonitor(); }
        _securityMonitor = new SecurityMonitor();
        _dbMonitor = new DatabaseMonitor();
        _alertPolicy = new AlertPolicyManager(_settings.AlertPolicies, _settings.MaintenanceWindows);
        _reportGen = new ReportGenerator();

        _iisMonitor.AlertRaised += OnAlertRaised;
        _networkMonitor.AlertRaised += OnAlertRaised;
        _securityMonitor.AlertRaised += OnAlertRaised;
        _dbMonitor.AlertRaised += OnAlertRaised;
        _alertPolicy.EscalatedAlert += OnAlertRaised;


        _fileWatcher = new FileWatcherMonitor();
        _fileWatcher.NewLineReceived += (filePath, line) =>
        {
            if (IsHandleCreated && _selectedWatchFile == filePath)
                BeginInvoke(() => AppendFileLog(FileWatcherMonitor.ParseLine(line)));
        };

        // Load saved files
        foreach (var f in _settings.WatchedFiles)
            try { _fileWatcher.AddFile(f); } catch { }


    }

    private void StartTimers()
    {
        _mainTimer = new System.Windows.Forms.Timer { Interval = _settings.CheckIntervalSeconds * 1000 };
        _mainTimer.Tick += async (s, e) => await DoMonitorCycleAsync();
        _mainTimer.Start();

        _uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _uiTimer.Tick += (s, e) => RefreshStatusBar();
        _uiTimer.Start();

        _ = DoMonitorCycleAsync();
    }

    // ─────────────────────────────────────────────────────
    //  BUILD UI
    // ─────────────────────────────────────────────────────
    private void BuildUI()
    {
        SuspendLayout();
        Controls.Clear();

        // ── Title Bar ─────────────────────────────────────
        var titleBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(10, 10, 26) };

        var lblTitle = new Label
        {
            Text = "Univue Guardian — SERVER-01",
            ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true, Location = new Point(14, 12)
        };

        var btnClose = new Button
        {
            Text = "✕", ForeColor = Color.White, BackColor = Color.FromArgb(239, 83, 80),
            FlatStyle = FlatStyle.Flat, Size = new Size(22, 22), Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();
        btnClose.Location = new Point(Width - 120, 11);

        var btnMin = new Button
        {
            Text = "—", ForeColor = Color.White, BackColor = Color.FromArgb(255, 202, 40),
            FlatStyle = FlatStyle.Flat, Size = new Size(22, 22), Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnMin.FlatAppearance.BorderSize = 0;
        btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;
        btnMin.Location = new Point(Width - 150, 11);

        titleBar.Controls.AddRange(new Control[] { lblTitle, btnClose, btnMin });
        this.Resize += (s, e) => { btnClose.Location = new Point(Width - 120, 11); btnMin.Location = new Point(Width - 150, 11); };

        // ── Nav Bar ───────────────────────────────────────
        var navBar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(17, 17, 40) };
        string[] tabNames = { "Dashboard", "Infrastructure", "Services", "Domains",
                      "Event Log", "Log Files", "Alerts", "IIS", "Network",
                      "Security", "Database", "Reports", "Settings" };
        _navButtons = new List<Label>();
        int nx = 12;

        for (int i = 0; i < tabNames.Length; i++)
        {
            var idx = i;
            var navBtn = new Label
            {
                Text = tabNames[i],
                ForeColor = i == 0 ? Color.White : Color.FromArgb(115, 115, 150),
                Font = new Font("Segoe UI", 9), AutoSize = false,
                Size = new Size(95, 34), Location = new Point(nx, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand, BackColor = Color.Transparent, Tag = i
            };
            navBtn.Paint += (s, e) =>
            {
                if ((int)((Label)s!).Tag == _activeTabIndex)
                {
                    using var pen = new Pen(Color.FromArgb(92, 107, 192), 2);
                    e.Graphics.DrawLine(pen, 0, 32, ((Label)s).Width, 32);
                }
            };
            navBtn.Click      += (s, e) => SwitchTab(idx);
            navBtn.MouseEnter += (s, e) => { if (idx != _activeTabIndex) ((Label)s!).ForeColor = Color.FromArgb(200, 200, 220); };
            navBtn.MouseLeave += (s, e) => { if (idx != _activeTabIndex) ((Label)s!).ForeColor = Color.FromArgb(115, 115, 150); };
            _navButtons.Add(navBtn);
            navBar.Controls.Add(navBtn);
            nx += 95;
        }

        // ── Status Bar ────────────────────────────────────
        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Color.FromArgb(13, 13, 32) };

        lblStatusMonitor  = MakeStatusLabel("● Monitor Active", Color.FromArgb(102, 187, 106));
        lblStatusSmtp     = MakeStatusLabel("● SMTP OK",        Color.FromArgb(102, 187, 106));
        lblStatusCritical = MakeStatusLabel("● 0 Critical",     Color.FromArgb(239, 83, 80));
        lblStatusWarn     = MakeStatusLabel("⚠ 0 Warnings",     Color.FromArgb(255, 202, 40));
        lblStatusRight    = MakeStatusLabel("",                  Color.FromArgb(80, 80, 110));

        lblStatusMonitor.Location  = new Point(10,  6);
        lblStatusSmtp.Location     = new Point(140, 6);
        lblStatusCritical.Location = new Point(240, 6);
        lblStatusWarn.Location     = new Point(340, 6);
        lblStatusRight.AutoSize    = true;
        lblStatusRight.Anchor      = AnchorStyles.Top | AnchorStyles.Right;
        statusBar.Resize += (s, e) => lblStatusRight.Location = new Point(statusBar.Width - lblStatusRight.Width - 10, 6);
        statusBar.Controls.AddRange(new Control[] { lblStatusMonitor, lblStatusSmtp, lblStatusCritical, lblStatusWarn, lblStatusRight });

        // ── Body ──────────────────────────────────────────
        var body = new Panel { Dock = DockStyle.Fill };

        // SIDEBAR
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 170, BackColor = Color.FromArgb(13, 13, 32) };
        sidebar.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(15, 255, 255, 255), 1);
            e.Graphics.DrawLine(pen, 47, 0, 47, sidebar.Height);
        };

        string[] icons = { "⊞", "📊", "⚙", "🌐", "📋", "📄", "🔔", "🖥", "🔌", "🔒", "🗄", "📑", "☰" };

        string[] tips = { "Dashboard", "Infrastructure", "Services", "Domains",
                   "Event Log", "Log Files", "Alerts", "IIS Monitor",
                   "Network", "Security", "Database", "Reports", "Settings" };
        _sideIcons = new List<Panel>();

        for (int i = 0; i < icons.Length; i++)
        {
            var idx = i;
            var iconPnl = new Panel
            {
                Size = new Size(154, 36),
                Location = new Point(8, 8 + i * 40),
                BackColor = i == 0 ? Color.FromArgb(40, 92, 107, 192) : Color.Transparent,
                Cursor = Cursors.Hand,
                Tag = i
            };

            var iconLbl = new Label
            {
                Text = icons[i],
                Size = new Size(28, 36),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12),
                ForeColor = i == 0 ? Color.FromArgb(121, 134, 203) : Color.FromArgb(90, 90, 120),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var textLbl = new Label
            {
                Text = tips[i],
                Size = new Size(120, 36),
                Location = new Point(32, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = i == 0 ? Color.White : Color.FromArgb(90, 90, 120),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            iconPnl.Controls.Add(iconLbl);
            iconPnl.Controls.Add(textLbl);
            iconPnl.Click += (s, e) => SwitchTab(idx);
            iconLbl.Click += (s, e) => SwitchTab(idx);
            textLbl.Click += (s, e) => SwitchTab(idx);

            _sideIcons.Add(iconPnl);
            sidebar.Controls.Add(iconPnl);
        }

        sidebar.Resize += (s, e) =>
        {
            if (_sideIcons.Count >= 13)
                _sideIcons[12].Location = new Point(8, sidebar.Height - 46);
        };

        // CONTENT
        _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(244, 245, 249) };

        _pages = new List<Panel>
        {
           BuildDashboardPage(),
            BuildInfraPage(),
            BuildServicesPage(),
            BuildDomainsPage(),
            BuildEventLogPage(),
            BuildLogFilesPage(),
            BuildAlertsPage(),
            BuildIisPage(),
            BuildNetworkPage(),
            BuildSecurityPage(),
            BuildDatabasePage(),
            BuildReportsPage(),
            BuildSettingsPage()
        };

        foreach (var page in _pages)
        {
            page.Dock    = DockStyle.Fill;
            page.Visible = false;
            _contentPanel.Controls.Add(page);
        }
        _pages[0].Visible = true;

        body.Controls.Add(_contentPanel);
        body.Controls.Add(sidebar);

        Controls.Add(body);
        Controls.Add(navBar);
        Controls.Add(titleBar);
        Controls.Add(statusBar);

        ResumeLayout();
    }

    // ─────────────────────────────────────────────────────
    //  TAB SWITCHING
    // ─────────────────────────────────────────────────────
    private void SwitchTab(int index)
    {
        _activeTabIndex = index;
        for (int i = 0; i < _pages.Count; i++) _pages[i].Visible = (i == index);
        for (int i = 0; i < _navButtons.Count; i++) { _navButtons[i].ForeColor = i == index ? Color.White : Color.FromArgb(115, 115, 150); _navButtons[i].Invalidate(); }
        for (int i = 0; i < _sideIcons.Count; i++)
        {
            bool active = (i == index);
            _sideIcons[i].BackColor = active ? Color.FromArgb(40, 92, 107, 192) : Color.Transparent;
            // Icon label
            _sideIcons[i].Controls[0].ForeColor = active ? Color.FromArgb(121, 134, 203) : Color.FromArgb(90, 90, 120);
            // Text label
            _sideIcons[i].Controls[1].ForeColor = active ? Color.White : Color.FromArgb(90, 90, 120);
        }
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Dashboard
    // ─────────────────────────────────────────────────────
    private Panel BuildDashboardPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true, Padding = new Padding(10) };
        int y = 10;

        // Row 1 — metric cards
        var row1 = MakeGridRow(page, y, 4, 90);
        (var c1, lblHealthScore, pbHealth) = MakeMetricCard("Health Score", "—%", Color.FromArgb(46, 125, 50),  Color.FromArgb(67, 160, 71));
        (var c2, lblCpu,         pbCpu)    = MakeMetricCard("CPU Load",     "—%", Color.FromArgb(21, 101, 192), Color.FromArgb(30, 136, 229));
        (var c3, lblRam,         pbRam)    = MakeMetricCard("RAM",          "—%", Color.FromArgb(230, 81, 0),   Color.FromArgb(251, 140, 0));
        (var c4, lblDisk,        pbDisk)   = MakeMetricCard("Disk C:",      "—%", Color.FromArgb(46, 125, 50),  Color.FromArgb(67, 160, 71));
        row1.Controls.Add(c1, 0, 0); row1.Controls.Add(c2, 1, 0); row1.Controls.Add(c3, 2, 0); row1.Controls.Add(c4, 3, 0);
        y += 100;

        // Row 2 — summary cards
        var row2 = MakeGridRow(page, y, 4, 70);
        var cs1 = MakeSimpleCard("Services", "—"); var cs2 = MakeSimpleCard("Domains", "—");
        var cs3 = MakeSimpleCard("SSL Certs","—"); var cs4 = MakeSimpleCard("Uptime",  "—");
        lblUptime = (Label)cs4.Controls[1];
        row2.Controls.Add(cs1, 0, 0); row2.Controls.Add(cs2, 1, 0); row2.Controls.Add(cs3, 2, 0); row2.Controls.Add(cs4, 3, 0);
        y += 80;

        // Alerts + RAM side by side
        var pnlAlerts = MakeSectionPanel("Recent Alerts", out var alertsBody);
        pnlAlerts.Location = new Point(10, y); pnlAlerts.Size = new Size(430, 195);
        pnlAlerts.Anchor   = AnchorStyles.Top | AnchorStyles.Left;
        lvAlertsDash = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.None };
        lvAlertsDash.Columns.Add("Time", 55); lvAlertsDash.Columns.Add("Source", 120); lvAlertsDash.Columns.Add("Message", 240);
        alertsBody.Controls.Add(lvAlertsDash);

        var pnlRamTop = MakeSectionPanel("Top RAM Consumers", out var ramBody);
        pnlRamTop.Location = new Point(450, y); pnlRamTop.Size = new Size(400, 195);
        pnlRamTop.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvRamProcs = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.None };
        lvRamProcs.Columns.Add("Process", 150); lvRamProcs.Columns.Add("RAM (MB)", 100); lvRamProcs.Columns.Add("CPU %", 80);
        ramBody.Controls.Add(lvRamProcs);
        y += 205;

        // Charts
        var cpuSec = MakeSectionPanel("CPU — Live (5 min)", out var cpuBody);
        cpuSec.Location = new Point(10, y); cpuSec.Size = new Size(430, 120);
        pnlCpuChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlCpuChart.Paint += (s, e) => DrawLineChart(e.Graphics, pnlCpuChart, _cpuChartData, Color.FromArgb(30, 136, 229), 100);
        cpuBody.Controls.Add(pnlCpuChart);

        var ramSec = MakeSectionPanel("RAM — Live (5 min)", out var ramChartBody);
        ramSec.Location = new Point(450, y); ramSec.Size = new Size(400, 120);
        ramSec.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlRamChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlRamChart.Paint += (s, e) => DrawLineChart(e.Graphics, pnlRamChart, _ramChartData, Color.FromArgb(251, 140, 0), 100);
        ramChartBody.Controls.Add(pnlRamChart);

        page.Controls.Add(pnlAlerts); page.Controls.Add(pnlRamTop);
        page.Controls.Add(cpuSec);    page.Controls.Add(ramSec);
        return page;
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Infrastructure
    // ─────────────────────────────────────────────────────
    private Panel BuildInfraPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };
        int y = 10;

        var row1 = MakeGridRow(page, y, 4, 80);
        (var c1, lblInfraCpu,  _) = MakeMetricCard("CPU Total",  "—%",     Color.FromArgb(21, 101, 192), Color.FromArgb(30, 136, 229));
        (var c2, lblInfraRam,  _) = MakeMetricCard("RAM Used",   "— GB",   Color.FromArgb(230, 81, 0),   Color.FromArgb(251, 140, 0));
        (var c3, lblInfraNet,  _) = MakeMetricCard("Network In", "— Mbps", Color.FromArgb(0, 105, 92),   Color.FromArgb(0, 137, 123));
        (var c4, lblInfraDisk, _) = MakeMetricCard("Disk I/O",   "— MB/s", Color.FromArgb(74, 20, 140),  Color.FromArgb(123, 31, 162));
        row1.Controls.Add(c1, 0, 0); row1.Controls.Add(c2, 1, 0); row1.Controls.Add(c3, 2, 0); row1.Controls.Add(c4, 3, 0);
        y += 90;

        var cpuSec = MakeSectionPanel("CPU — Live Chart", out var cpuBody);
        cpuSec.Location = new Point(10, y); cpuSec.Size = new Size(430, 130);
        pnlInfraCpuChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlInfraCpuChart.Paint += (s, e) => DrawLineChart(e.Graphics, pnlInfraCpuChart, _cpuChartData, Color.FromArgb(30, 136, 229), 100);
        cpuBody.Controls.Add(pnlInfraCpuChart);

        var ramSec = MakeSectionPanel("RAM — Live Chart", out var ramBody);
        ramSec.Location = new Point(450, y); ramSec.Size = new Size(400, 130);
        ramSec.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlInfraRamChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlInfraRamChart.Paint += (s, e) => DrawLineChart(e.Graphics, pnlInfraRamChart, _ramChartData, Color.FromArgb(251, 140, 0), 100);
        ramBody.Controls.Add(pnlInfraRamChart);
        y += 140;

        var procSec = MakeSectionPanel("Top Processes", out var procBody);
        procSec.Location = new Point(10, y); procSec.Size = new Size(840, 220);
        procSec.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        lvProcesses = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvProcesses.Columns.Add("Process", 160); lvProcesses.Columns.Add("PID", 70);
        lvProcesses.Columns.Add("CPU %", 80);    lvProcesses.Columns.Add("RAM (MB)", 90); lvProcesses.Columns.Add("Status", 80);

        var btnKill = new Button { Text = "Kill Selected Process", Dock = DockStyle.Bottom, Height = 28, BackColor = Color.FromArgb(255, 235, 238), ForeColor = Color.FromArgb(198, 40, 40), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnKill.FlatAppearance.BorderColor = Color.FromArgb(239, 154, 154);
        btnKill.Click += BtnKillProcess_Click;
        procBody.Controls.Add(lvProcesses); procBody.Controls.Add(btnKill);

        page.Controls.Add(cpuSec); page.Controls.Add(ramSec); page.Controls.Add(procSec);
        return page;
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Services
    // ─────────────────────────────────────────────────────
    private Panel BuildServicesPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249) };

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(244, 245, 249) };
        var btnAdd = new Button { Text = "+ Add Service", Location = new Point(10, 7), Size = new Size(110, 24), BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnAdd.FlatAppearance.BorderSize = 0;
        btnAdd.Click += BtnAddService_Click;

        lblSvcRunning = new Label { Text = "0 Running", Location = new Point(130, 11), AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(46, 125, 50) };
        lblSvcStopped = new Label { Text = "0 Stopped", Location = new Point(230, 11), AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(198, 40, 40) };
        lblSvcWarn    = new Label { Text = "0 Warning",  Location = new Point(330, 11), AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(230, 81, 0) };
        lblLastCheck  = new Label { Text = "Last check: —", Location = new Point(430, 11), AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = Color.Silver };
        toolbar.Controls.AddRange(new Control[] { btnAdd, lblSvcRunning, lblSvcStopped, lblSvcWarn, lblLastCheck });

        var svcSec = MakeSectionPanel("Monitored Services", out var svcBody);
        svcSec.Dock = DockStyle.Fill;

        lvServices = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvServices.Columns.Add("Service Name", 160); lvServices.Columns.Add("Display Name", 160);
        lvServices.Columns.Add("Status", 90);        lvServices.Columns.Add("CPU %", 70);
        lvServices.Columns.Add("RAM (MB)", 80);      lvServices.Columns.Add("Auto-Restart", 90);
        lvServices.MouseClick += LvServices_MouseClick;
        svcBody.Controls.Add(lvServices);

        page.Controls.Add(svcSec); page.Controls.Add(toolbar);
        return page;
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Domains
    // ─────────────────────────────────────────────────────
    private Panel BuildDomainsPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249) };

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(244, 245, 249) };
        var btnAdd = new Button { Text = "+ Add Domain", Location = new Point(10, 7), Size = new Size(110, 24), BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnAdd.FlatAppearance.BorderSize = 0;
        btnAdd.Click += BtnAddDomain_Click;
        toolbar.Controls.Add(btnAdd);

        var domSec = MakeSectionPanel("Monitored Domains", out var domBody);
        domSec.Dock = DockStyle.Fill;

        lvDomains = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvDomains.Columns.Add("Domain", 150); lvDomains.Columns.Add("URL", 200);
        lvDomains.Columns.Add("Status", 90);  lvDomains.Columns.Add("Response", 80);
        lvDomains.Columns.Add("Uptime 24h", 85); lvDomains.Columns.Add("SSL Expiry", 90);
        domBody.Controls.Add(lvDomains);

        page.Controls.Add(domSec); page.Controls.Add(toolbar);
        return page;
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Event Log
    // ─────────────────────────────────────────────────────
    private Panel BuildEventLogPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249) };

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(244, 245, 249) };

        cboLogSource = new ComboBox { Location = new Point(10, 8), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9) };
        cboLogSource.Items.AddRange(new[] { "All Sources", "System", "Application", "Univue Guardian" });
        cboLogSource.SelectedIndex = 0;

        cboLogLevel = new ComboBox { Location = new Point(150, 8), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9) };
        cboLogLevel.Items.AddRange(new[] { "All Levels", "ERROR", "WARN", "INFO" });
        cboLogLevel.SelectedIndex = 0;

        txtLogSearch = new TextBox { Location = new Point(270, 8), Width = 200, Font = new Font("Segoe UI", 9), PlaceholderText = "Search events..." };

        var btnRefresh = new Button { Text = "Refresh", Location = new Point(480, 7), Size = new Size(70, 24), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnRefresh.Click += (s, e) => RefreshEventLogPage();

        var btnExport = new Button { Text = "Export CSV", Location = new Point(560, 7), Size = new Size(85, 24), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
        btnExport.Click += BtnExportLog_Click;

        cboLogSource.SelectedIndexChanged += (s, e) => RefreshEventLogPage();
        cboLogLevel.SelectedIndexChanged  += (s, e) => RefreshEventLogPage();
        txtLogSearch.TextChanged          += (s, e) => RefreshEventLogPage();

        toolbar.Controls.AddRange(new Control[] { cboLogSource, cboLogLevel, txtLogSearch, btnRefresh, btnExport });

        var logSec = MakeSectionPanel("Windows Event Log — Live Tail", out var logBody);
        logSec.Dock = DockStyle.Fill;

        rtbLog = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(13, 17, 23), ForeColor = Color.FromArgb(205, 217, 229), Font = new Font("Consolas", 9), ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical };
        logBody.Controls.Add(rtbLog);

        page.Controls.Add(logSec); page.Controls.Add(toolbar);
        return page;
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Alerts
    // ─────────────────────────────────────────────────────
    private Panel BuildAlertsPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };
        int y = 10;

        var row = MakeGridRow(page, y, 4, 80);
        (var ca, lblAlertTotal,    _) = MakeMetricCard("Today Total", "0", Color.FromArgb(21, 101, 192), Color.FromArgb(30, 136, 229));
        (var cb, lblAlertCrit,     _) = MakeMetricCard("Critical",    "0", Color.FromArgb(198, 40, 40),  Color.FromArgb(239, 83, 80));
        (var cc, lblAlertWarn,     _) = MakeMetricCard("Warnings",    "0", Color.FromArgb(230, 81, 0),   Color.FromArgb(251, 140, 0));
        (var cd, lblAlertResolved, _) = MakeMetricCard("Resolved",    "0", Color.FromArgb(46, 125, 50),  Color.FromArgb(67, 160, 71));
        row.Controls.Add(ca, 0, 0); row.Controls.Add(cb, 1, 0); row.Controls.Add(cc, 2, 0); row.Controls.Add(cd, 3, 0);
        y += 90;

        var alertSec = MakeSectionPanel("Alert History", out var alertBody);
        alertSec.Location = new Point(10, y); alertSec.Size = new Size(860, 400);
        alertSec.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        lvAlerts = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvAlerts.Columns.Add("Time", 60); lvAlerts.Columns.Add("Severity", 80); lvAlerts.Columns.Add("Source", 130);
        lvAlerts.Columns.Add("Message", 370); lvAlerts.Columns.Add("Email", 60); lvAlerts.Columns.Add("Status", 70);
        alertBody.Controls.Add(lvAlerts);

        page.Controls.Add(alertSec);
        return page;
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Settings
    // ─────────────────────────────────────────────────────
    private Panel BuildSettingsPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };

        var smtpSec = MakeSectionPanel("Email / SMTP", out var smtpBody);
        smtpSec.Location = new Point(10, 10); smtpSec.Size = new Size(420, 265);

        var txtHost = MakeFormRow(smtpBody, 10, "SMTP Host", _settings.SmtpHost);
        var txtPort = MakeFormRow(smtpBody, 48, "Port", _settings.SmtpPort.ToString());
        var txtFrom = MakeFormRow(smtpBody, 86, "From Email", _settings.SmtpFrom);
        var txtPass = MakeFormRow(smtpBody, 124, "App Password", "", isPassword: true);
        var txtTo = MakeFormRow(smtpBody, 162, "To (semicolon sep.)", _settings.SmtpTo);

        var btnTest = new Button { Text = "Send Test Email", Location = new Point(140, 208), Size = new Size(130, 28), BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnTest.FlatAppearance.BorderSize = 0;
        btnTest.Click += async (s, e) =>
        {
            SaveSmtp(txtHost, txtPort, txtFrom, txtPass, txtTo);
            bool ok = await _emailSender.SendTestEmailAsync();
            MessageBox.Show(ok ? "Test email sent!" : "Failed. Check SMTP settings.", "Email Test", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        };
        smtpBody.Controls.Add(btnTest);

        var rulesSec = MakeSectionPanel("Alert Rules", out var rulesBody);
        rulesSec.Location = new Point(440, 10); rulesSec.Size = new Size(360, 265);

        var togSvcDown = MakeToggleRow(rulesBody, 10, "Service down → email", _settings.AlertServiceDown);
        var togSvcRec = MakeToggleRow(rulesBody, 48, "Service recovered → email", _settings.AlertServiceRecovered);
        var togRestart = MakeToggleRow(rulesBody, 86, "Restart failed → email", _settings.AlertRestartFailed);
        var togDomDown = MakeToggleRow(rulesBody, 124, "Domain down → email", _settings.AlertDomainDown);
        var togSsl = MakeToggleRow(rulesBody, 162, "SSL expiry → email", _settings.AlertSslExpiry);
        var togCpu = MakeToggleRow(rulesBody, 200, "High CPU → email", _settings.AlertHighCpu);
        var togDaily = MakeToggleRow(rulesBody, 238, "Daily report (8:00 AM)", _settings.AlertDailyReport);

        var thrSec = MakeSectionPanel("Monitor Thresholds", out var thrBody);
        thrSec.Location = new Point(10, 285); thrSec.Size = new Size(790, 110);
        thrSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        for (int i = 0; i < 3; i++) tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        thrBody.Controls.Add(tlp);

        var txtInterval = MakeInlineField(tlp, "Check interval (sec)", _settings.CheckIntervalSeconds.ToString(), 0, 0);
        var txtCpuAlert = MakeInlineField(tlp, "CPU alert %", _settings.CpuAlertPercent.ToString(), 1, 0);
        var txtMaxRst = MakeInlineField(tlp, "Max restarts", _settings.MaxRestarts.ToString(), 2, 0);
        var txtRamAlert = MakeInlineField(tlp, "RAM alert (MB)", _settings.RamAlertMb.ToString(), 0, 1);
        var txtDomSlow = MakeInlineField(tlp, "Domain slow (ms)", _settings.DomainSlowMs.ToString(), 1, 1);
        var txtSslWarn = MakeInlineField(tlp, "SSL warn (days)", _settings.SslWarnDays.ToString(), 2, 1);

        var btnSave = new Button { Text = "Save All Settings", Location = new Point(10, 405), Size = new Size(160, 32), BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (s, e) =>
        {
            SaveSmtp(txtHost, txtPort, txtFrom, txtPass, txtTo);
            _settings.AlertServiceDown = togSvcDown.Checked;
            _settings.AlertServiceRecovered = togSvcRec.Checked;
            _settings.AlertRestartFailed = togRestart.Checked;
            _settings.AlertDomainDown = togDomDown.Checked;
            _settings.AlertSslExpiry = togSsl.Checked;
            _settings.AlertHighCpu = togCpu.Checked;
            _settings.AlertDailyReport = togDaily.Checked;
            if (int.TryParse(txtInterval.Text, out int iv)) _settings.CheckIntervalSeconds = iv;
            if (double.TryParse(txtCpuAlert.Text, out double cpu)) _settings.CpuAlertPercent = cpu;
            if (int.TryParse(txtMaxRst.Text, out int mr)) _settings.MaxRestarts = mr;
            if (long.TryParse(txtRamAlert.Text, out long ram)) _settings.RamAlertMb = ram;
            if (int.TryParse(txtDomSlow.Text, out int ds)) _settings.DomainSlowMs = ds;
            if (int.TryParse(txtSslWarn.Text, out int sw)) _settings.SslWarnDays = sw;
            _settings.Save();
            _mainTimer.Interval = _settings.CheckIntervalSeconds * 1000;
            MessageBox.Show("Settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        page.Controls.Add(smtpSec); page.Controls.Add(rulesSec);
        page.Controls.Add(thrSec); page.Controls.Add(btnSave);
        return page;

    }
    // ─────────────────────────────────────────────────────
    //  MONITOR CYCLE
    // ─────────────────────────────────────────────────────
    private async Task DoMonitorCycleAsync()
    {
        _nextCheckCountdown = _settings.CheckIntervalSeconds;

        await Task.Run(() =>
        {
            try { _currentMetrics = _metrics.Collect(); } catch { }
            try { _cpuChartData = _metrics.GetCpuHistory(); } catch { }
            try { _ramChartData = _metrics.GetRamHistory(); } catch { }
            try { _svcMonitor.CheckAll(); } catch { }
            try { _eventLog.Poll(); } catch { }
            try { _iisMetrics = _iisMonitor.Collect(); } catch { }
            try { _networkMetrics = _networkMonitor.Collect(_settings.Ports); } catch { }
            try { _securityMetrics = _securityMonitor.Collect(_settings.WatchedFiles); } catch { }

            if (_currentMetrics != null)
            {
                if (_metricsHistory.Count >= 1008) _metricsHistory.RemoveAt(0);
                _metricsHistory.Add(_currentMetrics);
            }
        });

        try { await _domainMonitor.CheckAllAsync(); } catch { }
        try { _dbMetrics = await _dbMonitor.CollectAllAsync(_settings.Databases); } catch { }

        CheckDailyReport();

        if (IsHandleCreated)
        {
            if (InvokeRequired) Invoke(RefreshAllPages);
            else RefreshAllPages();
        }
    }

    // ─────────────────────────────────────────────────────
    //  REFRESH
    // ─────────────────────────────────────────────────────
    private void RefreshAllPages()
    {
        RefreshDashboard(); RefreshInfra(); RefreshServicesPage();
        RefreshDomainsPage(); RefreshAlertsPage();
        if (_activeTabIndex == 4) RefreshEventLogPage();
        if (_activeTabIndex == 7) RefreshIisPage();
        if (_activeTabIndex == 8) RefreshNetworkPage();
        if (_activeTabIndex == 9) RefreshSecurityPage();
        if (_activeTabIndex == 10) RefreshDatabasePage();
        pnlCpuChart?.Invalidate(); pnlRamChart?.Invalidate();
        pnlInfraCpuChart?.Invalidate(); pnlInfraRamChart?.Invalidate();
    }

    private void RefreshStatusBar()
    {
        _nextCheckCountdown = Math.Max(0, _nextCheckCountdown - 1);
        var up = DateTime.Now - _startTime;
        if (lblStatusRight    != null) lblStatusRight.Text    = $"Next check: {_nextCheckCountdown}s  |  Uptime: {up.Days}d {up.Hours}h {up.Minutes}m  |  Univue Guardian v2.0";
        if (lblStatusCritical != null) lblStatusCritical.Text = $"● {_alerts.Count(a => a.Severity == AlertSeverity.Critical && !a.Resolved)} Critical";
        if (lblStatusWarn     != null) lblStatusWarn.Text     = $"⚠ {_alerts.Count(a => a.Severity == AlertSeverity.Warning  && !a.Resolved)} Warnings";
    }

    private void RefreshDashboard()
    {
        if (lblHealthScore == null || _currentMetrics == null) return;
        lblHealthScore.Text = $"{_currentMetrics.HealthScore}%";
        lblCpu.Text  = $"{_currentMetrics.CpuPercent}%";
        lblRam.Text  = $"{_currentMetrics.RamPercent:F0}%";
        pbHealth.Value = Clamp(_currentMetrics.HealthScore);
        pbCpu.Value    = Clamp((int)_currentMetrics.CpuPercent);
        pbRam.Value    = Clamp((int)_currentMetrics.RamPercent);
        if (_currentMetrics.Disks.TryGetValue(@"C:\", out var d)) { lblDisk.Text = $"{d.UsedPercent:F0}%"; pbDisk.Value = Clamp((int)d.UsedPercent); }
        lblUptime.Text = $"{_currentMetrics.Uptime.Days}d {_currentMetrics.Uptime.Hours}h {_currentMetrics.Uptime.Minutes}m";

        lvAlertsDash.Items.Clear();
        foreach (var a in _alerts.Take(6))
        {
            var item = new ListViewItem(a.TimeDisplay);
            item.SubItems.Add(a.Source); item.SubItems.Add(a.Message);
            item.ForeColor = a.Severity == AlertSeverity.Critical ? Color.FromArgb(198, 40, 40) : a.Severity == AlertSeverity.Warning ? Color.FromArgb(230, 81, 0) : Color.FromArgb(21, 101, 192);
            lvAlertsDash.Items.Add(item);
        }
        lvRamProcs.Items.Clear();
        foreach (var p in _metrics.GetTopProcesses(5))
        {
            var item = new ListViewItem(p.Name);
            item.SubItems.Add(p.MemoryMb.ToString()); item.SubItems.Add(p.CpuPercent.ToString("F1"));
            lvRamProcs.Items.Add(item);
        }
    }

    private void RefreshInfra()
    {
        if (lblInfraCpu == null || _currentMetrics == null) return;
        lblInfraCpu.Text  = $"{_currentMetrics.CpuPercent}%";
        lblInfraRam.Text  = $"{_currentMetrics.RamUsedGb} GB";
        lblInfraNet.Text  = $"{_currentMetrics.NetworkInMbps} Mbps";
        lblInfraDisk.Text = $"{_currentMetrics.DiskIoMbps} MB/s";

        lvProcesses.Items.Clear();
        foreach (var p in _metrics.GetTopProcesses(15))
        {
            var item = new ListViewItem(p.Name);
            item.SubItems.Add(p.Pid.ToString()); item.SubItems.Add(p.CpuPercent.ToString("F1"));
            item.SubItems.Add(p.MemoryMb.ToString()); item.SubItems.Add(p.MemoryMb > 400 ? "High RAM" : "Normal");
            if (p.MemoryMb > 400) item.ForeColor = Color.FromArgb(230, 81, 0);
            lvProcesses.Items.Add(item);
        }
    }

    private void RefreshServicesPage()
    {
        if (lvServices == null) return;
        int running = 0, stopped = 0, warn = 0;
        lvServices.Items.Clear();
        foreach (var svc in _settings.Services)
        {
            var item = new ListViewItem(svc.ServiceName);
            item.SubItems.Add(svc.DisplayName); item.SubItems.Add(svc.Status.ToString());
            item.SubItems.Add(svc.CpuPercent.ToString("F1")); item.SubItems.Add(svc.MemoryMb.ToString());
            item.SubItems.Add(svc.AutoRestart ? "Yes" : "No");
            item.Tag = svc;
            switch (svc.Status)
            {
                case ServiceStatus.Running: item.ForeColor = Color.FromArgb(46, 125, 50);  running++; break;
                case ServiceStatus.Stopped: item.ForeColor = Color.FromArgb(198, 40, 40); stopped++; item.BackColor = Color.FromArgb(255, 248, 248); break;
                case ServiceStatus.Warning: item.ForeColor = Color.FromArgb(230, 81, 0);  warn++;    item.BackColor = Color.FromArgb(255, 251, 242); break;
            }
            lvServices.Items.Add(item);
        }
        if (lblSvcRunning != null) lblSvcRunning.Text = $"{running} Running";
        if (lblSvcStopped != null) lblSvcStopped.Text = $"{stopped} Stopped";
        if (lblSvcWarn    != null) lblSvcWarn.Text    = $"{warn} Warning";
        if (lblLastCheck  != null) lblLastCheck.Text  = $"Last check: {DateTime.Now:HH:mm:ss}";
    }

    private bool _domainClickAttached = false;

    private void RefreshDomainsPage()
    {
        if (lvDomains == null) return;
        lvDomains.Items.Clear();

        foreach (var d in _settings.Domains)
        {
            var item = new ListViewItem(d.DisplayName);
            item.SubItems.Add(d.Url);
            item.SubItems.Add(d.Status.ToString());
            item.SubItems.Add($"{d.ResponseMs} ms");
            item.SubItems.Add($"{d.Uptime24h}%");
            item.SubItems.Add(d.SslDaysRemaining >= 0 ? $"{d.SslDaysRemaining}d" : "—");
            item.ForeColor = d.Status == DomainStatus.Online ? Color.FromArgb(46, 125, 50)
                           : d.Status == DomainStatus.Down ? Color.FromArgb(198, 40, 40)
                           : Color.FromArgb(230, 81, 0);
            item.Tag = d; 
            lvDomains.Items.Add(item);
        }

        if (!_domainClickAttached)
        {
            _domainClickAttached = true;
            lvDomains.MouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right || lvDomains.SelectedItems.Count == 0) return;
                var domain = (MonitoredDomain)lvDomains.SelectedItems[0].Tag;

                var ctx = new ContextMenuStrip();
                ctx.Items.Add("🔍 Check Now", null, async (s, ev) =>
                {
                    await _domainMonitor.CheckDomainAsync(domain);
                    RefreshDomainsPage();
                });
                ctx.Items.Add(new ToolStripSeparator());
                ctx.Items.Add("✕ Remove", null, (s, ev) =>
                {
                    _settings.Domains.Remove(domain);
                    _settings.Save();
                    RefreshDomainsPage();
                });
                ctx.Show(lvDomains, e.Location);
            };
        }
    }

    private void RefreshAlertsPage()
    {
        if (lblAlertTotal == null) return;
        var today = _alerts.Where(a => a.Time.Date == DateTime.Today).ToList();
        lblAlertTotal.Text    = today.Count.ToString();
        lblAlertCrit.Text     = today.Count(a => a.Severity == AlertSeverity.Critical).ToString();
        lblAlertWarn.Text     = today.Count(a => a.Severity == AlertSeverity.Warning).ToString();
        lblAlertResolved.Text = today.Count(a => a.Resolved).ToString();

        lvAlerts.Items.Clear();
        foreach (var a in _alerts.Take(200))
        {
            var item = new ListViewItem(a.TimeDisplay);
            item.SubItems.Add(a.SeverityDisplay); item.SubItems.Add(a.Source);
            item.SubItems.Add(a.Message); item.SubItems.Add(a.EmailSent ? "✓ Yes" : "No");
            item.SubItems.Add(a.Resolved ? "Resolved" : "Active");
            item.ForeColor = a.Severity == AlertSeverity.Critical ? Color.FromArgb(198, 40, 40) : a.Severity == AlertSeverity.Warning ? Color.FromArgb(230, 81, 0) : Color.FromArgb(21, 101, 192);
            lvAlerts.Items.Add(item);
        }
    }

    private void RefreshEventLogPage()
    {
        if (rtbLog == null) return;
        string src    = cboLogSource.SelectedItem?.ToString() ?? "All";
        string level  = cboLogLevel.SelectedItem?.ToString()  ?? "All";
        var entries   = _eventLog.GetFiltered(src == "All Sources" ? "All" : src, level == "All Levels" ? "All" : level, txtLogSearch.Text);

        rtbLog.Clear(); rtbLog.SuspendLayout();
        foreach (var e in entries)
        {
            AppendLog(e.Time.ToString("HH:mm:ss"), Color.FromArgb(72, 79, 88));
            AppendLog($" [{e.Level}]", e.Level == "ERROR" ? Color.FromArgb(248, 81, 73) : e.Level == "WARN" ? Color.FromArgb(210, 153, 34) : Color.FromArgb(63, 185, 80));
            AppendLog($" {e.Source}: {e.Message}\n", Color.FromArgb(205, 217, 229));
        }
        rtbLog.ResumeLayout();
    }

    private void AppendLog(string text, Color color)
    {
        rtbLog.SelectionStart = rtbLog.TextLength; rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor = color; rtbLog.AppendText(text);
        rtbLog.SelectionColor = rtbLog.ForeColor;
    }

    // ─────────────────────────────────────────────────────
    //  EVENT HANDLERS
    // ─────────────────────────────────────────────────────
    private void OnServiceStateChanged(MonitoredService svc, ServiceStatus status, string detail)
        => _eventLog.AddGuardianEntry(status == ServiceStatus.Running ? "INFO" : status == ServiceStatus.Stopped ? "ERROR" : "WARN", $"Service {svc.ServiceName}: {status} — {detail}");

    private void OnAlertRaised(string source, AlertSeverity severity, string message)
    {
        var alert = new AlertEntry { Time = DateTime.Now, Severity = severity, Source = source, Message = message };
        _alerts.Insert(0, alert);
        _ = Task.Run(async () => { alert.EmailSent = await _emailSender.SendAlertAsync(alert); });
        _eventLog.AddGuardianEntry(severity == AlertSeverity.Critical ? "ERROR" : "WARN", $"ALERT: {source} — {message}");
        if (IsHandleCreated) BeginInvoke(RefreshAlertsPage);
    }

    private void OnDomainStateChanged(MonitoredDomain domain, DomainStatus status)
        => _eventLog.AddGuardianEntry(status == DomainStatus.Down ? "ERROR" : "WARN", $"Domain {domain.DisplayName}: {status}");

    private void OnNewEventLogEntry(Models.EventLogEntry entry)
    {
        if (IsHandleCreated && _activeTabIndex == 4) BeginInvoke(RefreshEventLogPage);
    }

    private void CheckDailyReport()
    {
        if (!_settings.AlertDailyReport) return;
        var now = DateTime.Now;
        if (now >= now.Date.Add(_settings.DailyReportTime) && _lastDailyReport.Date < now.Date)
        {
            _lastDailyReport = now;
            _ = Task.Run(() => _emailSender.SendDailyReportAsync(_currentMetrics, _settings.Services, _settings.Domains, _alerts.Where(a => a.Time.Date == now.Date)));
        }
    }

    private void BtnKillProcess_Click(object? sender, EventArgs e)
    {
        if (lvProcesses.SelectedItems.Count == 0) return;
        var item = lvProcesses.SelectedItems[0];
        if (!int.TryParse(item.SubItems[1].Text, out int pid)) return;
        if (MessageBox.Show($"Kill '{item.Text}' (PID {pid})?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            try { Process.GetProcessById(pid).Kill(); _eventLog.AddGuardianEntry("INFO", $"Process {item.Text} (PID {pid}) killed."); }
            catch (Exception ex) { MessageBox.Show($"Could not kill: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    private void BtnAddService_Click(object? sender, EventArgs e)
    {
        using var dlg = new AddServiceForm();
        if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
        { _settings.Services.Add(dlg.Result); _settings.Save(); RefreshServicesPage(); }
    }

    private void BtnAddDomain_Click(object? sender, EventArgs e)
    {
        using var dlg = new AddDomainForm();
        if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
        { _settings.Domains.Add(dlg.Result); _settings.Save(); RefreshDomainsPage(); }
    }

    private void LvServices_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || lvServices.SelectedItems.Count == 0) return;
        var svc = (MonitoredService)lvServices.SelectedItems[0].Tag;
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("▶ Start",   null, (s, ev) => { _svcMonitor.StartService(svc.ServiceName);      _ = DoMonitorCycleAsync(); });
        ctx.Items.Add("■ Stop",    null, (s, ev) => { _svcMonitor.StopService(svc.ServiceName);       _ = DoMonitorCycleAsync(); });
        ctx.Items.Add("↺ Restart", null, (s, ev) => { _svcMonitor.TryRestartService(svc.ServiceName); _ = DoMonitorCycleAsync(); });
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("✕ Remove",  null, (s, ev) => { _settings.Services.Remove(svc); _settings.Save(); RefreshServicesPage(); });
        ctx.Show(lvServices, e.Location);
    }

    private void BtnExportLog_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"EventLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            string src   = cboLogSource.SelectedItem?.ToString() ?? "All";
            string level = cboLogLevel.SelectedItem?.ToString()  ?? "All";
            var entries  = _eventLog.GetFiltered(src == "All Sources" ? "All" : src, level == "All Levels" ? "All" : level, txtLogSearch.Text);
            File.WriteAllText(dlg.FileName, _eventLog.ExportCsv(entries));
            MessageBox.Show("Exported.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ─────────────────────────────────────────────────────
    //  UI HELPERS
    // ─────────────────────────────────────────────────────
    private static TableLayoutPanel MakeGridRow(Panel parent, int y, int cols, int height)
    {
        var tlp = new TableLayoutPanel { Location = new Point(10, y), Size = new Size(parent.Width - 20, height), ColumnCount = cols, RowCount = 1, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        for (int i = 0; i < cols; i++) tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
        parent.Controls.Add(tlp);
        return tlp;
    }

    private static (Panel card, Label bigLabel, ProgressBar bar) MakeMetricCard(string title, string value, Color textColor, Color barColor)
    {
        var card = new Panel { Margin = new Padding(4), BackColor = Color.White, Padding = new Padding(10) };
        card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card);
        var t = new Label { Text = title.ToUpper(), Font = new Font("Segoe UI", 7.5f), ForeColor = Color.FromArgb(170, 170, 170), AutoSize = true, Location = new Point(10, 8) };
        var v = new Label { Text = value, Font = new Font("Segoe UI", 20), ForeColor = textColor, AutoSize = true, Location = new Point(10, 26) };
        var bar = new ProgressBar { Location = new Point(10, 66), Size = new Size(card.Width - 20, 5), Minimum = 0, Maximum = 100, Value = 0, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        card.Resize += (s, e) => { bar.Width = card.Width - 20; bar.Top = card.Height - 14; };
        card.Controls.AddRange(new Control[] { t, v, bar });
        return (card, v, bar);
    }

    private static Panel MakeSimpleCard(string title, string value)
    {
        var card = new Panel { Margin = new Padding(4), BackColor = Color.White };
        card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card);
        var t = new Label { Text = title.ToUpper(), Font = new Font("Segoe UI", 7.5f), ForeColor = Color.Silver, AutoSize = true, Location = new Point(10, 8) };
        var v = new Label { Text = value, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 80), AutoSize = true, Location = new Point(10, 26) };
        card.Controls.AddRange(new Control[] { t, v });
        return card;
    }

    private static Panel MakeSectionPanel(string title, out Panel content)
    {
        var outer = new Panel { BackColor = Color.White };
        outer.Paint += (s, e) => DrawRoundedBorder(e.Graphics, outer);
        var header = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(248, 248, 252) };
        var lbl = new Label { Text = title, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(60, 60, 80), Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
        header.Controls.Add(lbl);
        content = new Panel { Dock = DockStyle.Fill };
        outer.Controls.Add(content);
        outer.Controls.Add(header);
        return outer;
    }

    private static TextBox MakeFormRow(Panel parent, int y, string label, string value, bool isPassword = false)
    {
        var lbl = new Label { Text = label, Location = new Point(12, y + 7), Size = new Size(170, 20), Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(100, 100, 120) };
        var txt = new TextBox { Text = value, Location = new Point(190, y + 3), Size = new Size(200, 26), Font = new Font("Segoe UI", 9), BackColor = Color.FromArgb(250, 250, 252) };
        if (isPassword) txt.PasswordChar = '•';
        parent.Controls.AddRange(new Control[] { lbl, txt });
        return txt;
    }

    private static CheckBox MakeToggleRow(Panel parent, int y, string label, bool value)
    {
        var lbl = new Label { Text = label, Location = new Point(12, y + 7), Size = new Size(240, 20), Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(80, 80, 100) };
        var chk = new CheckBox { Checked = value, Location = new Point(260, y + 6), Size = new Size(70, 20), Text = value ? "On" : "Off" };
        chk.CheckedChanged += (s, e) => chk.Text = chk.Checked ? "On" : "Off";
        parent.Controls.AddRange(new Control[] { lbl, chk });
        return chk;
    }

    private static TextBox MakeInlineField(TableLayoutPanel tlp, string label, string value, int col, int row)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var lbl = new Label { Text = label, Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.Gray };
        var txt = new TextBox { Text = value, Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9) };
        pnl.Controls.Add(txt); pnl.Controls.Add(lbl);
        tlp.Controls.Add(pnl, col, row);
        return txt;
    }

    private static Label MakeStatusLabel(string text, Color color) =>
        new Label { Text = text, ForeColor = color, Font = new Font("Segoe UI", 8), AutoSize = true };

    private void SaveSmtp(TextBox host, TextBox port, TextBox from, TextBox pass, TextBox to)
    {
        _settings.SmtpHost = host.Text;
        if (int.TryParse(port.Text, out int p)) _settings.SmtpPort = p;
        _settings.SmtpFrom = from.Text;
        if (!string.IsNullOrEmpty(pass.Text)) _settings.SmtpPassword = pass.Text;
        _settings.SmtpTo = to.Text;
    }

    // ─────────────────────────────────────────────────────
    //  DRAWING
    // ─────────────────────────────────────────────────────
    private static void DrawRoundedBorder(Graphics g, Control ctrl)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen  = new Pen(Color.FromArgb(220, 220, 232), 0.5f);
        var rect = new Rectangle(0, 0, ctrl.Width - 1, ctrl.Height - 1);
        int r = 8;
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, r, r, 180, 90);
        path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }

    private static void DrawLineChart(Graphics g, Panel panel, double[] data, Color lineColor, double maxValue)
    {
        if (data.Length < 2) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int w = panel.Width, h = panel.Height - 18;
        if (w < 10 || h < 10) return;

        var pts = new PointF[data.Length];
        for (int i = 0; i < data.Length; i++)
            pts[i] = new PointF(i * (w - 1f) / (data.Length - 1), h - (float)(data[i] / maxValue * h));

        var fillPts = new List<PointF>(pts) { new PointF(w - 1f, h), new PointF(0, h) };
        using var brush = new SolidBrush(Color.FromArgb(25, lineColor));
        g.FillPolygon(brush, fillPts.ToArray());

        using var pen = new Pen(lineColor, 1.5f);
        g.DrawLines(pen, pts);

        float ty = h - (float)(0.8 * h);
        using var dash = new Pen(Color.FromArgb(180, 229, 83, 80), 0.5f) { DashStyle = DashStyle.Dash };
        g.DrawLine(dash, 0, ty, w, ty);

        string[] labels = { "-5m", "-4m", "-3m", "-2m", "-1m", "now" };
        using var font  = new Font("Segoe UI", 7);
        using var br    = new SolidBrush(Color.FromArgb(160, 160, 160));
        for (int i = 0; i < labels.Length; i++)
            g.DrawString(labels[i], font, br, i * (w - 1f) / (labels.Length - 1) - 8, h + 2);
    }
    // ─────────────────────────────────────────────────────
    //  PAGE: IIS Monitor
    // ─────────────────────────────────────────────────────
    private Panel BuildIisPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };
        int y = 10;

        var row = MakeGridRow(page, y, 4, 80);
        (var c1, lblIisReqSec, _) = MakeMetricCard("Requests/sec", "—", Color.FromArgb(21, 101, 192), Color.FromArgb(30, 136, 229));
        (var c2, lblIisConn, _) = MakeMetricCard("Active Conn.", "—", Color.FromArgb(0, 105, 92), Color.FromArgb(0, 137, 123));
        (var c3, lblIis4xx, _) = MakeMetricCard("4xx Errors", "—", Color.FromArgb(230, 81, 0), Color.FromArgb(251, 140, 0));
        (var c4, lblIis5xx, _) = MakeMetricCard("5xx Errors", "—", Color.FromArgb(198, 40, 40), Color.FromArgb(239, 83, 80));
        row.Controls.Add(c1, 0, 0); row.Controls.Add(c2, 1, 0); row.Controls.Add(c3, 2, 0); row.Controls.Add(c4, 3, 0);
        y += 90;

        var chartSec = MakeSectionPanel("Requests/sec — Live", out var chartBody);
        chartSec.Location = new Point(10, y); chartSec.Size = new Size(860, 110);
        chartSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlIisChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlIisChart.Paint += (s, e) => DrawLineChart(e.Graphics, pnlIisChart, _iisMetrics.RequestHistory, Color.FromArgb(30, 136, 229), 100);
        chartBody.Controls.Add(pnlIisChart);
        y += 120;

        var poolSec = MakeSectionPanel("Application Pools", out var poolBody);
        poolSec.Location = new Point(10, y); poolSec.Size = new Size(420, 220);
        lvAppPools = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvAppPools.Columns.Add("App Pool", 160); lvAppPools.Columns.Add("State", 80);
        lvAppPools.Columns.Add("Runtime", 80); lvAppPools.Columns.Add("RAM (MB)", 80);
        poolBody.Controls.Add(lvAppPools);

        var siteSec = MakeSectionPanel("IIS Sites", out var siteBody);
        siteSec.Location = new Point(440, y); siteSec.Size = new Size(430, 220);
        siteSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvSites = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvSites.Columns.Add("Site", 130); lvSites.Columns.Add("State", 70);
        lvSites.Columns.Add("Req/sec", 70); lvSites.Columns.Add("Binding", 140);
        siteBody.Controls.Add(lvSites);

        page.Controls.Add(poolSec); page.Controls.Add(siteSec);
        return page;
    }

    private void RefreshIisPage()
    {
        if (lblIisReqSec == null) return;
        lblIisReqSec.Text = $"{_iisMetrics.RequestsPerSec}";
        lblIisConn.Text = $"{_iisMetrics.ActiveConnections}";
        lblIis4xx.Text = $"{_iisMetrics.Error4xxCount}";
        lblIis5xx.Text = $"{_iisMetrics.Error5xxCount}";

        lvAppPools.Items.Clear();
        foreach (var p in _iisMetrics.AppPools)
        {
            var item = new ListViewItem(p.Name);
            item.SubItems.Add(p.State.ToString());
            item.SubItems.Add(p.RuntimeVersion);
            item.SubItems.Add(p.TotalMemoryMb.ToString());
            item.ForeColor = p.State == AppPoolState.Running ? Color.FromArgb(46, 125, 50) : Color.FromArgb(198, 40, 40);
            lvAppPools.Items.Add(item);
        }

        lvSites.Items.Clear();
        foreach (var s in _iisMetrics.Sites)
        {
            var item = new ListViewItem(s.Name);
            item.SubItems.Add(s.State.ToString());
            item.SubItems.Add(s.RequestsPerSec.ToString("F1"));
            item.SubItems.Add(s.BindingDisplay);
            item.ForeColor = s.State == SiteState.Running ? Color.FromArgb(46, 125, 50) : Color.FromArgb(198, 40, 40);
            lvSites.Items.Add(item);
        }
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Network
    // ─────────────────────────────────────────────────────
    private Panel BuildNetworkPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };
        int y = 10;

        var row = MakeGridRow(page, y, 3, 80);
        (var c1, lblNetIn, _) = MakeMetricCard("Inbound Mbps", "—", Color.FromArgb(0, 105, 92), Color.FromArgb(0, 137, 123));
        (var c2, lblNetOut, _) = MakeMetricCard("Outbound Mbps", "—", Color.FromArgb(74, 20, 140), Color.FromArgb(123, 31, 162));
        (var c3, lblTcpConn, _) = MakeMetricCard("TCP Conns", "—", Color.FromArgb(21, 101, 192), Color.FromArgb(30, 136, 229));
        row.Controls.Add(c1, 0, 0); row.Controls.Add(c2, 1, 0); row.Controls.Add(c3, 2, 0);
        y += 90;

        var inSec = MakeSectionPanel("Inbound — Live", out var inBody);
        inSec.Location = new Point(10, y); inSec.Size = new Size(425, 110);
        pnlNetInChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlNetInChart.Paint += (s, e) => DrawLineChart(e.Graphics, pnlNetInChart, _networkMetrics.InboundHistory, Color.FromArgb(0, 137, 123), 100);
        inBody.Controls.Add(pnlNetInChart);

        var outSec = MakeSectionPanel("Outbound — Live", out var outBody);
        outSec.Location = new Point(445, y); outSec.Size = new Size(425, 110);
        outSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlNetOutChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        pnlNetOutChart.Paint += (s, e) => DrawLineChart(e.Graphics, pnlNetOutChart, _networkMetrics.OutboundHistory, Color.FromArgb(123, 31, 162), 100);
        outBody.Controls.Add(pnlNetOutChart);
        y += 120;

        var portSec = MakeSectionPanel("Port Monitor", out var portBody);
        portSec.Location = new Point(10, y); portSec.Size = new Size(425, 220);

        var portToolbar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.FromArgb(248, 248, 252) };
        var btnAddPort = new Button { Text = "+ Add Port", Location = new Point(8, 5), Size = new Size(90, 22), BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        btnAddPort.FlatAppearance.BorderSize = 0;
        btnAddPort.Click += BtnAddPort_Click;
        portToolbar.Controls.Add(btnAddPort);

        lvPorts = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvPorts.Columns.Add("Label", 100); lvPorts.Columns.Add("Host", 100);
        lvPorts.Columns.Add("Port", 55); lvPorts.Columns.Add("Status", 70); lvPorts.Columns.Add("ms", 55);
        portBody.Controls.Add(lvPorts); portBody.Controls.Add(portToolbar);

        var connSec = MakeSectionPanel("Top Connections", out var connBody);
        connSec.Location = new Point(445, y); connSec.Size = new Size(425, 220);
        connSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvConnections = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvConnections.Columns.Add("Remote Address", 160); lvConnections.Columns.Add("Connections", 90); lvConnections.Columns.Add("State", 90);
        connBody.Controls.Add(lvConnections);

        page.Controls.Add(inSec); page.Controls.Add(outSec);
        page.Controls.Add(portSec); page.Controls.Add(connSec);
        return page;
    }

    private void RefreshNetworkPage()
    {
        if (lblNetIn == null) return;
        lblNetIn.Text = $"{_networkMetrics.InboundMbps}";
        lblNetOut.Text = $"{_networkMetrics.OutboundMbps}";
        lblTcpConn.Text = $"{_networkMetrics.TcpConnectionCount}";

        lvPorts.Items.Clear();
        foreach (var p in _networkMetrics.PortResults)
        {
            var item = new ListViewItem(p.Label);
            item.SubItems.Add(p.Host); item.SubItems.Add(p.Port.ToString());
            item.SubItems.Add(p.IsOpen ? "Open" : "Closed");
            item.SubItems.Add(p.ResponseMs.ToString());
            item.ForeColor = p.IsOpen ? Color.FromArgb(46, 125, 50) : Color.FromArgb(198, 40, 40);
            if (!p.IsOpen) item.BackColor = Color.FromArgb(255, 248, 248);
            lvPorts.Items.Add(item);
        }

        lvConnections.Items.Clear();
        foreach (var c in _networkMetrics.TopConnections)
        {
            var item = new ListViewItem(c.RemoteAddress);
            item.SubItems.Add(c.Count.ToString()); item.SubItems.Add(c.State);
            lvConnections.Items.Add(item);
        }
    }

    private void BtnAddPort_Click(object? sender, EventArgs e)
    {
        using var dlg = new AddPortForm();
        if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
        {
            _settings.Ports.Add(dlg.Result);
            _settings.Save();
        }
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Security
    // ─────────────────────────────────────────────────────
    private Panel BuildSecurityPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };
        int y = 10;

        var row = MakeGridRow(page, y, 3, 80);
        (var c1, lblFailedLogins, _) = MakeMetricCard("Failed Logins (1h)", "0", Color.FromArgb(198, 40, 40), Color.FromArgb(239, 83, 80));
        (var c2, lblSuspProcs, _) = MakeMetricCard("Suspicious Procs", "0", Color.FromArgb(230, 81, 0), Color.FromArgb(251, 140, 0));
        (var c3, lblFileChanges, _) = MakeMetricCard("File Changes", "0", Color.FromArgb(74, 20, 140), Color.FromArgb(123, 31, 162));
        row.Controls.Add(c1, 0, 0); row.Controls.Add(c2, 1, 0); row.Controls.Add(c3, 2, 0);
        y += 90;

        var evtSec = MakeSectionPanel("Recent Security Events", out var evtBody);
        evtSec.Location = new Point(10, y); evtSec.Size = new Size(550, 220);
        lvSecEvents = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.None };
        lvSecEvents.Columns.Add("Time", 70); lvSecEvents.Columns.Add("Event Type", 150); lvSecEvents.Columns.Add("Message", 310);
        evtBody.Controls.Add(lvSecEvents);

        var logonSec = MakeSectionPanel("Active Logon Sessions", out var logonBody);
        logonSec.Location = new Point(570, y); logonSec.Size = new Size(300, 220);
        logonSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvLogonSessions = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvLogonSessions.Columns.Add("Username", 120); lvLogonSessions.Columns.Add("Session", 80); lvLogonSessions.Columns.Add("State", 80);
        logonBody.Controls.Add(lvLogonSessions);
        y += 230;

        var suspSec = MakeSectionPanel("Suspicious Processes", out var suspBody);
        suspSec.Location = new Point(10, y); suspSec.Size = new Size(860, 150);
        suspSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvSuspProcs = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvSuspProcs.Columns.Add("Process Name", 160); lvSuspProcs.Columns.Add("PID", 70);
        lvSuspProcs.Columns.Add("RAM (MB)", 80); lvSuspProcs.Columns.Add("Start Time", 140);
        suspBody.Controls.Add(lvSuspProcs);

        page.Controls.Add(evtSec); page.Controls.Add(logonSec); page.Controls.Add(suspSec);
        return page;
    }

    private void RefreshSecurityPage()
    {
        if (lblFailedLogins == null) return;
        lblFailedLogins.Text = _securityMetrics.FailedLogins.ToString();
        lblSuspProcs.Text = _securityMetrics.SuspiciousProcesses.Count.ToString();
        lblFileChanges.Text = _securityMetrics.FileIntegrityAlerts.Count.ToString();

        lvSecEvents.Items.Clear();
        foreach (var e in _securityMetrics.RecentSecurityEvents.Take(50))
        {
            var item = new ListViewItem(e.Time.ToString("HH:mm:ss"));
            item.SubItems.Add(e.EventType); item.SubItems.Add(e.Message);
            item.ForeColor = e.Level == "ERROR" ? Color.FromArgb(198, 40, 40) : Color.FromArgb(46, 125, 50);
            lvSecEvents.Items.Add(item);
        }

        lvLogonSessions.Items.Clear();
        foreach (var s in _securityMetrics.LogonSessions)
        {
            var item = new ListViewItem(s.Username);
            item.SubItems.Add(s.SessionId); item.SubItems.Add(s.State);
            lvLogonSessions.Items.Add(item);
        }

        lvSuspProcs.Items.Clear();
        foreach (var p in _securityMetrics.SuspiciousProcesses)
        {
            var item = new ListViewItem(p.Name);
            item.SubItems.Add(p.Pid.ToString()); item.SubItems.Add(p.MemoryMb.ToString());
            item.SubItems.Add(p.StartTime.ToString("HH:mm:ss"));
            item.ForeColor = Color.FromArgb(198, 40, 40);
            item.BackColor = Color.FromArgb(255, 248, 248);
            lvSuspProcs.Items.Add(item);
        }
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Database
    // ─────────────────────────────────────────────────────
    private Panel BuildDatabasePage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };
        int y = 10;

        var toolbar = new Panel { Location = new Point(10, y), Size = new Size(860, 34), BackColor = Color.FromArgb(244, 245, 249) };
        var btnAddDb = new Button { Text = "+ Add Database", Location = new Point(0, 5), Size = new Size(120, 24), BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        btnAddDb.FlatAppearance.BorderSize = 0;
        btnAddDb.Click += BtnAddDatabase_Click;
        toolbar.Controls.Add(btnAddDb);
        page.Controls.Add(toolbar);
        y += 44;

        var dbSec = MakeSectionPanel("Database Connections", out var dbBody);
        dbSec.Location = new Point(10, y); dbSec.Size = new Size(860, 180);
        dbSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvDatabases = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None };
        lvDatabases.Columns.Add("Name", 120); lvDatabases.Columns.Add("Type", 80);
        lvDatabases.Columns.Add("Status", 80); lvDatabases.Columns.Add("Response", 80);
        lvDatabases.Columns.Add("Active Conn", 90); lvDatabases.Columns.Add("Size MB", 80);
        lvDatabases.Columns.Add("Cache Hit%", 80); lvDatabases.Columns.Add("Blocked", 70);
        dbBody.Controls.Add(lvDatabases);
        y += 190;

        var slowSec = MakeSectionPanel("Slow Queries (> 1 sec)", out var slowBody);
        slowSec.Location = new Point(10, y); slowSec.Size = new Size(860, 250);
        slowSec.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvSlowQueries = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Font = new Font("Consolas", 8.5f), BorderStyle = BorderStyle.None };
        lvSlowQueries.Columns.Add("DB", 100); lvSlowQueries.Columns.Add("Avg ms", 70);
        lvSlowQueries.Columns.Add("Executions", 80); lvSlowQueries.Columns.Add("Query", 590);
        slowBody.Controls.Add(lvSlowQueries);

        page.Controls.Add(dbSec); page.Controls.Add(slowSec);
        return page;
    }

    private void RefreshDatabasePage()
    {
        if (lvDatabases == null) return;
        lvDatabases.Items.Clear();
        foreach (var db in _dbMetrics)
        {
            var item = new ListViewItem(db.Name);
            item.SubItems.Add(db.DbType.ToString());
            item.SubItems.Add(db.IsOnline ? "Online" : "Offline");
            item.SubItems.Add($"{db.ResponseMs}ms");
            item.SubItems.Add($"{db.ActiveConnections}/{db.MaxConnections}");
            item.SubItems.Add($"{db.DatabaseSizeMb:F0}");
            item.SubItems.Add($"{db.CacheHitRatio:F1}%");
            item.SubItems.Add(db.BlockedQueries.ToString());
            item.ForeColor = db.IsOnline ? Color.FromArgb(46, 125, 50) : Color.FromArgb(198, 40, 40);
            if (!db.IsOnline)
            {
                item.BackColor = Color.FromArgb(255, 248, 248);
                item.ToolTipText = db.LastError; // Error dikhao hover pe
            }
            lvDatabases.Items.Add(item);
        }

        lvSlowQueries.Items.Clear();
        foreach (var db in _dbMetrics)
            foreach (var q in db.SlowQueries)
            {
                var item = new ListViewItem(db.Name);
                item.SubItems.Add(q.DurationMs.ToString());
                item.SubItems.Add(q.ExecutionCount.ToString());
                item.SubItems.Add(q.QueryText);
                item.ForeColor = q.DurationMs > 5000 ? Color.FromArgb(198, 40, 40) : Color.FromArgb(230, 81, 0);
                lvSlowQueries.Items.Add(item);
            }
    }

    private void BtnAddDatabase_Click(object? sender, EventArgs e)
    {
        using var dlg = new AddDatabaseForm();
        if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
        {
            _settings.Databases.Add(dlg.Result);
            _settings.Save();
            MessageBox.Show("Database added!", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshDatabasePage();
        }
    }

    // ─────────────────────────────────────────────────────
    //  PAGE: Reports
    // ─────────────────────────────────────────────────────
    private Panel BuildReportsPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249), AutoScroll = true };
        int y = 20;

        var sec = MakeSectionPanel("Generate Reports", out var body);
        sec.Location = new Point(10, y); sec.Size = new Size(500, 300);

        int ry = 16;
        AddReportRow(body, ref ry, "Weekly HTML Report", "Last 7 days — services, domains, alerts, metrics", async () =>
        {
            var html = _reportGen.GenerateWeeklyHtml(_metricsHistory, _alerts, _settings.Services, _settings.Domains);
            var path = _reportGen.GetDefaultReportPath("weekly");
            await _reportGen.SaveHtmlReportAsync(html, path);
            if (MessageBox.Show($"Report saved:\n{path}\n\nOpen in browser?", "Report Ready",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });

        AddReportRow(body, ref ry, "Daily Email Report", "Send today's summary via SMTP now", async () =>
        {
            bool ok = await _emailSender.SendDailyReportAsync(_currentMetrics, _settings.Services, _settings.Domains, _alerts.Where(a => a.Time.Date == DateTime.Today));
            MessageBox.Show(ok ? "Daily report emailed!" : "Failed — check SMTP settings.", "Email Report", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        });

        AddReportRow(body, ref ry, "Security Event Export", "Export security events to CSV", async () =>
        {
            using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"SecurityEvents_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var lines = new List<string> { "Time,EventType,Level,Message" };
                lines.AddRange(_securityMetrics.RecentSecurityEvents.Select(e =>
                    $"\"{e.Time:yyyy-MM-dd HH:mm:ss}\",\"{e.EventType}\",\"{e.Level}\",\"{e.Message.Replace("\"", "'")}\""));
                await File.WriteAllLinesAsync(dlg.FileName, lines);
                MessageBox.Show("Exported.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        });

        page.Controls.Add(sec);
        return page;
    }

    private static void AddReportRow(Panel parent, ref int y, string title, string desc, Func<Task> action)
    {
        var pnl = new Panel { Location = new Point(12, y), Size = new Size(460, 52), BackColor = Color.FromArgb(248, 249, 255) };
        pnl.Paint += (s, e) => { using var pen = new Pen(Color.FromArgb(220, 220, 232)); e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1); };
        var lbl = new Label { Text = title, Location = new Point(10, 7), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 80) };
        var sub = new Label { Text = desc, Location = new Point(10, 26), AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = Color.Silver };
        var btn = new Button { Text = "Generate", Location = new Point(355, 14), Size = new Size(88, 24), BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += async (s, e) => { btn.Enabled = false; btn.Text = "..."; try { await action(); } finally { btn.Enabled = true; btn.Text = "Generate"; } };
        pnl.Controls.AddRange(new Control[] { lbl, sub, btn });
        parent.Controls.Add(pnl);
        y += 62;
    }
    private static int Clamp(int v) => Math.Max(0, Math.Min(100, v));
    private Panel BuildLogFilesPage()
    {
        var page = new Panel { BackColor = Color.FromArgb(244, 245, 249) };

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(244, 245, 249) };

        var btnAddFile = new Button
        {
            Text = "+ Add File",
            Location = new Point(10, 7),
            Size = new Size(90, 24),
            BackColor = Color.FromArgb(63, 81, 181),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        btnAddFile.FlatAppearance.BorderSize = 0;
        btnAddFile.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select Log File",
                Filter = "Log files|*.log;*.txt;*.out|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            foreach (var f in dlg.FileNames)
            {
                if (!_settings.WatchedFiles.Contains(f))
                {
                    _settings.WatchedFiles.Add(f);
                    _fileWatcher.AddFile(f);
                }
            }
            _settings.Save();
            RefreshFileList();
        };

        cboFileLevel = new ComboBox
        {
            Location = new Point(110, 7),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9)
        };
        cboFileLevel.Items.AddRange(new[] { "All", "ERROR", "WARN", "INFO", "DEBUG" });
        cboFileLevel.SelectedIndex = 0;
        cboFileLevel.SelectedIndexChanged += (s, e) => RefreshFileLogView();

        txtFileSearch = new TextBox
        {
            Location = new Point(220, 7),
            Width = 180,
            Font = new Font("Segoe UI", 9),
            PlaceholderText = "Search..."
        };
        txtFileSearch.TextChanged += (s, e) => RefreshFileLogView();

        lblFileStatus = new Label
        {
            Location = new Point(410, 10),
            AutoSize = true,
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.Silver
        };

        var btnClear = new Button
        {
            Text = "Clear View",
            Location = new Point(680, 7),
            Size = new Size(80, 24),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9)
        };
        btnClear.Click += (s, e) => rtbFileLog?.Clear();

        toolbar.Controls.AddRange(new Control[]
            { btnAddFile, cboFileLevel, txtFileSearch, lblFileStatus, btnClear });

        // Split
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 50,
            Panel2MinSize = 50
        };

        // LEFT
        var fileSec = MakeSectionPanel("Watched Files", out var fileBody);
        fileSec.Dock = DockStyle.Fill;

        lvWatchFiles = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            Font = new Font("Segoe UI", 8.5f),
            BorderStyle = BorderStyle.None
        };
        lvWatchFiles.Columns.Add("File", 120);
        lvWatchFiles.Columns.Add("●", 30);
        lvWatchFiles.SelectedIndexChanged += (s, e) =>
        {
            if (lvWatchFiles.SelectedItems.Count == 0) return;
            _selectedWatchFile = lvWatchFiles.SelectedItems[0].Tag?.ToString() ?? "";
            RefreshFileLogView();
            lblFileStatus.Text = Path.GetFileName(_selectedWatchFile);
        };
        lvWatchFiles.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Delete || lvWatchFiles.SelectedItems.Count == 0) return;
            var path = lvWatchFiles.SelectedItems[0].Tag?.ToString() ?? "";
            _fileWatcher.RemoveFile(path);
            _settings.WatchedFiles.Remove(path);
            _settings.Save();
            if (_selectedWatchFile == path) { _selectedWatchFile = ""; rtbFileLog?.Clear(); }
            RefreshFileList();
        };

        fileBody.Controls.Add(lvWatchFiles);
        split.Panel1.Controls.Add(fileSec);

        // RIGHT
        var logSec = MakeSectionPanel("Log Output — Live", out var logBody);
        logSec.Dock = DockStyle.Fill;

        rtbFileLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(13, 17, 23),
            ForeColor = Color.FromArgb(205, 217, 229),
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        logBody.Controls.Add(rtbFileLog);
        split.Panel2.Controls.Add(logSec);

        page.Controls.Add(split);
        page.Controls.Add(toolbar);

        // YEH YAHAN lagao — Controls.Add ke baad
        split.VisibleChanged += (s, e) =>
        {
            if (split.Visible && split.Width > 500)
            {
                try { split.SplitterDistance = 200; } catch { }
            }
        };

        return page;
    }

    private void RefreshFileList()
    {
        if (lvWatchFiles == null) return;
        lvWatchFiles.Items.Clear();
        foreach (var entry in _fileWatcher.GetEntries())
        {
            var item = new ListViewItem(entry.FileName);
            item.SubItems.Add(entry.IsActive ? "●" : "○");
            item.ForeColor = entry.IsActive ? Color.FromArgb(102, 187, 106) : Color.Gray;
            item.Tag = entry.FilePath;
            lvWatchFiles.Items.Add(item);
        }
    }

    private void RefreshFileLogView()
    {
        if (rtbFileLog == null || string.IsNullOrEmpty(_selectedWatchFile)) return;

        string level = cboFileLevel?.SelectedItem?.ToString() ?? "All";
        string search = txtFileSearch?.Text ?? "";

        var lines = _fileWatcher.GetLines(_selectedWatchFile, level, search);

        rtbFileLog.Clear();
        rtbFileLog.SuspendLayout();
        foreach (var line in lines)
            AppendFileLog(line);
        rtbFileLog.ResumeLayout();
        rtbFileLog.ScrollToCaret();
    }

    private void AppendFileLog(LogLine line)
    {
        if (rtbFileLog == null) return;

        // Time
        AppendFileLogText($"{line.Time:HH:mm:ss} ", Color.FromArgb(72, 79, 88));

        // Level badge
        var (levelColor, bgNote) = line.Level switch
        {
            "ERROR" => (Color.FromArgb(248, 81, 73), ""),
            "WARN" => (Color.FromArgb(210, 153, 34), ""),
            "DEBUG" => (Color.FromArgb(139, 148, 158), ""),
            _ => (Color.FromArgb(63, 185, 80), "")
        };
        AppendFileLogText($"[{line.Level}] ", levelColor);

        // Message
        AppendFileLogText(line.Raw + "\n", Color.FromArgb(205, 217, 229));
    }

    private void AppendFileLogText(string text, Color color)
    {
        rtbFileLog.SelectionStart = rtbFileLog.TextLength;
        rtbFileLog.SelectionLength = 0;
        rtbFileLog.SelectionColor = color;
        rtbFileLog.AppendText(text);
        rtbFileLog.SelectionColor = rtbFileLog.ForeColor;
    }
    // ─────────────────────────────────────────────────────
    //  CLEANUP
    // ─────────────────────────────────────────────────────
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _mainTimer?.Stop(); _uiTimer?.Stop();
        _metrics?.Dispose(); _eventLog?.Dispose(); _domainMonitor?.Dispose();
        _fileWatcher?.Dispose();
        base.OnFormClosed(e);
    }
}
