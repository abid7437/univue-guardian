using System.Runtime.Versioning;
using UnivueGuardian.Forms;

[assembly: SupportedOSPlatform("windows")]

namespace UnivueGuardian;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // High-DPI support for Windows Server 2022
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Global exception handler
        Application.ThreadException += (s, e) =>
        {
            MessageBox.Show(
                $"Unexpected error:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Univue Guardian — Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                MessageBox.Show($"Fatal error: {ex.Message}", "Fatal", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        };

        Application.Run(new MainForm());
    }
}
