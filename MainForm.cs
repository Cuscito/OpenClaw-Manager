using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OpenClawManager;

public class MainForm : Form
{
    Panel sidebar, body, themePanel, titlePanel;
    Label titleLbl, versionLbl;
    int _navEndY;
    NotifyIcon trayIcon;
    string currentPanel = "dashboard";
    ChatPage? _chatPage;
    internal static readonly string AppVersion = "1.2.10";
    static string? _ocVersion;
    public static void RefreshOcVersion() { _ocVersion = null; }

    internal static string WindowTitle()
    {
        var suffix = LicenseManager.IsPro ? OpenClawManager.Properties.LanguageManager.GetString("MainFormPro") : LicenseManager.IsTrialExpired ? OpenClawManager.Properties.LanguageManager.GetString("MainFormTrialExpired") : string.Format(OpenClawManager.Properties.LanguageManager.GetString("MainFormTrial"), LicenseManager.TrialDaysRemaining);
        // 标题栏版本跟随 OpenClaw 真实版本
        var ver = AppVersion;
        if (_ocVersion == null)
        {
            try
            {
                string nodeExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node.exe");
                string entry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "index.js");
                if (!File.Exists(entry)) entry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "openclaw.mjs");
                if (File.Exists(nodeExe) && File.Exists(entry))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo { FileName = nodeExe, Arguments = "\"" + entry + "\" --version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var p = System.Diagnostics.Process.Start(psi);
                    if (p != null) { p.WaitForExit(3000); var o = p.StandardOutput.ReadToEnd().Trim(); var m = System.Text.RegularExpressions.Regex.Match(o, @"(\d+\.\d+\.\d+)"); if (m.Success) _ocVersion = m.Groups[1].Value; }
                }
            }
            catch { }
            _ocVersion ??= AppVersion;
        }
        ver = _ocVersion;
        return $"OpenClaw v{ver}{suffix}";
    }
    static readonly string BuildDate = System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd");
    (string id, string label, string icon)[] GetNavItems() =>
    [
        ("dashboard", OpenClawManager.Properties.LanguageManager.GetString("NavDashboard"), "■"),
        ("setup", OpenClawManager.Properties.LanguageManager.GetString("NavSetup"), "⚑"),
        ("chat", OpenClawManager.Properties.LanguageManager.GetString("NavChat"), "●"),
        ("models", OpenClawManager.Properties.LanguageManager.GetString("NavModels"), "◆"),
        ("skills", OpenClawManager.Properties.LanguageManager.GetString("NavSkills"), "★"),
        ("channels", OpenClawManager.Properties.LanguageManager.GetString("NavChannels"), "◇"),
        ("logs", OpenClawManager.Properties.LanguageManager.GetString("NavLogs"), "☰"),
        ("settings", OpenClawManager.Properties.LanguageManager.GetString("NavSettings"), "⚙"),
        ("guardian", OpenClawManager.Properties.LanguageManager.GetString("NavGuardian"), "▲"),
    ];

    public static string CfgFullPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "openclaw.json");
    public static string GatewayUrl => _gwUrl ??= ResolveGatewayUrl();
    static string? _gwUrl;
    static string ResolveGatewayUrl()
    {
        try
        {
            if (File.Exists(CfgFullPath))
            {
                var cfg = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(CfgFullPath, System.Text.Encoding.UTF8))?.AsObject();
                if (cfg != null && cfg.TryGetPropertyValue("gateway", out var gw) && gw is System.Text.Json.Nodes.JsonObject gwo)
                {
                    string port = gwo.TryGetPropertyValue("port", out var pt) ? pt!.ToString() : "18789";
                    string host = "127.0.0.1";
                    if (gwo.TryGetPropertyValue("bind", out var bd) && bd is System.Text.Json.Nodes.JsonValue bdv)
                    {
                        var b = bdv.ToString();
                        if (b == "lan" || b == "public") host = "127.0.0.1"; // always local from Manager
                    }
                    return "http://" + host + ":" + port + "/";
                }
            }
        }
        catch { }
        return "http://127.0.0.1:18789/";
    }

    public MainForm()
    {
        Theme.InitDpi(this);
        Theme.LoadTheme();
        // 设置为 OpenClaw 默认图标
        try { var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apple-touch-icon.png"); if (File.Exists(icoPath)) { var bmp = new Bitmap(icoPath); Icon = Icon.FromHandle(bmp.GetHicon()); } } catch { }
        DpiChanged += (_, _) => { Theme.InitDpi(this); BuildSidebar(); ShowPanel(currentPanel); };
        Text = WindowTitle();
        
        // 订阅语言更改事件
        OpenClawManager.Properties.LanguageManager.LanguageChanged += () => {
            if (InvokeRequired)
            {
                Invoke(() => {
                    Controls.Clear();
                    BuildShell();
                    UpdateCurrentPanelTitle();
                    if (currentPanel != "setup" && currentPanel != "launcher") BuildSidebar();
                    ShowPanel(currentPanel);
                });
            }
            else
            {
                Controls.Clear();
                BuildShell();
                UpdateCurrentPanelTitle();
                if (currentPanel != "setup" && currentPanel != "launcher") BuildSidebar();
                ShowPanel(currentPanel);
            }
        };
        
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        var w = Math.Min((int)(1200 * Theme.ScaleFactor), (int)(wa.Width * 0.9));
        var h = Math.Min((int)(780 * Theme.ScaleFactor), (int)(wa.Height * 0.9));
        Size = new Size(w, h);
        MinimumSize = new Size((int)(800 * Theme.ScaleFactor), (int)(500 * Theme.ScaleFactor));
        BackColor = Theme.Bg; ForeColor = Theme.Fc;
        Font = Theme.Font(9f);
        StartPosition = FormStartPosition.CenterScreen;
        Resize += OnResize;
        FormClosing += OnFormClosing;

        trayIcon = new NotifyIcon
        {
            Text = WindowTitle(),
            Visible = false,
            Icon = Icon
        };
        trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add(OpenClawManager.Properties.LanguageManager.GetString("MainFormShow"), null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        trayMenu.Items.Add("-");
        trayMenu.Items.Add(OpenClawManager.Properties.LanguageManager.GetString("MainFormExit"), null, (_, _) =>
        {
            var dlg = new Form
            {
                Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormExitTitle"),
                Size = new Size(360, 180),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = Theme.BgWhite, ForeColor = Theme.Fc,
                Font = Theme.Font(9f)
            };
            dlg.Controls.Add(new Label
            {
                Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormExitPrompt"),
                AutoSize = true, Location = new Point(28, 30),
                ForeColor = Theme.Fc, Font = Theme.Font(10f, FontStyle.Bold),
                BackColor = Color.Transparent
            });
            dlg.Controls.Add(new Label
            {
                Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormExitSubPrompt"),
                AutoSize = true, Location = new Point(28, 58),
                ForeColor = Theme.Fc2, Font = Theme.Font(8f),
                BackColor = Color.Transparent
            });

            DialogResult result = DialogResult.Cancel;
            var btnClose = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormCloseGateway"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Red, ForeColor = Theme.FcWhite, Font = Theme.Font(9f), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, Location = new Point(60, 95) };
            btnClose.Click += (_, _) => { result = DialogResult.Yes; dlg.Close(); };
            dlg.Controls.Add(btnClose);

            var btnOnly = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormExitOnly"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Fc2, ForeColor = Theme.FcWhite, Font = Theme.Font(9f), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, Location = new Point(190, 95) };
            btnOnly.Click += (_, _) => { result = DialogResult.No; dlg.Close(); };
            dlg.Controls.Add(btnOnly);

            dlg.ShowDialog();

            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes)
            {
                OpenClawRuntime.KillOwnedProcesses();
            }
            trayIcon.Visible = false;
            Application.Exit();
        });
        trayIcon.ContextMenuStrip = trayMenu;

        BuildShell();
        if (File.Exists(CfgFullPath))
        {
            ShowPanel("dashboard");
            UpdateCurrentPanelTitle();
        }
        else
        {
            ShowPanel("setup");
            UpdateCurrentPanelTitle();
        }
    }

    void BuildShell()
    {
        // 标题栏容器面板
        titlePanel = new Panel
        {
            Size = new Size(ClientSize.Width, (int)(Theme.TitleH * Theme.ScaleFactor)),
            BackColor = Theme.BgWhite
        };
        
        titleLbl = new Label
        {
            Text = "  OpenClaw",
            ForeColor = Theme.Fc,
            Font = Theme.Font(13.5f, FontStyle.Bold),
            Size = new Size(ClientSize.Width - 55, (int)(Theme.TitleH * Theme.ScaleFactor)),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Theme.BgWhite,
            Location = new Point(0, 0)
        };
        titleLbl.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(Theme.BdrLight), 0, (int)(Theme.TitleH * Theme.ScaleFactor) - 1, titleLbl.Width, (int)(Theme.TitleH * Theme.ScaleFactor) - 1); };
        titleLbl.Click += (_, _) => _chatPage?.CollapseSidebar();
        titlePanel.Controls.Add(titleLbl);
        
        // 语言切换按钮
        var langBtn = new Button
        {
            Name = "langBtn",
            Text = CurrentLangShort(),
            Font = Theme.Font(9f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(50, (int)(Theme.TitleH * Theme.ScaleFactor)),
            BackColor = Theme.BgWhite,
            ForeColor = Theme.Fc,
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 },
            Location = new Point(ClientSize.Width - 55, 0)
        };
        langBtn.Click += ToggleLanguage;
        titlePanel.Controls.Add(langBtn);
        
        Controls.Add(titlePanel);

        bool hasConfig = File.Exists(CfgFullPath);

        int scaledTitleH = (int)(Theme.TitleH * Theme.ScaleFactor);
        int dynSidebarW = SidebarWidth();
        sidebar = new Panel
        {
            BackColor = Theme.BgSidebar,
            Location = new Point(0, scaledTitleH),
            Size = new Size(dynSidebarW, ClientSize.Height - scaledTitleH),
            Visible = hasConfig
        };
        sidebar.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(Theme.BdrLight), dynSidebarW - 1, 0, dynSidebarW - 1, sidebar.Height); };
        sidebar.Click += (_, _) => _chatPage?.CollapseSidebar();
        Controls.Add(sidebar);

        body = new Panel
        {
            BackColor = Theme.Bg,
            Location = new Point(hasConfig ? dynSidebarW : 0, scaledTitleH),
            Size = new Size(ClientSize.Width - (hasConfig ? dynSidebarW : 0), ClientSize.Height - scaledTitleH),
            AutoScroll = true
        };
        Controls.Add(body);

        BuildSidebar();
    }

    internal void BuildSidebar()
    {
        sidebar.SuspendLayout();
        sidebar.Controls.Clear();
        bool hasConfig = File.Exists(CfgFullPath);
        int sw = sidebar.Width;
        int y = (int)(12 * Theme.ScaleFactor);
        int itemH = (int)(42 * Theme.ScaleFactor);
        int itemGap = (int)(5 * Theme.ScaleFactor);
        float fs = Theme.ScaleFactor;
        foreach (var (id, label, icon) in GetNavItems())
        {
            if (!hasConfig && id != "setup") continue;
            if (hasConfig && id == "setup") continue;
            if (id == "guardian") continue; // 本地免部署版本无需守护程序
            var p = new Panel
            {
                Size = new Size(sw - (int)(16 * fs), itemH),
                Location = new Point((int)(8 * fs), y),
                Cursor = Cursors.Hand,
                BackColor = id == currentPanel ? Theme.BgSelected : Theme.BgSidebar,
                Tag = id
            };
            p.MouseEnter += (_, _) => { if (id != currentPanel) p.BackColor = Theme.BgHover; };
            p.MouseLeave += (_, _) => { if (id != currentPanel) p.BackColor = Theme.BgSidebar; };
            p.Click += (_, _) => { _chatPage?.CollapseSidebar(); ShowPanel(id); };

            var ic = new Label { Text = icon, ForeColor = id == currentPanel ? Theme.Acc : Theme.Fc2, Font = Theme.Font(10f, FontStyle.Bold), Size = new Size((int)(28 * fs), itemH), TextAlign = ContentAlignment.MiddleCenter, Location = new Point((int)(10 * fs), 0), BackColor = Color.Transparent };
            ic.Click += (_, _) => { _chatPage?.CollapseSidebar(); ShowPanel(id); };
            p.Controls.Add(ic);

            var lb = new Label { Text = label, ForeColor = Theme.Fc, Font = Theme.Font(9.5f, id == currentPanel ? FontStyle.Bold : FontStyle.Regular), Size = new Size(sw - (int)(58 * fs), itemH), TextAlign = ContentAlignment.MiddleLeft, Location = new Point((int)(44 * fs), 0), BackColor = Color.Transparent };
            lb.Click += (_, _) => { _chatPage?.CollapseSidebar(); ShowPanel(id); };
            p.Controls.Add(lb);

            sidebar.Controls.Add(p);
            y += itemH + itemGap;
        }
        _navEndY = y;

        themePanel = new Panel
        {
            Size = new Size(sw - (int)(16 * fs), (int)(34 * fs)),
            Location = new Point((int)(8 * fs), Math.Max(y + (int)(4 * fs), sidebar.ClientSize.Height - (int)(66 * fs))),
            Cursor = Cursors.Hand,
            BackColor = Theme.BgSidebar
        };
        themePanel.MouseEnter += (_, _) => themePanel.BackColor = Theme.BgHover;
        themePanel.MouseLeave += (_, _) => themePanel.BackColor = Theme.BgSidebar;
        var themeIco = new Label { Text = Theme.IsDark ? "☀️" : "🌙", Font = Theme.Font(11f), ForeColor = Theme.Fc2, Size = new Size((int)(28 * fs), (int)(32 * fs)), TextAlign = ContentAlignment.MiddleCenter, Location = new Point((int)(8 * fs), 0), BackColor = Color.Transparent };
        var themeLbl = new Label { Text = Theme.IsDark ? OpenClawManager.Properties.LanguageManager.GetString("MainFormSwitchLight") : OpenClawManager.Properties.LanguageManager.GetString("MainFormSwitchDark"), Font = Theme.Font(8.5f), ForeColor = Theme.Fc2, Size = new Size((int)(92 * fs), (int)(32 * fs)), TextAlign = ContentAlignment.MiddleLeft, Location = new Point((int)(38 * fs), 0), BackColor = Color.Transparent };
        themePanel.Controls.Add(themeIco);
        themePanel.Controls.Add(themeLbl);
        EventHandler toggleTheme = (_, _) => ApplyThemeChange();
        themePanel.Click += toggleTheme; themeIco.Click += toggleTheme; themeLbl.Click += toggleTheme;
        sidebar.Controls.Add(themePanel);

        versionLbl = new Label
        {
            Text = $"v{AppVersion} \u00B7 {BuildDate}",
            ForeColor = Theme.Fc2,
            Font = Theme.Font(8f),
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Size = new Size(sw - 16, 22),
            Location = new Point((int)(14 * fs), sidebar.ClientSize.Height - (int)(30 * fs))
        };
        sidebar.Controls.Add(versionLbl);
        sidebar.ResumeLayout(false);
    }

    void ApplyThemeChange()
    {
        Theme.Toggle();
        SuspendLayout();
        try
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Fc;
            Font = Theme.Font(9f);
            
            // 更新标题栏面板
            int titleH = (int)(Theme.TitleH * Theme.ScaleFactor);
            titlePanel.Size = new Size(ClientSize.Width, titleH);
            titlePanel.BackColor = Theme.BgWhite;
            titleLbl.BackColor = Theme.BgWhite;
            titleLbl.ForeColor = Theme.Fc;
            titleLbl.Size = new Size(ClientSize.Width - 55, titleH);
            
            // 更新语言切换按钮主题
            var ctls = titlePanel.Controls.Find("langBtn", false);
            if (ctls.Length > 0 && ctls[0] is Button lb)
            {
                lb.BackColor = Theme.BgWhite;
                lb.ForeColor = Theme.Fc;
                lb.Location = new Point(ClientSize.Width - 55, 0);
                lb.Size = new Size(50, titleH);
            }
            
            sidebar.BackColor = Theme.BgSidebar;
            body.BackColor = Theme.Bg;
            BuildSidebar();

            if (currentPanel == "chat" && _chatPage != null)
            {
                body.Controls.Clear();
                _chatPage.Reattach(body);
                _chatPage.ApplyTheme();
                Theme.ApplyTo(body);
            }
            else
            {
                ShowPanel(currentPanel);
            }

            titleLbl.Invalidate();
            sidebar.Invalidate(true);
            body.Invalidate(true);
            trayIcon.Text = WindowTitle();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    internal void ShowPanel(string id)
    {
        currentPanel = id;
        body.Controls.Clear();
        body.BackColor = Theme.Bg;
        body.AutoScrollPosition = new Point(0, 0);
        body.AutoScroll = id != "chat";

        if (id == "setup" || id == "launcher")
        {
            titleLbl.Visible = false;
            sidebar.Visible = false;
            body.Location = new Point(0, 0);
            body.Size = new Size(ClientSize.Width, ClientSize.Height);
        }
        else
        {
            titleLbl.Visible = true;
            sidebar.Visible = true;
            int sh = (int)(Theme.TitleH * Theme.ScaleFactor);
            int sw = SidebarWidth();
            sidebar.Width = sw;
            body.Location = new Point(sw, sh);
            body.Size = new Size(ClientSize.Width - sw, ClientSize.Height - sh);
        }

        foreach (Control c in sidebar.Controls)
        {
            if (c is Panel p)
                p.BackColor = p.Tag?.ToString() == id ? Theme.BgSelected : Theme.BgSidebar;
        }

        Text = WindowTitle();
        UpdateCurrentPanelTitle();

        switch (id)
        {
            case "dashboard": new DashboardPage().Build(body); break;
            case "chat":
                if (_chatPage == null) { _chatPage = new ChatPage(); _chatPage.Build(body); }
                else _chatPage.Reattach(body);
                break;
            case "models":    new ModelsPage().Build(body);    break;
            case "skills":    new SkillsPage().Build(body);    break;
            case "channels":  new ChannelsPage().Build(body);  break;
            case "logs":      new LogsPage().Build(body);      break;
            case "launcher": new LauncherPage().Build(body); break;
            case "setup":     new SetupPage().Build(body);     break;
            case "settings":  new SettingsPage().Build(body); body.HorizontalScroll.Enabled = false; body.HorizontalScroll.Visible = false; break;
            case "guardian":  new GuardianPage().Build(body);  break;
        }
        Theme.ApplyTo(body);
    }

    void OnResize(object sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            trayIcon.Visible = true;
            trayIcon.ShowBalloonTip(2000, "OpenClaw", OpenClawManager.Properties.LanguageManager.GetString("MainFormMinimizedToTray"), ToolTipIcon.Info);
            return;
        }
        // 更新标题栏面板大小
        int titleH = (int)(Theme.TitleH * Theme.ScaleFactor);
        titlePanel.Size = new Size(ClientSize.Width, titleH);
        titleLbl.Size = new Size(ClientSize.Width - 55, titleH);
        
        // 更新语言按钮位置
        var ctls = titlePanel.Controls.Find("langBtn", false);
        if (ctls.Length > 0 && ctls[0] is Button lb)
        {
            lb.Location = new Point(ClientSize.Width - 55, 0);
            lb.Size = new Size(50, titleH);
        }
        
        sidebar.Width = SidebarWidth();
        sidebar.Height = ClientSize.Height - (int)(Theme.TitleH * Theme.ScaleFactor);
        if (currentPanel == "setup" || currentPanel == "launcher")
        {
            titleLbl.Visible = false;
            body.Size = new Size(ClientSize.Width, ClientSize.Height);
        }
        else if (sidebar.Visible)
        {
            body.Size = new Size(ClientSize.Width - sidebar.Width, ClientSize.Height - (int)(Theme.TitleH * Theme.ScaleFactor));
            versionLbl.Location = new Point(12, sidebar.ClientSize.Height - 28);
            versionLbl.Width = sidebar.Width - 16;
            // 同步更新主题切换按钮位置和宽度（最大化/还原时侧边栏尺寸变化）
            // 用 _navEndY 重算 Y：取导航项底部和侧边栏底部两个值中的较大者
            float fs = Theme.ScaleFactor;
            themePanel.Width = sidebar.Width - (int)(16 * fs);
            themePanel.Location = new Point((int)(8 * fs), Math.Max(_navEndY + (int)(4 * fs), sidebar.ClientSize.Height - (int)(66 * fs)));
        }
        else
        {
            body.Size = new Size(ClientSize.Width, ClientSize.Height - (int)(Theme.TitleH * Theme.ScaleFactor));
        }
        if (!string.IsNullOrEmpty(currentPanel))
            ShowPanel(currentPanel);
    }

    int SidebarWidth()
    {
        var ideal = (int)(Theme.SidebarW * Theme.ScaleFactor);
        if (ClientSize.Width <= 0) return ideal;
        return Math.Max((int)(176 * Theme.ScaleFactor), Math.Min(ideal, ClientSize.Width / 4));
    }

    void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.UserClosing) return;
        e.Cancel = true;

        var dlg = new Form
        {
            Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormCloseWindow"),
            Size = new Size(360, 115),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ControlBox = false,
            BackColor = Theme.BgWhite, ForeColor = Theme.Fc,
            Font = Theme.Font(9f)
        };

        DialogResult result = DialogResult.Cancel;

        var btnTray = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormMinimizeToTray"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Acc, ForeColor = Theme.FcWhite, Font = Theme.Font(9f), Size = new Size(100, 34), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, Location = new Point(22, 30) };
        btnTray.Click += (_, _) => { result = DialogResult.No; dlg.Close(); };
        dlg.Controls.Add(btnTray);

        var btnExit = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormExitAndClose"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Red, ForeColor = Theme.FcWhite, Font = Theme.Font(9f), Size = new Size(114, 34), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, Location = new Point(128, 30) };
        btnExit.Click += (_, _) => { result = DialogResult.Yes; dlg.Close(); };
        dlg.Controls.Add(btnExit);

        var btnCancel = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("MainFormCancel"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Bg, ForeColor = Theme.Fc, Font = Theme.Font(9f), Size = new Size(80, 34), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 1, BorderColor = Theme.Bdr }, Location = new Point(248, 30) };
        btnCancel.Click += (_, _) => { dlg.Close(); };
        dlg.Controls.Add(btnCancel);

        dlg.ShowDialog();

        if (result == DialogResult.No)
        {
            Hide();
            trayIcon.Visible = true;
        }
        else if (result == DialogResult.Yes)
        {
            OpenClawRuntime.KillOwnedProcesses();
            trayIcon.Visible = false;
            Application.Exit();
        }
        // Cancel: do nothing, stay open
    }

    // 直接切换语言，按钮只显示另一种语言缩写
    void ToggleLanguage(object sender, EventArgs e)
    {
        var current = OpenClawManager.Properties.LanguageManager.CurrentCulture.Name;
        var next = current == "zh-CN" ? "en-US" : "zh-CN";
        OpenClawManager.Properties.LanguageManager.SetLanguage(next);
    }

    string CurrentLangShort()
    {
        var current = OpenClawManager.Properties.LanguageManager.CurrentCulture.Name;
        return current == "zh-CN" ? "中" : "EN";
    }

    public void UpdateLangBtnText()
    {
        var ctls = titlePanel.Controls.Find("langBtn", false);
        if (ctls.Length > 0 && ctls[0] is Button btn)
            btn.Text = CurrentLangShort();
    }

    // 更新当前面板标题
    void UpdateCurrentPanelTitle()
    {
        titleLbl.Text = "  OpenClaw - " + (currentPanel switch
        {
            "dashboard" => OpenClawManager.Properties.LanguageManager.GetString("NavDashboard"),
            "chat" => OpenClawManager.Properties.LanguageManager.GetString("NavChat"),
            "models" => OpenClawManager.Properties.LanguageManager.GetString("NavModels"),
            "skills" => OpenClawManager.Properties.LanguageManager.GetString("NavSkills"),
            "channels" => OpenClawManager.Properties.LanguageManager.GetString("NavChannels"),
            "logs" => OpenClawManager.Properties.LanguageManager.GetString("NavLogs"),
            "setup" => OpenClawManager.Properties.LanguageManager.GetString("NavSetup"),
            "settings" => OpenClawManager.Properties.LanguageManager.GetString("NavSettings"),
            "guardian" => OpenClawManager.Properties.LanguageManager.GetString("NavGuardian"),
            "launcher" => "??",
            _ => currentPanel
        });
    }
}
