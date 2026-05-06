using System.Runtime.Versioning;
using UnivueGuardian.Models;

namespace UnivueGuardian.Forms;

[SupportedOSPlatform("windows")]
public class AddPortForm : Form
{
    public MonitoredPort? Result { get; private set; }
    private TextBox txtLabel = null!, txtHost = null!, txtPort = null!;
    private ComboBox cboProtocol = null!;

    public AddPortForm()
    {
        Text = "Add Port Monitor";
        Size = new Size(380, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.White; Font = new Font("Segoe UI", 9);

        int y = 16;
        AddLabel("Label:",    16, y); txtLabel    = AddTxt(160, y, "e.g. RabbitMQ"); y += 36;
        AddLabel("Host:",     16, y); txtHost     = AddTxt(160, y, "localhost");      y += 36;
        AddLabel("Port:",     16, y); txtPort     = AddTxt(160, y, "5672");           y += 36;
        AddLabel("Protocol:", 16, y);
        cboProtocol = new ComboBox { Location = new Point(160, y), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        cboProtocol.Items.AddRange(new[] { "TCP", "UDP" }); cboProtocol.SelectedIndex = 0;
        Controls.Add(cboProtocol); y += 46;

        var btnOk = new Button { Text = "Add", Location = new Point(185, y), Size = new Size(75, 28), BackColor = Color.FromArgb(63,81,181), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (s, e) =>
        {
            if (!int.TryParse(txtPort.Text, out int port)) { MessageBox.Show("Invalid port.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; return; }
            Result = new MonitoredPort { Label = txtLabel.Text.Trim(), Host = txtHost.Text.Trim(), Port = port, Protocol = cboProtocol.SelectedItem?.ToString() ?? "TCP" };
        };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(270, y), Size = new Size(75, 28), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        Controls.AddRange(new Control[] { btnOk, btnCancel });
        AcceptButton = btnOk; CancelButton = btnCancel;
    }

    private TextBox AddTxt(int x, int y, string placeholder)
    {
        var txt = new TextBox { Location = new Point(x, y), Width = 180, Font = new Font("Segoe UI", 9), PlaceholderText = placeholder };
        Controls.Add(txt); return txt;
    }
    private void AddLabel(string text, int x, int y) =>
        Controls.Add(new Label { Text = text, Location = new Point(x, y + 3), AutoSize = true, ForeColor = Color.FromArgb(80,80,100) });
}
