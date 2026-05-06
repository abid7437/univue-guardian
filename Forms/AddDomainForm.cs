using System.Runtime.Versioning;
using UnivueGuardian.Models;

namespace UnivueGuardian.Forms;

[SupportedOSPlatform("windows")]
public class AddDomainForm : Form
{
    public MonitoredDomain? Result { get; private set; }

    private TextBox txtUrl = null!;
    private TextBox txtDisplayName = null!;

    public AddDomainForm()
    {
        InitDialog();
    }

    private void InitDialog()
    {
        Text = "Add Domain to Monitor";
        Size = new Size(420, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9);

        int y = 20;
        AddLabel("URL (https://...):", 16, y);
        txtUrl = new TextBox { Location = new Point(160, y), Width = 220, PlaceholderText = "https://example.com" };
        Controls.Add(txtUrl);
        y += 38;

        AddLabel("Display Name:", 16, y);
        txtDisplayName = new TextBox { Location = new Point(160, y), Width = 220, PlaceholderText = "Optional friendly name" };
        Controls.Add(txtDisplayName);
        y += 50;

        var btnOk = new Button
        {
            Text = "Add", Location = new Point(220, y), Size = new Size(80, 30),
            BackColor = Color.FromArgb(63, 81, 181), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += BtnOk_Click;

        var btnCancel = new Button
        {
            Text = "Cancel", Location = new Point(310, y), Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel
        };
        Controls.AddRange(new Control[] { btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        string url = txtUrl.Text.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Please enter a valid URL starting with https:// or http://",
                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        Result = new MonitoredDomain
        {
            Url = url,
            DisplayName = txtDisplayName.Text.Trim().Length > 0
                ? txtDisplayName.Text.Trim()
                : new Uri(url).Host
        };
    }

    private void AddLabel(string text, int x, int y)
    {
        Controls.Add(new Label
        {
            Text = text, Location = new Point(x, y + 3),
            AutoSize = true, ForeColor = Color.FromArgb(80, 80, 100)
        });
    }
}
