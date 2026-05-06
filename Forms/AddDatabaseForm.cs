using System.Runtime.Versioning;
using UnivueGuardian.Models;

namespace UnivueGuardian.Forms;

[SupportedOSPlatform("windows")]
public class AddDatabaseForm : Form
{
    public DatabaseConnection? Result { get; private set; }
    private TextBox txtName = null!, txtHost = null!, txtPort = null!, txtConnStr = null!;
    private ComboBox cboType = null!;

    public AddDatabaseForm()
    {
        Text = "Add Database Connection";
        Size = new Size(420, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.White; Font = new Font("Segoe UI", 9);

        int y = 16;
        AddLabel("Name:", 14, y); txtName = AddTxt(150, y, "My Database"); y += 36;
        AddLabel("DB Type:", 14, y);
        cboType = new ComboBox { Location = new Point(150, y), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        cboType.Items.AddRange(new[] { "SqlServer", "MySql", "PostgreSql" });
        cboType.SelectedIndex = 0;
        cboType.SelectedIndexChanged += (s, e) =>
        {
            txtPort.Text = cboType.SelectedIndex switch { 1 => "3306", 2 => "5432", _ => "1433" };
        };
        Controls.Add(cboType); y += 36;

        AddLabel("Host:", 14, y); txtHost = AddTxt(150, y, "localhost"); y += 36;
        AddLabel("Port:", 14, y); txtPort = AddTxt(150, y, "1433"); y += 36;
        AddLabel("Conn. String:", 14, y);
        txtConnStr = new TextBox
        {
            Location = new Point(150, y),
            Width = 220,
            Font = new Font("Segoe UI", 8.5f),
            PlaceholderText = "Server=...;Database=...;User Id=...;Password=...;"
        };
        Controls.Add(txtConnStr); y += 46;

        var btnOk = new Button
        {
            Text = "Add",
            Location = new Point(220, y),
            Size = new Size(75, 28),
            BackColor = Color.FromArgb(63, 81, 181),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Name required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (!int.TryParse(txtPort.Text, out int port)) port = 1433;
            Result = new DatabaseConnection
            {
                Name = txtName.Text.Trim(),
                DbType = cboType.SelectedIndex switch { 1 => DbType2.MySql, 2 => DbType2.PostgreSql, _ => DbType2.SqlServer },
                Host = txtHost.Text.Trim(),
                Port = port,
                ConnectionString = txtConnStr.Text.Trim()
            };
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(305, y),
            Size = new Size(75, 28),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };
        Controls.AddRange(new Control[] { btnOk, btnCancel });
        AcceptButton = btnOk; CancelButton = btnCancel;
    }

    private TextBox AddTxt(int x, int y, string placeholder)
    {
        var txt = new TextBox
        {
            Location = new Point(x, y),
            Width = 220,
            Font = new Font("Segoe UI", 9),
            PlaceholderText = placeholder
        };
        Controls.Add(txt);
        return txt;
    }

    private void AddLabel(string text, int x, int y) =>
        Controls.Add(new Label
        {
            Text = text,
            Location = new Point(x, y + 3),
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 100)
        });
}