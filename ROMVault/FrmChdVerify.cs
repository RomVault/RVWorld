using System;
using System.Drawing;
using System.Windows.Forms;
using RomVaultCore.Utils;

namespace ROMVault;

/// <summary>
/// WinForms report window for CHD verification and mapping diagnostics.
/// </summary>
/// <remarks>
/// Displays:
/// - parsed member hashes (Name/Size/CRC/SHA1/MD5)
/// - expected-to-extracted mapping rows
/// - raw text report emitted by <see cref="RomVaultCore.Utils.ChdVerify"/>
/// </remarks>
public sealed class FrmChdVerify : Form
{
    private readonly string _baseTitle;
    private readonly TextBox _txt;
    private readonly DataGridView _grid;
    private readonly DataGridView _mapGrid;
    private readonly Button _btnClose;
    private readonly Button _btnCopy;
    private readonly Button _btnSave;

    /// <summary>
    /// Creates a CHD report viewer window.
    /// </summary>
    /// <param name="title">Base window title.</param>
    public FrmChdVerify(string title)
    {
        _baseTitle = title;
        Text = title;
        Width = 980;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;

        _grid = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = 360,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "Size" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "CRC", DataPropertyName = "CRC" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SHA1", DataPropertyName = "SHA1" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MD5", DataPropertyName = "MD5" });

        _mapGrid = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = 160,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _mapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expected", DataPropertyName = "Expected" });
        _mapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Extracted", DataPropertyName = "Extracted" });
        _mapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", DataPropertyName = "Reason" });

        _txt = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Text = "Running..."
        };

        FlowLayoutPanel buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 4, 8, 4),
            WrapContents = false
        };
        _btnClose = new Button { Text = "Close", Width = 92, Height = 28 };
        _btnClose.Click += (_, _) => Close();
        _btnCopy = new Button { Text = "Copy", Width = 92, Height = 28 };
        _btnCopy.Click += (_, _) =>
        {
            try { Clipboard.SetText(_txt.Text ?? ""); } catch { }
        };
        _btnSave = new Button { Text = "Save...", Width = 92, Height = 28 };
        _btnSave.Click += (_, _) =>
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            sfd.FileName = "chd-report.txt";
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try { System.IO.File.WriteAllText(sfd.FileName, _txt.Text ?? ""); } catch { }
            }
        };
        buttons.Controls.Add(_btnClose);
        buttons.Controls.Add(_btnSave);
        buttons.Controls.Add(_btnCopy);

        Controls.Add(_txt);
        Controls.Add(_mapGrid);
        Controls.Add(_grid);
        Controls.Add(buttons);
    }

    /// <summary>
    /// Updates the window content from a full textual verify report.
    /// </summary>
    /// <param name="text">Report text.</param>
    public void SetText(string text)
    {
        _txt.Text = text ?? "";
        try
        {
            var list = ParseEntries(text);
            _grid.DataSource = list;
            var map = ParseMapping(text);
            _mapGrid.DataSource = map;
        }
        catch
        {
        }
        string mode = TryFindMode(text);
        string desc = TryFindDescriptorSource(text);
        if (!string.IsNullOrWhiteSpace(mode) || !string.IsNullOrWhiteSpace(desc))
        {
            string extra = mode;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                if (extra.Length > 0) extra += ", ";
                extra += desc;
            }
            Text = _baseTitle + " [" + extra + "]";
        }
        else
            Text = _baseTitle;
    }

    /// <summary>
    /// Runs verification for a CHD and shows the report window.
    /// </summary>
    /// <param name="owner">Owning window.</param>
    /// <param name="chdPath">CHD path.</param>
    public static void RunAndShow(IWin32Window owner, string chdPath)
    {
        using FrmChdVerify frm = new FrmChdVerify("Verify CHD");
        frm.Show(owner);
        frm.Refresh();
        int rc = ChdVerify.TryGenerateReport(chdPath, out string report);
        frm.SetText(report);
        if (rc != 0)
            frm.Text = "Verify CHD (errors)";
    }

    /// <summary>
    /// Parses flat report lines into table rows for member hash display.
    /// </summary>
    private static System.Collections.Generic.List<Entry> ParseEntries(string report)
    {
        var items = new System.Collections.Generic.List<Entry>();
        if (string.IsNullOrWhiteSpace(report))
            return items;
        var lines = report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // Expect pattern: "<name> size=<n> crc=<hex> sha1=<hex> md5=<hex>"
            if (line.Contains(" size=") && line.Contains(" crc=") && line.Contains(" sha1="))
            {
                string name = line;
                int sp = name.IndexOf(" size=", StringComparison.OrdinalIgnoreCase);
                if (sp > 0) name = name.Substring(0, sp).Trim();

                string size = GetToken(line, "size=");
                string crc = GetToken(line, "crc=");
                string sha1 = GetToken(line, "sha1=");
                string md5 = GetToken(line, "md5=");
                items.Add(new Entry { Name = name, Size = size, CRC = crc, SHA1 = sha1, MD5 = md5 });
            }
        }
        return items;
    }

    /// <summary>
    /// Parses the "mapping:" block into table rows.
    /// </summary>
    private static System.Collections.Generic.List<ChdVerify.MapRow> ParseMapping(string report)
    {
        var items = new System.Collections.Generic.List<ChdVerify.MapRow>();
        if (string.IsNullOrWhiteSpace(report))
            return items;
        var lines = report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        bool inMap = false;
        foreach (var line in lines)
        {
            if (line.Trim().Equals("mapping:", StringComparison.OrdinalIgnoreCase))
            {
                inMap = true;
                continue;
            }
            if (!inMap) continue;
            var parts = line.Split('|');
            if (parts.Length >= 3)
            {
                items.Add(new ChdVerify.MapRow
                {
                    Expected = parts[0].Trim(),
                    Extracted = parts[1].Trim(),
                    Reason = parts[2].Trim()
                });
            }
        }
        return items;
    }

    private static string GetToken(string line, string token)
    {
        int i = line.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        i += token.Length;
        int e = line.IndexOf(' ', i);
        if (e < 0) e = line.Length;
        return line.Substring(i, e - i).Trim();
    }

    private static string TryFindMode(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
            return "";
        var lines = report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
                return lines[i].Substring(5).Trim();
        }
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("streamMode=", StringComparison.OrdinalIgnoreCase))
                return lines[i].Trim();
        }
        return "";
    }

    private static string TryFindDescriptorSource(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
            return "";
        var lines = report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("descriptorSource=", StringComparison.OrdinalIgnoreCase))
                return lines[i].Trim();
        }
        return "";
    }

    /// <summary>
    /// Row model for CHD member hash output.
    /// </summary>
    private sealed class Entry
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string CRC { get; set; }
        public string SHA1 { get; set; }
        public string MD5 { get; set; }
    }
}
