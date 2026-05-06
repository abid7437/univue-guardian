using System.Runtime.Versioning;
using System.ServiceProcess;
using UnivueGuardian.Models;

namespace UnivueGuardian.Forms;

[SupportedOSPlatform("windows")]
public class AddServiceForm : Form
{
    public MonitoredService? Result { get; private set; }

    private ComboBox cboServiceName = null!;
    private TextBox txtDisplayName  = null!;
    private CheckBox chkAutoRestart = null!;
    private NumericUpDown nudMaxRestarts = null!;

    public AddServiceForm()
    {
        InitDialog();
        LoadInstalledServices();
    }

    private void InitDialog()
    {
        Text = "Add Service to Monitor";
        Size = new Size(420, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9);

        int y = 20;
        AddLabel("Service Name:", 16, y);
        cboServiceName = new ComboBox { Location = new Point(160, y), Width = 220, DropDownStyle = ComboBoxStyle.DropDown };
        cboServiceName.SelectedIndexChanged += (s, e) =>
        {
            if (txtDisplayName.Text.Length == 0)
                txtDisplayName.Text = cboServiceName.Text;
        };
        Controls.Add(cboServiceName);
        y += 38;

        AddLabel("Display Name:", 16, y);
        txtDisplayName = new TextBox { Location = new Point(160, y), Width = 220 };
        Controls.Add(txtDisplayName);
        y += 38;

        AddLabel("Auto-Restart:", 16, y);
        chkAutoRestart = new CheckBox { Location = new Point(160, y), Checked = true, Text = "Enabled" };
        Controls.Add(chkAutoRestart);
        y += 38;

        AddLabel("Max Restarts:", 16, y);
        nudMaxRestarts = new NumericUpDown
        {
            Location = new Point(160, y), Width = 80,
            Minimum = 1, Maximum = 10, Value = 3
        };
        Controls.Add(nudMaxRestarts);
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
        if (string.IsNullOrWhiteSpace(cboServiceName.Text))
        {
            MessageBox.Show("Please select or enter a service name.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        Result = new MonitoredService
        {
            ServiceName  = cboServiceName.Text.Trim(),
            DisplayName  = txtDisplayName.Text.Trim().Length > 0 ? txtDisplayName.Text.Trim() : cboServiceName.Text.Trim(),
            AutoRestart  = chkAutoRestart.Checked,
            MaxRestarts  = (int)nudMaxRestarts.Value
        };
    }

    private void LoadInstalledServices()
    {
        try
        {
            var names = ServiceController.GetServices()
                .Select(s => s.ServiceName)
                .OrderBy(s => s)
                .ToArray();
            cboServiceName.Items.AddRange(names);
        }
        catch { }
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
