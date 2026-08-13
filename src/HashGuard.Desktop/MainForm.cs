using System.ComponentModel;
using System.Drawing.Drawing2D;
using HashGuard.Desktop.Models;
using HashGuard.Desktop.Services;
using Humanizer;

namespace HashGuard.Desktop;

public sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(18, 31, 53);
    private static readonly Color Blue = Color.FromArgb(43, 108, 246);
    private static readonly Color PaleBlue = Color.FromArgb(235, 242, 255);
    private static readonly Color Background = Color.FromArgb(244, 247, 251);
    private static readonly Color Border = Color.FromArgb(215, 222, 232);
    private static readonly Color Muted = Color.FromArgb(92, 106, 125);

    private readonly HashService _hashService = new();
    private readonly HistoryStore _historyStore = new();
    private readonly CsvExportService _csvExportService = new();
    private readonly BindingList<HashRecord> _history = [];

    private readonly TextBox _filePath = CreateTextBox();
    private readonly TextBox _sha256 = CreateTextBox(readOnly: true);
    private readonly TextBox _sha512 = CreateTextBox(readOnly: true);
    private readonly TextBox _expectedHash = CreateTextBox();
    private readonly Label _fileMeta = CreateMutedLabel("Choose a file to begin.");
    private readonly Label _verification = CreateMutedLabel("No comparison requested");
    private readonly Label _status = CreateMutedLabel("Ready");
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 8, Visible = false };
    private readonly Button _analyzeButton = CreatePrimaryButton("Calculate checksums");
    private readonly DataGridView _historyGrid = new();
    private CancellationTokenSource? _calculationCancellation;
    private HashResult? _currentResult;

    public MainForm()
    {
        Text = "HashGuard Desktop";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);
        Size = new Size(1120, 790);
        BackColor = Background;
        Font = new Font("Segoe UI", 9.5f);

        Controls.Add(BuildLayout());
        Shown += async (_, _) => await LoadHistoryAsync();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(28, 22, 28, 18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 232));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildFileCard(), 0, 1);
        root.Controls.Add(BuildResultCard(), 0, 2);
        root.Controls.Add(BuildHistoryCard(), 0, 3);
        root.Controls.Add(BuildStatusBar(), 0, 4);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(new Label
        {
            Text = "HashGuard Desktop",
            Font = new Font("Segoe UI Semibold", 22f),
            ForeColor = Navy,
            AutoSize = true,
            Location = new Point(0, 0),
        });
        panel.Controls.Add(new Label
        {
            Text = "Calculate and verify SHA-256 / SHA-512 checksums with an auditable local history.",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Muted,
            AutoSize = true,
            Location = new Point(3, 42),
        });
        var dependencyBadge = new Label
        {
            Text = "Newtonsoft.Json  •  CsvHelper  •  Humanizer",
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = Blue,
            BackColor = PaleBlue,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(730, 8),
        };
        panel.Controls.Add(dependencyBadge);
        panel.Resize += (_, _) => dependencyBadge.Left = panel.ClientSize.Width - dependencyBadge.Width;
        return panel;
    }

    private Control BuildFileCard()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(18, 14, 18, 14),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = CreateSectionLabel("1  Select a file");
        layout.Controls.Add(heading, 0, 0);
        layout.SetColumnSpan(heading, 3);
        _filePath.PlaceholderText = "Select any file to calculate its checksums";
        layout.Controls.Add(_filePath, 0, 1);

        var browse = CreateSecondaryButton("Browse…");
        browse.Click += BrowseForFile;
        layout.Controls.Add(browse, 1, 1);
        _analyzeButton.Click += AnalyzeSelectedFileAsync;
        layout.Controls.Add(_analyzeButton, 2, 1);
        layout.Controls.Add(_fileMeta, 0, 2);
        layout.SetColumnSpan(_fileMeta, 3);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildResultCard()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(18, 14, 18, 14),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = CreateSectionLabel("2  Review and verify");
        layout.Controls.Add(heading, 0, 0);
        layout.SetColumnSpan(heading, 3);
        AddHashRow(layout, "SHA-256", _sha256, 1);
        AddHashRow(layout, "SHA-512", _sha512, 2);

        _expectedHash.PlaceholderText = "Paste an expected SHA-256 or SHA-512 value (optional)";
        _expectedHash.TextChanged += (_, _) => UpdateVerification();
        layout.Controls.Add(CreateMutedLabel("Expected"), 0, 3);
        layout.Controls.Add(_expectedHash, 1, 3);
        var verify = CreateSecondaryButton("Verify");
        verify.Click += (_, _) => UpdateVerification();
        layout.Controls.Add(verify, 2, 3);
        layout.Controls.Add(_verification, 1, 4);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildHistoryCard()
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(18, 12, 18, 14),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateSectionLabel("3  Recent checksum history"), 0, 0);
        var export = CreateSecondaryButton("Export CSV");
        export.Click += ExportHistoryAsync;
        layout.Controls.Add(export, 1, 0);
        var clear = CreateSecondaryButton("Clear history");
        clear.Click += ClearHistoryAsync;
        layout.Controls.Add(clear, 2, 0);

        ConfigureHistoryGrid();
        layout.Controls.Add(_historyGrid, 0, 1);
        layout.SetColumnSpan(_historyGrid, 3);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildStatusBar()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.Controls.Add(_status, 0, 0);
        layout.Controls.Add(_progress, 1, 0);
        return layout;
    }

    private void ConfigureHistoryGrid()
    {
        _historyGrid.Dock = DockStyle.Fill;
        _historyGrid.AutoGenerateColumns = false;
        _historyGrid.DataSource = _history;
        _historyGrid.ReadOnly = true;
        _historyGrid.AllowUserToAddRows = false;
        _historyGrid.AllowUserToDeleteRows = false;
        _historyGrid.AllowUserToResizeRows = false;
        _historyGrid.RowHeadersVisible = false;
        _historyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _historyGrid.BackgroundColor = Color.White;
        _historyGrid.BorderStyle = BorderStyle.None;
        _historyGrid.GridColor = Border;
        _historyGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = PaleBlue,
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 9f),
            SelectionBackColor = PaleBlue,
        };
        _historyGrid.EnableHeadersVisualStyles = false;
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HashRecord.ScannedAt),
            HeaderText = "Scanned",
            Width = 155,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" },
        });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HashRecord.FileName),
            HeaderText = "File",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HashRecord.VerificationStatus),
            HeaderText = "Verification",
            Width = 135,
        });
        _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HashRecord.Sha256),
            HeaderText = "SHA-256",
            Width = 220,
        });
    }

    private static void AddHashRow(TableLayoutPanel layout, string label, TextBox textBox, int row)
    {
        layout.Controls.Add(CreateMutedLabel(label), 0, row);
        layout.Controls.Add(textBox, 1, row);
        var copy = CreateSecondaryButton("Copy");
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                Clipboard.SetText(textBox.Text);
            }
        };
        layout.Controls.Add(copy, 2, row);
    }

    private void BrowseForFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a file to verify",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _filePath.Text = dialog.FileName;
            var info = new FileInfo(dialog.FileName);
            _fileMeta.Text = $"{FormatBytes(info.Length)}  •  Modified {info.LastWriteTime.Humanize()}";
        }
    }

    private async void AnalyzeSelectedFileAsync(object? sender, EventArgs e)
    {
        if (!File.Exists(_filePath.Text))
        {
            MessageBox.Show(this, "Choose an existing file first.", "File required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _calculationCancellation?.Cancel();
        _calculationCancellation = new CancellationTokenSource();
        _analyzeButton.Enabled = false;
        _progress.Visible = true;
        _progress.Value = 0;
        _status.Text = "Calculating checksums…";

        try
        {
            var progress = new Progress<int>(value => _progress.Value = Math.Clamp(value, 0, 100));
            _currentResult = await _hashService.ComputeAsync(_filePath.Text, progress, _calculationCancellation.Token);
            _sha256.Text = _currentResult.Sha256;
            _sha512.Text = _currentResult.Sha512;
            UpdateVerification();

            var record = new HashRecord(
                DateTimeOffset.Now,
                Path.GetFileName(_currentResult.FilePath),
                _currentResult.FilePath,
                _currentResult.FileSizeBytes,
                _currentResult.Sha256,
                _currentResult.Sha512,
                string.IsNullOrWhiteSpace(_expectedHash.Text) ? "Not compared" : _verification.Text);
            _history.Insert(0, record);
            while (_history.Count > 50)
            {
                _history.RemoveAt(_history.Count - 1);
            }

            await _historyStore.SaveAsync(_history);
            _status.Text = "Checksums calculated and saved to local history.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Calculation cancelled.";
        }
        catch (Exception exception)
        {
            _status.Text = "Unable to calculate checksums.";
            MessageBox.Show(this, exception.Message, "HashGuard error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _analyzeButton.Enabled = true;
            _progress.Visible = false;
        }
    }

    private void UpdateVerification()
    {
        if (_currentResult is null || string.IsNullOrWhiteSpace(_expectedHash.Text))
        {
            _verification.Text = "No comparison requested";
            _verification.ForeColor = Muted;
            return;
        }

        if (HashService.MatchesExpected(_expectedHash.Text, _currentResult))
        {
            _verification.Text = "MATCH — the expected checksum is authentic";
            _verification.ForeColor = Color.FromArgb(20, 126, 76);
        }
        else
        {
            _verification.Text = "NO MATCH — the file may differ from the expected copy";
            _verification.ForeColor = Color.FromArgb(190, 55, 55);
        }
    }

    private async Task LoadHistoryAsync()
    {
        foreach (var record in await _historyStore.LoadAsync())
        {
            _history.Add(record);
        }

        _status.Text = _history.Count == 0 ? "Ready — no previous scans found." : $"Loaded {_history.Count} previous scan(s).";
    }

    private async void ExportHistoryAsync(object? sender, EventArgs e)
    {
        if (_history.Count == 0)
        {
            MessageBox.Show(this, "There is no history to export yet.", "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export checksum history",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"HashGuard-History-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await _csvExportService.ExportAsync(dialog.FileName, _history);
            _status.Text = $"Exported {_history.Count} record(s) to {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private async void ClearHistoryAsync(object? sender, EventArgs e)
    {
        if (MessageBox.Show(this, "Clear the local checksum history?", "Confirm clear", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _history.Clear();
        await _historyStore.ClearAsync();
        _status.Text = "Local history cleared.";
    }

    private static Panel CreateCard() => new RoundedPanel(Border)
    {
        Dock = DockStyle.Fill,
        BackColor = Color.White,
        Margin = new Padding(0, 0, 0, 14),
    };

    private static Label CreateSectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Navy,
        Font = new Font("Segoe UI Semibold", 10.5f),
        Anchor = AnchorStyles.Left,
    };

    private static Label CreateMutedLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Muted,
        Anchor = AnchorStyles.Left,
    };

    private static TextBox CreateTextBox(bool readOnly = false) => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = readOnly,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = readOnly ? Color.FromArgb(249, 250, 252) : Color.White,
        Font = new Font(readOnly ? "Cascadia Mono" : "Segoe UI", readOnly ? 8.8f : 9.5f),
        Margin = new Padding(3, 4, 8, 5),
    };

    private static Button CreatePrimaryButton(string text) => new Button
    {
        Text = text,
        Dock = DockStyle.Fill,
        FlatStyle = FlatStyle.Flat,
        BackColor = Blue,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9.5f),
        Cursor = Cursors.Hand,
        Margin = new Padding(8, 2, 0, 4),
    }.WithFlatBorder(Color.Transparent);

    private static Button CreateSecondaryButton(string text) => new Button
    {
        Text = text,
        Dock = DockStyle.Fill,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        ForeColor = Navy,
        Font = new Font("Segoe UI Semibold", 9f),
        Cursor = Cursors.Hand,
        Margin = new Padding(8, 2, 0, 4),
    }.WithFlatBorder(Border);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    private sealed class RoundedPanel : Panel
    {
        private readonly Color _borderColor;

        public RoundedPanel(Color borderColor)
        {
            _borderColor = borderColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            const int radius = 12;
            var rectangle = new Rectangle(0, 0, Width - 1, Height - 1);
            path.AddArc(rectangle.Left, rectangle.Top, radius, radius, 180, 90);
            path.AddArc(rectangle.Right - radius, rectangle.Top, radius, radius, 270, 90);
            path.AddArc(rectangle.Right - radius, rectangle.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            using var pen = new Pen(_borderColor);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

internal static class ButtonExtensions
{
    public static Button WithFlatBorder(this Button button, Color color)
    {
        if (color == Color.Transparent)
        {
            button.FlatAppearance.BorderSize = 0;
        }
        else
        {
            button.FlatAppearance.BorderColor = color;
            button.FlatAppearance.BorderSize = 1;
        }

        return button;
    }
}
