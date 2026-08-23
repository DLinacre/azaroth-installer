using System.Diagnostics;

namespace AzarothInstaller;

public class ComboValue
{
    public string Display;
    public object Value;
    public override string ToString() => Display;
}

public class WizardForm : Form
{
    readonly AppConfig _cfg;
    readonly bool _autoFlag;
    CancellationTokenSource _cts = new();
    readonly object _logLock = new();

    // ------------------------------------------------------------- state
    SystemInfo _sys;
    DriveStat _drive;
    string _installRoot;
    string _repackZipLocal = "";
    ServerLayout _layout;
    DbServerInfo _db;
    WowCandidate _selectedWow;
    bool _dataDone;
    bool _verified;
    string _gmUser, _gmPass;
    InstallSummary _summary;

    // world & options
    WorldOptionsConfig _worldOpts;
    List<ModuleInfo> _modules;
    List<ModuleInfo> _moduleSelection = new();
    List<ExtraModule> _extraModules = new();
    List<ExtraModule> _urlModules = new();
    string _localeChoice = "auto";
    TextBox _realmNameBox;
    ComboBox _localeCombo, _xpCombo, _honorCombo, _goldCombo, _levelCombo;
    ComboBox _botsCombo, _addClassCombo, _maxAddedCombo, _guildsCombo;
    CheckBox _botsAutoCheck, _onlyOnlineCheck, _gmGenieCheck;
    ListView _moduleList;
    TextBox _moduleUrlBox;
    TextBox _gmUserBox, _gmPassBox, _gmCharBox;

    // ------------------------------------------------------------- ui refs
    Panel _content;
    Label _status;
    ProgressBar _progress;
    TextBox _logBox;
    Button _backBtn, _nextBtn, _autoBtn, _cancelBtn;
    Label[] _navLabels;
    readonly string[] _stepTitles =
    {
        "Welcome", "System Check", "Install Location", "Server Core",
        "Data & PlayerBots", "Database", "Game Client", "World & Options",
        "Verify & Finish", "Done"
    };

    // per-step dynamic controls
    TextBox _urlBox, _layoutNotes, _dataStatus, _dbStatus, _verifyResult;
    Label _localZipLabel, _fullPathLabel;
    ComboBox _driveCombo;
    TextBox _folderBox;
    CheckBox _skipData, _forceDb;
    ListView _wowList;
    RadioButton _wowNone;
    Label _welcomeBanner;
    TextBox _summaryBox;
    Label _verifyChecklist;

    int _step = 0;
    bool _busy;
    string _existingInstall;

    public WizardForm(bool autoFlag)
    {
        _cfg = AppConfig.Load(out var loadError);
        _autoFlag = autoFlag;
        _existingInstall = ServerBuilder.FindExistingInstall()?.InstallRoot;

        Text = "Azaroth Core - One-Click Installer";
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Width = 1080;
        Height = 740;
        MinimumSize = new Size(960, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 20, 26);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 9.5f);

        BuildUi();
        ShowStep(0);

        if (_autoFlag)
            Shown += async (s, e) => await RunAutoInstallAsync();
        else if (loadError != null)
            AddLog("Note: " + loadError);
    }

    // ================================================================== ui
    void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));

        // header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        var title = new Label
        {
            Text = "⚔  Azaroth Core — One-Click Installer",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 196, 87),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var ver = new Label
        {
            Text = "AzerothCore 3.3.5a\n+ PlayerBots  |  v1.0",
            ForeColor = Color.Silver,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(ver, 1, 0);

        // body: nav + content
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(24, 27, 36),
            Padding = new Padding(4)
        };
        _navLabels = new Label[_stepTitles.Length];
        for (int i = 0; i < _stepTitles.Length; i++)
        {
            int idx = i;
            var lbl = new Label
            {
                Text = (i + 1) + ".  " + _stepTitles[i],
                AutoSize = false,
                Width = 195,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.FromArgb(32, 35, 46),
                ForeColor = Color.Silver,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 0, 2)
            };
            lbl.Click += (s, e) =>
            {
                if (_busy) return;
                if (idx < _step) ShowStep(idx);
            };
            _navLabels[i] = lbl;
            nav.Controls.Add(lbl);
        }

        _content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12, 2, 12, 2) };

        body.Controls.Add(nav, 0, 0);
        body.Controls.Add(_content, 1, 0);

        // footer
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        _status = new Label
        {
            Text = "Ready.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(160, 220, 160)
        };
        _progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100 };
        topRow.Controls.Add(_status, 0, 0);
        topRow.Controls.Add(_progress, 1, 0);

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(12, 14, 18),
            ForeColor = Color.FromArgb(140, 210, 140),
            Font = new Font("Consolas", 8.5f)
        };

        var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 4, 0, 0) };
        _cancelBtn = MkBtn("✖ Cancel", Color.FromArgb(170, 70, 70));
        _cancelBtn.Click += (s, e) => { if (_busy) { _cts.Cancel(); AddLog("Cancelling..."); } };
        _autoBtn = MkBtn("⚡ Full Auto Install", Color.FromArgb(35, 135, 70));
        _autoBtn.Click += async (s, e) => await RunAutoInstallAsync();
        _nextBtn = MkBtn("Next  ▶", Color.FromArgb(35, 105, 190));
        _nextBtn.Click += (s, e) => OnNext();
        _backBtn = MkBtn("◀  Back", Color.FromArgb(60, 64, 76));
        _backBtn.Click += (s, e) => { if (!_busy && _step > 0) ShowStep(_step - 1); };
        btns.Controls.Add(_cancelBtn);
        btns.Controls.Add(_autoBtn);
        btns.Controls.Add(_nextBtn);
        btns.Controls.Add(_backBtn);

        footer.Controls.Add(topRow, 0, 0);
        footer.Controls.Add(_logBox, 0, 1);
        footer.Controls.Add(btns, 0, 2);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(body, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);

        FormClosing += (s, e) =>
        {
            if (_busy && MessageBox.Show("The installer is still working.\nCancel and close?", "Azaroth Installer",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                e.Cancel = true;
            else
                _cts.Cancel();
        };
    }

    Button MkBtn(string text, Color color)
    {
        var b = new Button
        {
            Text = text,
            Width = 155,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(4, 2, 4, 2),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        _autoBtn.Enabled = !busy;
        _nextBtn.Enabled = !busy;
        _backBtn.Enabled = !busy;
        _cancelBtn.Visible = busy;
        if (busy) _progress.Style = ProgressBarStyle.Marquee;
        else _progress.Style = ProgressBarStyle.Continuous;
    }

    void SetProgress(int pct)
    {
        if (!IsHandleCreated) return;
        BeginInvoke((Action)(() =>
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = Math.Clamp(pct, 0, 100);
        }));
    }

    void SetStatus(string text, Color? color = null)
    {
        if (!IsHandleCreated) return;
        BeginInvoke((Action)(() =>
        {
            _status.Text = text;
            if (color.HasValue) _status.ForeColor = color.Value;
        }));
    }

    public void AddLog(string line)
    {
        var entry = DateTime.Now.ToString("HH:mm:ss") + "  " + line;
        lock (_logLock)
        {
            try
            {
                if (!IsHandleCreated) return;
                if (!_logBox.InvokeRequired) AppendLog(entry);
                else _logBox.Invoke(new Action(() => AppendLog(entry)));
            }
            catch { }
        }
    }

    void AppendLog(string entry)
    {
        _logBox.AppendText(entry + Environment.NewLine);
        while (_logBox.TextLength > 120000) _logBox.Clear();
    }

    // ================================================================ steps
    void ShowStep(int step)
    {
        _step = step;
        _content.Controls.Clear();

        for (int i = 0; i < _navLabels.Length; i++)
        {
            _navLabels[i].Text = (i < step ? "✓ " : (i + 1) + ".  ") + _stepTitles[i];
            _navLabels[i].ForeColor = i == step ? Color.FromArgb(255, 200, 85)
                : (i < step ? Color.FromArgb(130, 215, 130) : Color.FromArgb(160, 165, 180));
            _navLabels[i].BackColor = i == step ? Color.FromArgb(48, 54, 72)
                : (i < step ? Color.FromArgb(26, 32, 44) : Color.FromArgb(24, 27, 36));
        }

        _autoBtn.Visible = step == 0;
        _nextBtn.Visible = step > 0 && step < _stepTitles.Length - 1;
        _backBtn.Visible = step > 0;
        _nextBtn.Text = step == _stepTitles.Length - 2 ? "Finish  ▶" : "Next  ▶";

        switch (step)
        {
            case 0: BuildWelcome(); break;
            case 1: BuildSystemCheck(); break;
            case 2: BuildLocation(); break;
            case 3: BuildCore(); break;
            case 4: BuildDataBots(); break;
            case 5: BuildDatabase(); break;
            case 6: BuildClient(); break;
            case 7: BuildWorldOptions(); break;
            case 8: BuildVerify(); break;
            case 9: BuildDone(); break;
        }
        if (!string.IsNullOrEmpty(_existingInstall) && step == 1)
            AddLog("Existing Azaroth install detected at " + _existingInstall + " - it will be reused/repaired.");
    }

    // ------------------------------------------------------------- step 0
    void BuildWelcome()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var h = new Label
        {
            Text = "Welcome to the Azaroth Core one-click installer",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill
        };

        var what = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            Text =
                "This wizard installs everything for a private Azaroth (AzerothCore 3.3.5a) world with PlayerBots:\n\n" +
                "  •  Checks your CPU, GPU, RAM and drives, and picks the best install drive\n" +
                "  •  Finds an existing World of Warcraft 3.3.5 client on your PC (no re-download if it can avoid it)\n" +
                "  •  Finds or installs the database (reuses a local MySQL/MariaDB or the one bundled with the server)\n" +
                "  •  Downloads the server + PlayerBots, game data, and configures everything automatically\n" +
                "  •  Creates a GM account, Start/Play shortcuts, and verifies the server actually boots"
        };

        var legal = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(190, 170, 120),
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            Text =
                "Before you start:\n" +
                "  – This is for private / personal use. Running a private WoW server and using bots can violate\n" +
                "    Blizzard's Terms of Use - you are responsible for how you use this software.\n" +
                "  – The server emulator (AzerothCore) is open source; game client files are NOT redistributed here.\n" +
                "    The wizard uses your own WoW 3.3.5 client if it finds one on this PC.\n" +
                "  – You need an internet connection for downloads (server files + game data can be ~1.5-2 GB)."
        };

        _welcomeBanner = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(255, 196, 87),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = false,
            Text = _existingInstall == null ? "" :
                "✔ An existing Azaroth install was found at:  " + _existingInstall +
                "\n  The wizard will reuse its database and files where possible (repair mode)."
        };

        p.Controls.Add(h, 0, 0);
        p.Controls.Add(what, 0, 1);
        p.Controls.Add(legal, 0, 2);
        p.Controls.Add(_welcomeBanner, 0, 3);
        _content.Controls.Add(p);
    }

    // ------------------------------------------------------------- step 1
    void BuildSystemCheck()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.Controls.Add(Heading("System check — what does this PC have?"), 0, 0);
        var info = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 35, 44), ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 9f), Text = _sys == null ? "Scanning hardware..." : FormatSysInfo()
        };
        p.Controls.Add(info, 0, 1);
        _content.Controls.Add(p);

        if (_sys == null && !_busy)
            _ = RunStepActionAsync(async () =>
            {
                _sys = await Task.Run(() => SysProbe.GetSystemInfo(AddLog));
                if (_sys == null) throw new Exception("System scan failed.");
                if (IsHandleCreated) { _content.Controls.Clear(); ShowStep(1); }
            });
    }

    string FormatSysInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"CPU      : {_sys.CpuName}  ({_sys.PhysicalCores} cores / {_sys.LogicalCores} threads)");
        sb.AppendLine($"RAM      : {_sys.RamGb}");
        foreach (var g in _sys.Gpus)
            sb.AppendLine($"GPU      : {g.Name}" + (g.VideoBytes > 0 ? $"  ({g.VideoBytes / 1073741824.0:0.#} GB VRAM)" : "") + (string.IsNullOrEmpty(g.DriverVersion) ? "" : $"  driver {g.DriverVersion}"));
        sb.AppendLine($"OS       : {_sys.OsVersion}  ({(_sys.Is64Bit ? "64-bit" : "32-bit")})");
        sb.AppendLine();
        sb.AppendLine("Drives:");
        foreach (var d in _sys.Drives)
            sb.AppendLine($"   {d.Root}  {d.FreeText}  {d.Label}  {(d.IsSystem ? "[system]" : "")}");
        sb.AppendLine();
        if (_sys.Warnings.Count > 0)
        {
            sb.AppendLine("NOTES:");
            foreach (var w in _sys.Warnings) sb.AppendLine("   ⚠ " + w);
        }
        else
            sb.AppendLine("Your system looks good for running Azaroth Core.");
        return sb.ToString();
    }

    // ------------------------------------------------------------- step 2
    void BuildLocation()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.Controls.Add(Heading("Where should Azaroth Core be installed?"), 0, 0);

        var cardBox = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(6) };
        cardBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        cardBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        cardBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        cardBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        _driveCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        if (_sys != null)
        {
            foreach (var d in _sys.Drives)
                _driveCombo.Items.Add($"{d.Root}   -   {d.FreeText}" + (d.IsSystem ? "  [system]" : ""));
        }
        SelectBestDrive();
        cardBox.Controls.Add(Row("Drive (auto-picked most free space):", _driveCombo), 0, 0);

        _folderBox = new TextBox { Dock = DockStyle.Fill, Text = _cfg.InstallFolderName };
        cardBox.Controls.Add(Row("Folder name:", _folderBox), 0, 1);

        _fullPathLabel = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(160, 220, 160), Text = "", TextAlign = ContentAlignment.MiddleLeft };
        cardBox.Controls.Add(Row("Full target path:", _fullPathLabel), 0, 2);

        var spaceOk = new Label { Dock = DockStyle.Fill, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        cardBox.Controls.Add(Row("Free space requirement (" + _cfg.MinFreeSpaceGB + " GB):", spaceOk), 0, 3);

        void Refresh()
        {
            if (_driveCombo == null || _driveCombo.SelectedItem == null || _sys == null) return;
            var d = _sys.Drives[Convert.ToInt32(_driveCombo.SelectedIndex)];
            var folder = string.IsNullOrWhiteSpace(_folderBox.Text) ? _cfg.InstallFolderName : _folderBox.Text.Trim();
            _installRoot = Path.Combine(d.Root, folder);
            _fullPathLabel.Text = _installRoot;
            bool ok = d.FreeBytes >= _cfg.MinFreeSpaceGB * 1073741824;
            spaceOk.Text = ok ? "✓ " + d.FreeText + " — plenty of space" : "✗ only " + d.FreeText + " free — not enough!";
            spaceOk.ForeColor = ok ? Color.FromArgb(130, 220, 130) : Color.OrangeRed;
        }
        _driveCombo.SelectedIndexChanged += (s, e) => Refresh();
        _folderBox.TextChanged += (s, e) => Refresh();
        Refresh();

        p.Controls.Add(Card("Target Drive & Directory", cardBox), 0, 1);

        var tipLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Silver,
            Padding = new Padding(4, 12, 4, 4),
            Text = "💡 Tip: A dedicated game or data drive (other than C:) is ideal for server files and database storage.\n" +
                   "Your existing WoW game client will NOT be moved or modified."
        };
        p.Controls.Add(tipLabel, 0, 2);
        _content.Controls.Add(p);
    }

    void SelectBestDrive()
    {
        if (_driveCombo == null || _sys == null) return;
        var best = _sys.Drives
            .OrderByDescending(d => d.FreeBytes)
            .ThenBy(d => d.IsSystem ? 0 : 1)
            .FirstOrDefault();
        if (best == null) return;
        // prefer the existing install's drive when repairing
        if (_existingInstall != null)
        {
            var root = new DirectoryInfo(_existingInstall).Root?.FullName;
            var match = _sys.Drives.FirstOrDefault(d => d.Root.TrimEnd('\\') == root?.TrimEnd('\\'));
            if (match != null) best = match;
        }
        _drive = best;
        int idx = _sys.Drives.ToList().FindIndex(d => d == best);
        if (idx >= 0) _driveCombo.SelectedIndex = idx;
    }

    // ------------------------------------------------------------- step 3
    void BuildCore()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        p.Controls.Add(Heading("Server core — Azaroth Core + PlayerBots"), 0, 0);

        _urlBox = new TextBox { Dock = DockStyle.Fill };
        _urlBox.Text = _cfg.Downloads.ServerRepack?.Urls?.FirstOrDefault() ?? "";
        p.Controls.Add(Row("Direct download URL (optional):", _urlBox), 0, 1);

        if (string.IsNullOrEmpty(_repackZipLocal))
        {
            var candidates = ServerBuilder.FindLocalRepackZips(_installRoot);
            if (candidates.Count > 0) _repackZipLocal = candidates[0];
        }

        var browse = new Button { Text = "Browse for .zip ...", Width = 175, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 64, 76), ForeColor = Color.White };
        _localZipLabel = new Label { Dock = DockStyle.Fill, ForeColor = Color.Silver, Text = string.IsNullOrEmpty(_repackZipLocal) ? "(none yet - a local zip works too)" : _repackZipLocal, AutoEllipsis = true };
        var browseRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        browseRow.Controls.Add(browse);
        browseRow.Controls.Add(_localZipLabel);
        browse.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Zip archives (*.zip)|*.zip|All files (*.*)|*.*", Title = "Pick the Azaroth Core / repack zip" };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _repackZipLocal = ofd.FileName;
                _localZipLabel.Text = _repackZipLocal;
            }
        };
        p.Controls.Add(Row("Or pick a local zip file:", browseRow), 0, 2);

        var prepare = new Button
        {
            Text = _layout == null ? "⬇  Download & Prepare Server" : "↻  Re-prepare Server",
            Height = 36, Width = 280, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 90, 160), ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 2)
        };
        prepare.Click += (s, e) => _ = RunStepActionAsync(async () =>
        {
            _layout = null;
            await PrepareCoreAsync();
            if (IsHandleCreated) { _content.Controls.Clear(); ShowStep(3); }
        });
        var prepRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        prepRow.Controls.Add(prepare);
        p.Controls.Add(prepRow, 0, 3);

        _layoutNotes = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 35, 44), ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 9f),
            Text = _layout == null ? "Nothing yet - click 'Download & Prepare Server'." : FormatLayout()
        };
        p.Controls.Add(_layoutNotes, 0, 4);

        p.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.Silver,
            Text = "No direct link? Pick the zip on disk (your Azaroth release, OwnedCore or DrePack repack, ...).\n" +
                   "The wizard accepts any prebuilt WINDOWS AzerothCore + PlayerBots zip and figures out its layout."
        }, 0, 5);
        _content.Controls.Add(p);
    }

    string FormatLayout()
    {
        if (_layout == null) return "";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Server files:      " + _layout.ServerDir);
        sb.AppendLine("worldserver.exe:   " + (File.Exists(_layout.WorldserverExe) ? "found" : "MISSING"));
        sb.AppendLine("authserver.exe:    " + (File.Exists(_layout.AuthserverExe) ? "found" : "not found"));
        sb.AppendLine("realmserver.exe:   " + (File.Exists(_layout.RealmserverExe) ? "found" : "not found"));
        sb.AppendLine("Game data (data/): " + (_layout.HasData ? "present - no download needed" : "MISSING - next step will download it"));
        sb.AppendLine("SQL dumps found:   " + _layout.SqlFiles.Count);
        sb.AppendLine("MySQL bundled:     " + (string.IsNullOrEmpty(_layout.BundledMysqld) ? "no" : "yes" + (_layout.BundledDatadirPopulated ? " (with prebuilt database)" : " (needs initialization)")));
        sb.AppendLine("PlayerBots conf:   " + (string.IsNullOrEmpty(_layout.BundledPlayerbotsConf) ? "not in repack (wizard will fetch it)" : "present"));
        foreach (var n in _layout.Notes) sb.AppendLine("  • " + n);
        return sb.ToString();
    }

    async Task PrepareCoreAsync()
    {
        if (string.IsNullOrEmpty(_installRoot)) throw new Exception("Choose an install location first.");
        var b = NewBuilder();
        SetStatus("Preparing server core...");

        if (string.IsNullOrWhiteSpace(_repackZipLocal) &&
            string.IsNullOrWhiteSpace(_urlBox?.Text) &&
            (_cfg.Downloads.ServerRepack?.Urls == null || _cfg.Downloads.ServerRepack.Urls.Count == 0))
        {
            var candidates = ServerBuilder.FindLocalRepackZips(_installRoot);
            if (candidates.Count > 0)
            {
                _repackZipLocal = candidates[0];
            }
            else
            {
                MessageBox.Show(this,
                    "Please pick your AzerothCore / PlayerBots server .zip file using the 'Browse for .zip ...' button, " +
                    "or paste a direct download URL into the text box above.",
                    "Azaroth Installer - Repack Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("Please select a repack .zip file or paste a download URL.", Color.OrangeRed);
                return;
            }
        }

        var zip = await b.GetRepackZipAsync(_repackZipLocal, _urlBox?.Text);
        _layout = await Task.Run(() => b.PrepareLayout(zip));
        SetStatus("Server core prepared: " + _layout.ServerDir, Color.FromArgb(120, 220, 120));
    }

    // ------------------------------------------------------------- step 4
    void BuildDataBots()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        p.Controls.Add(Heading("Game data & PlayerBots"), 0, 0);

        var run = new Button
        {
            Text = "⬇  Prepare Data & PlayerBots",
            Height = 34, Width = 260, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 90, 160), ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        run.Click += (s, e) => _ = RunStepActionAsync(async () =>
        {
            _dataDone = false;
            await PrepareDataBotsAsync();
            if (IsHandleCreated) { _content.Controls.Clear(); ShowStep(4); }
        });
        var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _skipData = new CheckBox
        {
            Text = "skip data download (I will provide data/ myself)",
            ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(10, 10, 20, 0)
        };
        _skipData.CheckedChanged += (s, e) => run.Enabled = !_busy;
        row1.Controls.Add(run);
        row1.Controls.Add(_skipData);
        p.Controls.Add(row1, 0, 1);

        p.Controls.Add(Heading("Status", small: true), 0, 2);
        _dataStatus = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 35, 44), ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 9f),
            Text = _dataDone ? "✓ Game data is ready.\n✓ PlayerBots configuration is ready." : "Not prepared yet - click the button above."
        };
        p.Controls.Add(_dataStatus, 0, 3);
        p.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.Silver,
            Text = "Game data (dbc/maps/vmaps/mmaps) is ~1.2 GB and only downloaded when the repack does not include it.\n" +
                   "PlayerBots = the mod that spawns bot players (randombots, AddClass pool, altbots) in your world."
        }, 0, 4);
        _content.Controls.Add(p);
    }

    async Task PrepareDataBotsAsync()
    {
        if (_layout == null) throw new Exception("Prepare the server core first (previous step).");
        var b = NewBuilder();
        var lines = new List<string>();
        if (_skipData?.Checked == true)
        {
            lines.Add("Data download skipped by user choice.");
            _dataDone = _layout.HasData;
        }
        else
        {
            SetStatus("Checking/downloading game data...");
            await Task.Run(async () => await b.EnsureGameDataAsync(_layout));
            _dataDone = _layout.HasData;
            lines.Add(_layout.HasData ? "✓ Game data ready (download skipped - already present, or just downloaded)."
                                      : "✗ Game data still missing - the world server will NOT start until a data/ folder exists.");
        }
        SetStatus("Checking PlayerBots...");
        await Task.Run(async () => await b.EnsurePlayerBotsAsync(_layout));
        lines.Add("✓ PlayerBots module configuration done (Mod_PlayerBots added to worldserver.conf on the verify step).");
        _dataStatus.Text = string.Join(Environment.NewLine, lines);
        SetStatus(_dataDone ? "Data & PlayerBots ready." : "Game data missing - see details.", _dataDone ? null : Color.OrangeRed);
    }

    // ------------------------------------------------------------- step 5
    void BuildDatabase()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.Controls.Add(Heading("Database (MySQL / MariaDB)"), 0, 0);

        var actionBox = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(6) };
        var run = new Button
        {
            Text = "🗄  Set Up Database",
            Height = 36, Width = 220, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(35, 105, 190), ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand, Margin = new Padding(0, 2, 16, 2)
        };
        run.FlatAppearance.BorderSize = 0;
        run.Click += (s, e) => _ = RunStepActionAsync(async () =>
        {
            _db = null;
            await SetupDbAsync(_forceDb?.Checked == true);
            if (IsHandleCreated) { _content.Controls.Clear(); ShowStep(5); }
        });

        _forceDb = new CheckBox
        {
            Text = "re-import database files (resets characters - only for broken installs)",
            ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(0, 10, 0, 0)
        };

        actionBox.Controls.Add(run);
        actionBox.Controls.Add(_forceDb);
        p.Controls.Add(Card("Database Actions", actionBox), 0, 1);

        _dbStatus = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 25, 34), ForeColor = _db != null ? Color.FromArgb(140, 215, 140) : Color.Gainsboro,
            Font = new Font("Consolas", 9.5f),
            Text = _db == null ? "Not set up yet — click 'Set Up Database' above to detect or initialize MySQL/MariaDB." :
                $"Source    : {_db.Source}\r\nLogin     : {_db.Login}\r\nPassword  : {_db.Password}\r\nDatabases : {_cfg.Database.AuthDb} / {_cfg.Database.CharactersDb} / {_cfg.Database.WorldDb}\r\n\r\n{_db.Note}"
        };
        p.Controls.Add(Card("Database Status & Connection Info", _dbStatus), 0, 2);
        _content.Controls.Add(p);
    }

    async Task SetupDbAsync(bool forceFresh)
    {
        if (_layout == null) throw new Exception("Prepare the server core first (previous step).");
        var b = NewBuilder();
        SetStatus("Resolving database (bundled -> local -> fresh install)...");
        _db = await Task.Run(() => b.ResolveDatabase(_layout, forceFresh));
        SetStatus("Database ready: " + _db.Source, Color.FromArgb(120, 220, 120));
    }

    // ------------------------------------------------------------- step 6
    void BuildClient()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        p.Controls.Add(Heading("World of Warcraft client (3.3.5a)"), 0, 0);

        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var rescan = new Button { Text = "🔍  Rescan my PC", Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 64, 76), ForeColor = Color.White };
        var browse = new Button { Text = "Point to a folder ...", Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 64, 76), ForeColor = Color.White };
        row.Controls.Add(rescan, 0, 0);
        row.Controls.Add(browse, 1, 0);
        p.Controls.Add(row, 0, 1);

        _wowList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.FromArgb(32, 35, 44),
            ForeColor = Color.Gainsboro,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _wowList.Columns.Add("Client folder", 560);
        _wowList.Columns.Add("Score", 60);
        _wowList.Columns.Add("Details", 220);
        p.Controls.Add(_wowList, 0, 2);

        _wowNone = new RadioButton { Text = "No client on this PC - install the server only (I'll add the client later)", ForeColor = Color.Silver, AutoSize = true };
        p.Controls.Add(_wowNone, 0, 3);
        _wowList.SelectedIndexChanged += (s, e) => { if (_wowList.SelectedItems.Count > 0) _wowNone.Checked = false; };
        _wowNone.CheckedChanged += (s, e) => { if (_wowNone.Checked) _wowList.SelectedItems.Clear(); };

        p.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.Silver,
            Text = "The wizard searched your registry and all drives for a 3.3.5a client (wow.exe + Data + Interface + WTF).\n" +
                   "If it finds one, the server is wired to it automatically - nothing is moved or modified."
        }, 0, 4);
        _content.Controls.Add(p);

        rescan.Click += (s, e) => _ = RunStepActionAsync(async () =>
        {
            await ScanWowAsync();
            if (IsHandleCreated) { _content.Controls.Clear(); ShowStep(6); }
        });
        browse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog { Description = "Select the folder that contains wow.exe" };
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                var c = new WowCandidate { Path = fbd.SelectedPath };
                try
                {
                    c.HasWowExe = File.Exists(Path.Combine(fbd.SelectedPath, "wow.exe"));
                    foreach (var sub in Directory.EnumerateDirectories(fbd.SelectedPath))
                    {
                        var n = Path.GetFileName(sub).ToLowerInvariant();
                        if (n == "data") c.HasData = true;
                        if (n == "interface") c.HasInterface = true;
                        if (n == "wtf") c.HasWtf = true;
                    }
                    c.Score = (c.HasWowExe ? 4 : 0) + (c.HasData ? 2 : 0) + (c.HasInterface ? 2 : 0) + (c.HasWtf ? 1 : 0);
                }
                catch { }
                _wowList.Items.Clear();
                AddWowItem(c, true);
            }
        };

        if (_cfg.WowClient.AutoScan && !_busy)
            _ = ScanWowAsync();
    }

    async Task ScanWowAsync()
    {
        SetStatus("Scanning this PC for a WoW 3.3.5 client...");
        var extra = _cfg.WowClient.ExtraScanDirs ?? new List<string>();
        var found = await Task.Run(() => SysProbe.ScanForWoW(AddLog, extra));
        if (IsHandleCreated)
        {
            _wowList?.Items.Clear();
            foreach (var c in found.Take(12))
                AddWowItem(c, false);
            // keep a sensible selection
            if (_selectedWow == null && found.Count > 0)
            {
                var best = found.FirstOrDefault(c => !c.LooksModern) ?? found[0];
                _selectedWow = best;
            }
            if (found.Count == 0)
                SetStatus("No 3.3.5 client found on this PC.", Color.OrangeRed);
            else
                SetStatus(found.Count + " client candidate(s) found.", Color.FromArgb(120, 220, 120));
        }
    }

    void AddWowItem(WowCandidate c, bool selected)
    {
        var item = new ListViewItem(new[] { c.Path, c.Score.ToString(), c.Hint });
        _wowList.Items.Add(item);
        if (selected) item.Selected = true;
        item.Tag = c;
    }

    void CommitWowSelection()
    {
        if (_wowNone != null && _wowNone.Checked) { _selectedWow = null; return; }
        if (_wowList != null && _wowList.SelectedItems.Count > 0)
            _selectedWow = _wowList.SelectedItems[0].Tag as WowCandidate;
    }

    // ------------------------------------------------------------- step 7 (world & options)
    void BuildWorldOptions()
    {
        var b = NewBuilder();
        _modules = _layout != null ? b.DetectModules(_layout) : new List<ModuleInfo>();

        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 135));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        p.Controls.Add(Heading("World & Options — make the realm yours"), 0, 0);

        // ------------------------------------------------ realm card
        var realm = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        realm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        realm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        realm.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        realm.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        realm.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _realmNameBox = new TextBox { Dock = DockStyle.Fill, Text = _cfg.World.RealmName, MaxLength = 15 };
        _localeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var l in new[] { "auto", "enUS", "enGB", "frFR", "deDE", "esES", "esMX", "zhCN", "zhTW", "koKR", "ptBR", "ruRU" })
            _localeCombo.Items.Add(l);
        _localeCombo.SelectedItem = _cfg.World.ClientLocale;
        if (_localeCombo.SelectedIndex < 0) _localeCombo.SelectedIndex = 0;
        realm.Controls.Add(Row2("Realm name (character select):", _realmNameBox), 0, 0);
        realm.Controls.Add(Row2("Client locale (realmlist/data):", _localeCombo), 1, 0);
        realm.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(255, 205, 92),
            Text = "⚔ Game version: 3.3.5a — Wrath of the Lich King.\n" +
                   "This is the version AzerothCore supports - all WotLK content (incl. ICC) is available on your realm.",
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        realm.SetColumnSpan(realm.Controls[realm.Controls.Count - 1], 2);
        p.Controls.Add(Card("Realm identity", realm), 0, 1);

        // ------------------------------------------------ rates card
        var rates = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        rates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        rates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        rates.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        rates.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _xpCombo = RateCombo(_cfg.World.XpRate, 1, 2, 5, 10, 25, 50, 100);
        _honorCombo = RateCombo(_cfg.World.HonorRate, 1, 2, 3, 5, 10);
        _goldCombo = RateCombo(_cfg.World.GoldRate, 1, 2, 5, 10, 20);
        _levelCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var v in new[] { 80, 70, 60, 50 })
            _levelCombo.Items.Add(new ComboValue { Display = v == 80 ? "80 (max)" : v.ToString(), Value = v });
        _levelCombo.SelectedItem = _levelCombo.Items.Cast<ComboValue>().FirstOrDefault(i => (int)i.Value == _cfg.World.LevelCap)
            ?? _levelCombo.Items[0];
        rates.Controls.Add(Row2("Experience rate:", _xpCombo), 0, 0);
        rates.Controls.Add(Row2("Honor rate:", _honorCombo), 1, 0);
        rates.Controls.Add(Row2("Gold rate:", _goldCombo), 0, 1);
        rates.Controls.Add(Row2("Level cap:", _levelCombo), 1, 1);
        p.Controls.Add(Card("Progression & economy", rates), 0, 2);

        // ------------------------------------------------ bots card
        var bots = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        bots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++) bots.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        _botsCombo = NumberCombo(new[] { 10, 25, 50, 100, 250, 500, 1000 }, _cfg.World.RandomBots);
        _botsAutoCheck = new CheckBox { Text = "auto-login bots on server start", Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, Checked = _cfg.World.BotsAutologin };
        _addClassCombo = NumberCombo(new[] { 10, 25, 50, 100 }, _cfg.World.AddClassPool);
        _maxAddedCombo = NumberCombo(new[] { 20, 40, 80, 160 }, _cfg.World.MaxAddedBots);
        _guildsCombo = NumberCombo(new[] { 0, 5, 10, 20, 30 }, _cfg.World.BotGuilds);
        _onlyOnlineCheck = new CheckBox { Text = "bots only while a player is online", Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, Checked = _cfg.World.BotsOnlyWhenPlayerOnline };
        bots.Controls.Add(Row2("Random bots in the world:", _botsCombo), 0, 0);
        bots.Controls.Add(Row2("Auto-login:", _botsAutoCheck), 1, 0);
        bots.Controls.Add(Row2("AddClass pool (instant raid members):", _addClassCombo), 0, 1);
        bots.Controls.Add(Row2("Max summonable bots:", _maxAddedCombo), 1, 1);
        bots.Controls.Add(Row2("Bot guilds:", _guildsCombo), 0, 2);
        bots.Controls.Add(Row2("Only when I'm online:", _onlyOnlineCheck), 1, 2);
        p.Controls.Add(Card("PlayerBots — fill your world with living players", bots), 0, 3);

        // ------------------------------------------------ modules card
        var mods = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        mods.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        mods.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        mods.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        _moduleList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            CheckBoxes = true,
            GridLines = true,
            BackColor = Color.FromArgb(32, 35, 44),
            ForeColor = Color.Gainsboro
        };
        _moduleList.Columns.Add("Module", 240);
        _moduleList.Columns.Add("File", 320);
        _moduleList.Columns.Add("Status", 100);
        foreach (var m in _modules)
        {
            var it = new ListViewItem(m.Friendly);
            it.SubItems.Add(Path.GetFileName(m.Path));
            it.SubItems.Add(m.Enabled ? "enabled" : "disabled");
            it.Tag = m;
            it.Checked = m.Enabled;
            _moduleList.Items.Add(it);
        }
        if (_modules.Count == 0)
        {
            var it = new ListViewItem("(no extra module DLLs found in this repack - the core always runs)");
            it.SubItems.Add(""); it.SubItems.Add("");
            it.Checked = false;
            _moduleList.Items.Add(it);
        }
        var extraRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        extraRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        extraRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        extraRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        var extraLabel = new Label { Text = "Add a prebuilt module (direct .dll / .zip link):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Silver };
        _moduleUrlBox = new TextBox { Dock = DockStyle.Fill };
        var addBtn = new Button { Text = "Add", Width = 70, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 64, 76), ForeColor = Color.White };
        addBtn.Click += (s, e) =>
        {
            var url = _moduleUrlBox.Text.Trim();
            if (url.Length > 5)
            {
                _urlModules.Add(new ExtraModule { Name = "custom module", Url = url });
                _moduleUrlBox.Clear();
                var label2 = url.Length > 50 ? "⬇ " + url.Substring(0, 50) + "…" : "⬇ " + url;
                var it = new ListViewItem(label2);
                it.SubItems.Add("(downloaded at verify)"); it.SubItems.Add("will install");
                it.Checked = true;
                it.Tag = new ModuleInfo { Name = "extra", Friendly = "extra module", Enabled = true, Path = url };
                _moduleList.Items.Add(it);
            }
        };
        extraRow.Controls.Add(extraLabel, 0, 0);
        extraRow.Controls.Add(_moduleUrlBox, 1, 0);
        extraRow.Controls.Add(addBtn, 2, 0);
        var gmRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        _gmGenieCheck = new CheckBox
        {
            Text = "GM Genie — GM tools add-on installed into your WoW client (recommended for GMs)",
            ForeColor = Color.Gainsboro, AutoSize = true, Margin = new Padding(4, 8, 4, 4),
            Checked = _cfg.World.GmGenieAddon,
            Enabled = _selectedWow != null
        };
        if (_selectedWow == null)
            _gmGenieCheck.Text = "GM Genie — GM tools add-on (needs a WoW client - select one in the Game Client step)";
        gmRow.Controls.Add(_gmGenieCheck);
        mods.Controls.Add(_moduleList, 0, 0);
        mods.Controls.Add(extraRow, 0, 1);
        mods.Controls.Add(gmRow, 0, 2);
        p.Controls.Add(Card("Server modules (checked = enabled in your world)", mods), 0, 4);

        // ------------------------------------------------ GM + commands card
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

        var gm = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        for (int i = 0; i < 3; i++) gm.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        _gmUserBox = new TextBox { Dock = DockStyle.Fill, Text = _cfg.Server.GmUsername };
        _gmPassBox = new TextBox { Dock = DockStyle.Fill, Text = _cfg.Server.GmPassword };
        _gmCharBox = new TextBox { Dock = DockStyle.Fill, Text = _cfg.Server.GmCharacterName, MaxLength = 12 };
        gm.Controls.Add(Row2("GM account:", _gmUserBox), 0, 0);
        gm.Controls.Add(Row2("GM password:", _gmPassBox), 0, 1);
        gm.Controls.Add(Row2("GM character:", _gmCharBox), 0, 2);
        bottom.Controls.Add(gm, 0, 0);

        var cmds = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 26, 32), ForeColor = Color.FromArgb(170, 210, 255),
            Font = new Font("Consolas", 8.5f),
            Text =
                "Instant raid — in game, in your raid channel or chat:\n" +
                "  lfg            a bot joins your raid, filling tank/healer/DPS\n" +
                "  lfg 25         same, targeting a 25-man raid\n" +
                "  .playerbots bot addclass warrior   summon a geared bot of any class\n" +
                "  .playerbots bot add [name1,name2]  log your alts in as bots\n" +
                "  whisper to a bot: summon · follow · attack · grind · release\n" +
                "  .playerbots             full command list in game"
        };
        bottom.Controls.Add(cmds, 1, 0);
        bottom.SetRowSpan(cmds, 2);

        p.Controls.Add(bottom, 0, 5);
        _content.Controls.Add(p);

        _extraModules = _cfg.World.ExtraModules?.Where(em => !string.IsNullOrWhiteSpace(em.Url)).ToList() ?? new List<ExtraModule>();
    }

    Control Card(string title, Control body)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 2, 0, 6) };
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var lbl = new Label
        {
            Text = "  " + title,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(255, 200, 85),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(34, 38, 50),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
        body.Margin = new Padding(0, 4, 0, 0);
        t.Controls.Add(lbl, 0, 0);
        t.Controls.Add(body, 0, 1);
        return t;
    }

    Control Row2(string label, Control control)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(2) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        var lbl = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Silver };
        t.Controls.Add(lbl, 0, 0);
        t.Controls.Add(control, 1, 0);
        return t;
    }

    ComboBox RateCombo(double current, params double[] values)
    {
        var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var v in values)
            c.Items.Add(new ComboValue { Display = v == Math.Floor(v) ? ((long)v) + "×" : v + "×", Value = v });
        c.SelectedItem = c.Items.Cast<ComboValue>().FirstOrDefault(i => Math.Abs((double)i.Value - current) < 0.001)
            ?? c.Items.Cast<ComboValue>().FirstOrDefault(i => Math.Abs((double)i.Value - 1) < 0.001)
            ?? c.Items[0];
        return c;
    }

    ComboBox NumberCombo(int[] values, int current)
    {
        var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var v in values)
            c.Items.Add(new ComboValue { Display = v.ToString(), Value = v });
        c.SelectedItem = c.Items.Cast<ComboValue>().FirstOrDefault(i => (int)i.Value == current) ?? c.Items[0];
        return c;
    }

    void CommitWorldOptions()
    {
        double ComboDouble(ComboBox c, double fallback)
            => c != null && c.SelectedItem is ComboValue cv && cv.Value is double d ? d : fallback;
        int ComboInt(ComboBox c, int fallback)
            => c != null && c.SelectedItem is ComboValue cv2 && cv2.Value is int i ? i : fallback;

        _worldOpts = new WorldOptionsConfig
        {
            RealmName = string.IsNullOrWhiteSpace(_realmNameBox?.Text) ? _cfg.World.RealmName : _realmNameBox.Text.Trim(),
            ClientLocale = _localeCombo?.SelectedItem?.ToString() ?? "auto",
            XpRate = ComboDouble(_xpCombo, _cfg.World.XpRate),
            HonorRate = ComboDouble(_honorCombo, _cfg.World.HonorRate),
            GoldRate = ComboDouble(_goldCombo, _cfg.World.GoldRate),
            LevelCap = ComboInt(_levelCombo, _cfg.World.LevelCap),
            RandomBots = ComboInt(_botsCombo, _cfg.World.RandomBots),
            BotsAutologin = _botsAutoCheck?.Checked ?? true,
            AddClassPool = ComboInt(_addClassCombo, _cfg.World.AddClassPool),
            MaxAddedBots = ComboInt(_maxAddedCombo, _cfg.World.MaxAddedBots),
            BotGuilds = ComboInt(_guildsCombo, _cfg.World.BotGuilds),
            BotsOnlyWhenPlayerOnline = _onlyOnlineCheck?.Checked ?? false,
            GmGenieAddon = _gmGenieCheck?.Checked ?? _selectedWow != null
        };
        _localeChoice = _worldOpts.ClientLocale;

        // GM credentials from the GUI
        if (!string.IsNullOrWhiteSpace(_gmUserBox?.Text)) _cfg.Server.GmUsername = _gmUserBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(_gmPassBox?.Text)) _cfg.Server.GmPassword = _gmPassBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(_gmCharBox?.Text)) _cfg.Server.GmCharacterName = _gmCharBox.Text.Trim();

        _moduleSelection = new List<ModuleInfo>();
        if (_moduleList != null)
            foreach (ListViewItem it in _moduleList.Items)
                if (it.Checked && it.Tag is ModuleInfo m && _modules.Contains(m))
                    _moduleSelection.Add(m);

        _worldOpts.ExtraModules = _extraModules.Concat(_urlModules).ToList();

        AddLog("World options: realm=" + _worldOpts.RealmName +
               " xp=" + _worldOpts.XpRate + "× honor=" + _worldOpts.HonorRate + "× gold=" + _worldOpts.GoldRate + "× cap=" + _worldOpts.LevelCap +
               " bots=" + _worldOpts.RandomBots +
               (string.Join("", _moduleSelection.Select(m => " [" + m.Friendly + "]"))) +
               (_worldOpts.ExtraModules.Count > 0 ? " +" + _worldOpts.ExtraModules.Count + " extra module(s)" : ""));
    }

    // ------------------------------------------------------------- step 8
    void BuildVerify()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.Controls.Add(Heading("Verify & finish"), 0, 0);

        var run = new Button
        {
            Text = "🚀  Run Verification & Finish",
            Height = 34, Width = 300, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(38, 110, 60), ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        run.Click += (s, e) => _ = RunStepActionAsync(async () =>
        {
            _verified = false;
            await VerifyFinishAsync();
            if (IsHandleCreated) { _content.Controls.Clear(); ShowStep(7); }
        });
        p.Controls.Add(run, 0, 1);

        _verifyChecklist = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            Text = "This will:\n  1.  Write authserver / worldserver / realmserver configuration (DB + PlayerBots module)\n" +
                   "  2.  Create the GM account" + (string.IsNullOrEmpty(_cfg.Server.GmUsername) ? "" : " (" + _cfg.Server.GmUsername + ")") + " and a starter character\n" +
                   "  3.  Create Start/Stop/Play shortcuts on the desktop + firewall rule\n" +
                   "  4.  Boot the whole server stack and verify it actually runs\n  5.  Leave the database running so you can play right away"
        };
        p.Controls.Add(_verifyChecklist, 0, 2);

        p.Controls.Add(Heading("Result", small: true), 0, 3);
        _verifyResult = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 35, 44),
            ForeColor = _verified ? Color.FromArgb(120, 220, 120) : Color.Gainsboro,
            Font = new Font("Consolas", 9f),
            Text = _verified ? "✓ Verification passed - Azaroth Core is up and running." : "Not verified yet - click the button above."
        };
        p.Controls.Add(_verifyResult, 0, 4);
        _content.Controls.Add(p);
    }

    async Task VerifyFinishAsync()
    {
        if (_layout == null) throw new Exception("Prepare the server core first.");
        if (_db == null) throw new Exception("Set up the database first.");
        CommitWowSelection();

        CommitWorldOptions();
        var b = NewBuilder();
        _gmUser = _cfg.Server.GmUsername;
        _gmPass = _cfg.Server.GmPassword;
        var wowPath = _selectedWow?.Path;

        SetStatus("Writing configuration & world options...");
        var opts = _worldOpts;
        var mods = _moduleSelection;
        var locale = _localeChoice;
        await Task.Run(() =>
        {
            b.WriteConfigs(_layout, _db);
            b.ApplyWorldOptions(_layout, _db, opts, mods, wowPath, locale);
            b.CreateGmAccount(_db);
            b.WriteLaunchers(_layout, _db, wowPath, locale);
        });

        SetStatus("Smoke-testing the server stack (up to ~2 min)...");
        _verified = await b.SmokeTestAsync(_layout, _db);

        var summary = BuildSummary(wowPath);
        b.WriteMarker(summary);

        if (_verified)
            SetStatus("Verification passed - Azaroth Core is ready!", Color.FromArgb(120, 220, 120));
        else
            SetStatus("Server stack did not verify - check the log (DB/data problems are common on first boot).", Color.OrangeRed);
        _summary = summary;
    }

    InstallSummary BuildSummary(string wowPath)
    {
        return new InstallSummary
        {
            InstallRoot = _installRoot ?? "",
            ServerDir = _layout?.ServerDir ?? "",
            WowPath = wowPath ?? "",
            ClientFound = wowPath != null,
            DbSource = _db?.Source ?? "",
            DbLogin = _db?.Login ?? "",
            DbPassword = _db?.Password ?? "",
            GmUser = _gmUser,
            GmPassword = _gmPass,
            ServerVerified = _verified
        };
    }

    // ------------------------------------------------------------- step 8
    void BuildDone()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        p.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        p.Controls.Add(Heading("All done! ⚔"), 0, 0);

        _summaryBox = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 35, 44), ForeColor = Color.Gainsboro,
            Font = new Font("Consolas", 9.5f),
            Text = FormatSummary()
        };
        p.Controls.Add(_summaryBox, 0, 1);

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0) };
        var start = MkBtn("▶  Start Azaroth Server", Color.FromArgb(38, 110, 60));
        start.Click += (s, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = Path.Combine(_layout.ServerDir, "Start_Azaroth.bat"), WorkingDirectory = _layout.ServerDir, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not start the server: " + ex.Message, "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        var play = MkBtn("🎮  Play (launch WoW)", Color.FromArgb(40, 90, 160));
        play.Enabled = _selectedWow != null;
        play.Click += (s, e) =>
        {
            try
            {
                var bat = Path.Combine(_layout.ServerDir, "Play_Azaroth.bat");
                Process.Start(new ProcessStartInfo { FileName = bat, WorkingDirectory = _layout.ServerDir, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, "Could not launch the game: " + ex.Message, "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        var open = MkBtn("📂  Open Install Folder", Color.FromArgb(60, 64, 76));
        open.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = _installRoot, UseShellExecute = true }); }
            catch { }
        };
        var exit = MkBtn("✖  Exit", Color.FromArgb(80, 40, 40));
        exit.Click += (s, e) => Close();
        row.Controls.Add(start);
        row.Controls.Add(play);
        row.Controls.Add(open);
        row.Controls.Add(exit);
        p.Controls.Add(row, 0, 2);
        _content.Controls.Add(p);
    }

    string FormatSummary()
    {
        var s = _summary;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("AZAROTH CORE - INSTALLATION COMPLETE " + (s?.ServerVerified == true ? "✓ (server verified)" : "(server not verified - see log)"));
        sb.AppendLine();
        sb.AppendLine("  Install folder : " + (s?.InstallRoot ?? _installRoot));
        sb.AppendLine("  Server files   : " + (s?.ServerDir ?? _layout?.ServerDir));
        sb.AppendLine("  Database       : " + (s?.DbSource ?? _db?.Source));
        sb.AppendLine("  DB login       : " + (s?.DbLogin ?? _db?.Login) + "  /  " + (s?.DbPassword ?? _db?.Password));
        if (_worldOpts != null)
            sb.AppendLine("  World          : " + _worldOpts.RealmName + "  |  XP " + _worldOpts.XpRate + "×  honor " +
                          _worldOpts.HonorRate + "×  gold " + _worldOpts.GoldRate + "×  cap " + _worldOpts.LevelCap +
                          "  |  " + _worldOpts.RandomBots + " randombots" +
                          (string.Join("", _moduleSelection.Select(m => " +" + m.Friendly))) +
                          (_worldOpts.ExtraModules.Count > 0 ? " +" + _worldOpts.ExtraModules.Count + " extra" : ""));
        sb.AppendLine("  GM account     : " + _gmUser + "  /  " + _gmPass + "   (gmsec 3)");
        sb.AppendLine("  WoW client     : " + (s?.WowPath == "" ? "(none - add a 3.3.5 client later, then use Play_Azaroth.bat)" : s.WowPath));
        sb.AppendLine();
        sb.AppendLine("  Desktop:  [Start Azaroth Server]  [Stop Azaroth Server]" + (s?.ClientFound == true ? "  [Play Azaroth]" : ""));
        sb.AppendLine();
        sb.AppendLine("  To play:     click 'Start Azaroth Server', wait for 'world server is running',");
        sb.AppendLine("               then click 'Play Azaroth' and log in as " + _gmUser + " / " + _gmPass + ".");
        sb.AppendLine("  PlayerBots:  in game, type  .playerbots  to open the bot manager (e.g. .playerbots randombot spawn 5).");
        sb.AppendLine("  Config:      " + Path.Combine(AppContext.BaseDirectory, "config.json"));
        return sb.ToString();
    }

    // =============================================================== wiring
    ServerBuilder NewBuilder()
    {
        var b = new ServerBuilder(_cfg, _installRoot ?? Path.Combine(_drive?.Root ?? @"C:\", _cfg.InstallFolderName))
        {
            Log = AddLog,
            CancellationToken = _cts.Token,
            ExtractPct = new Progress<long>(pct => SetProgress((int)pct)),
            DownloadProgress = new Progress<DownloadProgress>(dp =>
            {
                SetStatus("Downloading " + dp.File + " - " + dp.PercentText);
                if (dp.Total > 0) SetProgress((int)(dp.Received * 100 / dp.Total));
            })
        };
        return b;
    }

    Label Heading(string text, bool small = false) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", small ? 10f : 12f, FontStyle.Bold),
        ForeColor = Color.White
    };

    Label FieldLabel(string text) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.Silver
    };

    Control Row(string label, Control control)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var lbl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Silver
        };
        t.Controls.Add(lbl, 0, 0);
        t.Controls.Add(control, 1, 0);
        return t;
    }

    // generic runner: executes an action off the UI thread, with busy state + status
    async Task RunStepActionAsync(Func<Task> action)
    {
        if (_busy) return;
        SetBusy(true);
        _cts = new CancellationTokenSource();
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            AddLog("Cancelled by user.");
            SetStatus("Cancelled.", Color.OrangeRed);
        }
        catch (Exception ex)
        {
            AddLog("ERROR: " + ex.Message);
            SetStatus("Error: " + ex.Message, Color.OrangeRed);
            MessageBox.Show(this, "Something went wrong:\n\n" + ex.Message,
                "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            _progress.Style = ProgressBarStyle.Continuous;
        }
    }

    void OnNext()
    {
        if (_busy) return;
        switch (_step)
        {
            case 1:
                if (_sys == null) return;
                if (!string.IsNullOrEmpty(_installRoot))
                {
                    // make sure folder exists/creatable
                    try { Directory.CreateDirectory(_installRoot); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Cannot use this path:\n" + ex.Message, "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                ShowStep(2);
                break;
            case 2:
                if (_driveCombo.SelectedItem == null) return;
                _drive = _sys.Drives[Convert.ToInt32(_driveCombo.SelectedIndex)];
                if (_drive.FreeBytes < _cfg.MinFreeSpaceGB * 1073741824 &&
                    MessageBox.Show(this, "Only " + _drive.FreeBytes / 1073741824 + " GB free on " + _drive.Root +
                        " - less than the recommended " + _cfg.MinFreeSpaceGB + " GB.\nContinue anyway?",
                        "Low disk space", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
                ShowStep(3);
                break;
            case 3:
                if (_layout == null)
                {
                    MessageBox.Show(this, "Click 'Download & Prepare Server' first - the wizard needs the server files.",
                        "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ShowStep(4);
                break;
            case 4:
                if (!_dataDone)
                {
                    if (_skipData?.Checked == true) { /* user accepted */ }
                    else
                    {
                        MessageBox.Show(this, "Game data is not ready. The world server cannot start without it.\n\n" +
                                "Run 'Prepare Data & PlayerBots' first, tick 'skip data download' if you provide data yourself, " +
                            "or Back out to fix the repack.",
                            "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                ShowStep(5);
                break;
            case 5:
                if (_db == null)
                {
                    MessageBox.Show(this, "Click 'Set Up Database' first.", "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ShowStep(6);
                break;
            case 6:
                CommitWowSelection();
                ShowStep(7);
                break;
            case 7:
                CommitWorldOptions();
                ShowStep(8);
                break;
            case 8:
                if (!_verified)
                {
                    if (MessageBox.Show(this, "The smoke test did not pass.\nYou can still finish and look at the logs, or go back and re-run the steps.\n\nFinish anyway?",
                            "Azaroth Installer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }
                ShowStep(9);
                break;
        }
    }

    // ============================================================ full auto
    async Task RunAutoInstallAsync()
    {
        AddLog("=== FULL AUTO MODE - the wizard will pick the best options for your system ===");
        SetBusy(true);
        try
        {
            // 1 system
            ShowStep(1);
            _sys = await Task.Run(() => SysProbe.GetSystemInfo(AddLog));
            AddLog("CPU: " + _sys.CpuName);
            AddLog("RAM: " + _sys.RamGb + "   GPU: " + string.Join("; ", _sys.Gpus.Select(g => g.Name)));
            foreach (var d in _sys.Drives)
                AddLog("Drive " + d.Root + ": " + d.FreeText + (d.IsSystem ? " [system]" : ""));
            ShowStep(1); // refresh the on-screen summary now that we have data

            // 2 location
            ShowStep(2);
            SelectBestDrive();
            _drive = _sys.Drives.OrderByDescending(d => d.FreeBytes).ThenBy(d => d.IsSystem ? 0 : 1).FirstOrDefault();
            _installRoot = Path.Combine(_drive.Root, _cfg.InstallFolderName);
            AddLog("Best drive for installation: " + _drive.Root + " -> " + _installRoot);
            Directory.CreateDirectory(_installRoot);

            // 3 core
            ShowStep(3);
            _layout = null;
            try
            {
                await PrepareCoreAsync();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("No repack source"))
                {
                    AddLog("No repack zip/URL configured - full auto paused. Pick a local .zip file (or paste a URL) and click 'Download & Prepare Server', then continue with Next.");
                    SetStatus("Full auto paused: pick the server zip, then continue manually.", Color.OrangeRed);
                    SetBusy(false);
                    return;
                }
                throw;
            }

            if (_layout == null)
            {
                AddLog("Full auto paused: server core is not prepared. Pick a local .zip file or paste a URL, then click 'Download & Prepare Server'.");
                SetStatus("Full auto paused: pick a server repack zip or paste a URL.", Color.OrangeRed);
                SetBusy(false);
                return;
            }

            // 4 data + bots
            ShowStep(4);
            await PrepareDataBotsAsync();

            // 5 database
            ShowStep(5);
            await SetupDbAsync(false);

            // 6 client
            ShowStep(6);
            var extra = _cfg.WowClient.ExtraScanDirs ?? new List<string>();
            var found = await Task.Run(() => SysProbe.ScanForWoW(AddLog, extra));
            if (IsHandleCreated && _wowList != null)
            {
                _wowList.Items.Clear();
                foreach (var c in found.Take(12))
                    AddWowItem(c, false);
            }
            _selectedWow = found.FirstOrDefault(c => !c.LooksModern);
            AddLog(_selectedWow == null ? "No 3.3.5 WoW client found - server-only install." : "WoW client: " + _selectedWow.Path);

            // 7 world & options (config.json defaults - shown for transparency)
            ShowStep(7);
            CommitWorldOptions();
            await Task.Delay(900);

            // 8 verify
            ShowStep(8);
            await VerifyFinishAsync();

            // 9 done
            ShowStep(9);
            SetStatus(_verified ? "Full auto install complete!" : "Install finished with warnings - see log.",
                _verified ? Color.FromArgb(120, 220, 120) : Color.OrangeRed);
        }
        catch (OperationCanceledException)
        {
            AddLog("Cancelled by user.");
        }
        catch (Exception ex)
        {
            AddLog("FULL AUTO ERROR: " + ex.Message);
            SetStatus("Full auto stopped: " + ex.Message, Color.OrangeRed);
            MessageBox.Show(this, "Full auto install stopped:\n\n" + ex.Message +
                "\n\nYou can continue manually from this screen.", "Azaroth Installer",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            _progress.Style = ProgressBarStyle.Continuous;
        }
    }
}
