using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawManager;

public class DashboardPage
{
    static readonly HttpClient GatewayHealthClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    // ── 内建守护 + 实时控制台（静态字段跨 Build 存活）──
    static System.Windows.Forms.Timer? _healthTimer;
    static int _healthFailCount;
    static Process? _gatewayProc;
    static StringBuilder _consoleLog = new();
    static TextBox? _consoleBox;
    static readonly string _consoleLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "logs", "dashboard-console.log");
    
    static string _lastLogMsg = "";
    static int _lastLogSec;

    public static async Task ApproveAllDevices() { await Task.CompletedTask; }

    /// 外部页面写入仪表盘日志（线程安全）
    public static void LogToConsole(string msg)
    {
        // 同一秒内相同消息去重
        var now = DateTime.Now;
        var sec = now.Hour * 3600 + now.Minute * 60 + now.Second;
        var line = $"[{now:HH:mm:ss}] {msg}\r\n";
        if (sec == _lastLogSec && line == _lastLogMsg) return;
        _lastLogSec = sec;
        _lastLogMsg = line;
        _consoleLog.Append(line);
        try { _consoleBox?.Invoke(() => { _consoleBox.AppendText(line); _consoleBox.ScrollToCaret(); }); } catch { }
    }

    static string TailText(string s, int maxLen)
    {
        if (s.Length <= maxLen) return s;
        return "... (已截断) ...\r\n" + s.Substring(s.Length - maxLen);
    }

    // 仪表盘控件引用，用于原地更新状态（避免整页刷新）
    static Label? _gwDot, _gwStatusLbl;
    static Button? _startBtn, _stopBtn, _restartBtn, _fixBtn, _btnWebConsole;
    static Label? _cardGw, _cardCpu, _cardUptime, _cardStart; // 卡片值标签，实时刷新
    static System.Windows.Forms.Timer? _refreshTimer;

    public void Build(Panel body)
    {
        // 清理旧定时器防止内存泄漏
        if (_healthTimer != null) { _healthTimer.Stop(); _healthTimer.Dispose(); _healthTimer = null; }
        _tailCts?.Cancel(); _tailCts?.Dispose(); _tailCts = null;
        if (_refreshTimer != null) { _refreshTimer.Stop(); _refreshTimer.Dispose(); _refreshTimer = null; }

        body.Controls.Clear();
        PatchWorkspaceDrive(); // 先修正盘符，再读取路径
        int pad = 12;
        body.Controls.Add(new Label { Text = "⏳ 仪表盘加载中...", ForeColor = Theme.Acc, Font = Theme.Font(11f, FontStyle.Bold), Location = new Point(pad, pad), AutoSize = true, BackColor = Color.Transparent });

        Task.Run(async () =>
        {
            var gwCheckTask = Task.Run(() => GwCheck());
            var gwInstalledTask = Task.Run(() => GwInstalledCheck());
            var nodeVerTask = Task.Run(() => NodeVerSync());
            var verTask = Task.Run(() => VerSync());
            var chCntTask = Task.Run(() => ChCount().ToString());
            var wsTask = Task.Run(() => WorkspaceSync());
            var modelTask = Task.Run(() => DefaultModelSync());

            var (gwOk, pid) = await gwCheckTask;
            bool gwInstalled = await gwInstalledTask;
            string cpuMem = CpuMemStr(pid), st = StartTimeSync(pid), ut = UpTimeSync(pid);

            string nodeVer = await nodeVerTask;
            string ver = await verTask;
            string chCnt = await chCntTask;
            string ws = await wsTask;
            string model = await modelTask;
            string ocVer = OcVerSync();

            if (body.IsDisposed) return;
            body.Invoke(() => Render(body, gwOk, pid, gwInstalled, cpuMem, chCnt, ver, st, ut, ws, model, nodeVer, ocVer));
        });
    }

    void Render(Panel body, bool gwOk, int? pid, bool gwInstalled, string cpuMem, string chCnt, string ver, string st, string ut, string ws, string model, string nodeVer, string ocVer)
    {
        body.AutoScroll = true;
        body.HorizontalScroll.Enabled = false;
        body.HorizontalScroll.Visible = false;
        body.Controls.Clear();
        float s = Theme.ScaleFactor;
        int pad = (int)(10 * s), gap = Math.Max(4, (int)(6 * s));
        int cw = body.ClientSize.Width - pad * 2, cardW = (cw - gap * 3) / 4;
        int cardH = (int)(80 * s), rowH = cardH + (int)(10 * s);
        int availH = body.ClientSize.Height;
        int y = pad;

        // Row 1 — 统计卡片
        var row1 = Flow(pad, y, cw, rowH); body.Controls.Add(row1);
        row1.Controls.Add(Card("Gateway 状态", gwOk ? "在线 (PID: " + pid + ")" : "离线", cardW, cardH, gwOk ? Theme.Grn : Theme.Red));
        _cardGw = row1.Controls[0].Controls[1] as Label;
        row1.Controls.Add(Card("CPU / 内存", cpuMem, cardW, cardH));
        _cardCpu = row1.Controls[1].Controls[1] as Label;
        row1.Controls.Add(Card("频道数", chCnt, cardW, cardH));
        row1.Controls.Add(Card("OpenClaw 版本", ver, cardW, cardH));
        y += rowH + (int)(4 * s);

        var row2 = Flow(pad, y, cw, rowH); body.Controls.Add(row2);
        row2.Controls.Add(Card("启动时间", st, cardW, cardH));
        _cardStart = row2.Controls[0].Controls[1] as Label;
        row2.Controls.Add(Card("运行时长", ut, cardW, cardH));
        _cardUptime = row2.Controls[1].Controls[1] as Label;
        row2.Controls.Add(Card("工作区", ws, cardW, cardH));
        row2.Controls.Add(Card("默认模型", model, cardW, cardH));
        y += rowH + (int)(4 * s);

        // 环境状态
        bool needNode = nodeVer == "未安装", needOc = ocVer == "未安装";
        body.Controls.Add(SectionTitle("环境状态", pad, y)); y += (int)(22 * s);
        int ey = y + (int)(6 * s), ex = pad;
        void AddLbl(string t, Color? c = null, float s = 9f, FontStyle st = FontStyle.Regular)
        {
            var l = new Label { Text = t, ForeColor = c ?? Theme.Fc, Font = Theme.Font(s, st), AutoSize = true, BackColor = Color.Transparent, Location = new Point(ex, ey) };
            body.Controls.Add(l);
            ex += l.PreferredSize.Width;
        }
        AddLbl("Node.js: ", Theme.Fc2);
        AddLbl(needNode ? "未安装" : nodeVer, needNode ? Theme.Red : Theme.Grn, 9f, FontStyle.Bold);
        ex += 6; if (!needNode) { AddLbl(" 已就绪", Theme.Grn); ex += 8; }
        AddLbl("OpenClaw: ", Theme.Fc2);
        AddLbl(needOc ? "未安装" : ocVer, needOc ? Theme.Red : Theme.Grn, 9f, FontStyle.Bold);
        ex += 6; if (!needOc) { AddLbl(" 已就绪", Theme.Grn); ex += 8; }
        AddLbl("网关: ", Theme.Fc2);
        AddLbl(gwInstalled ? "已就绪" : "未安装", gwInstalled ? Theme.Grn : Theme.Red, 9f, FontStyle.Bold);
        ex += 10;
        y += (int)(34 * s);

        // 网关控制
        body.Controls.Add(SectionTitle("网关控制", pad, y)); y += (int)(22 * s);
        int gy = y + 6;
        body.Controls.Add(new Label { Text = "\u25CF ", ForeColor = gwOk ? Theme.Grn : Theme.Red, Font = Theme.Font(12f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(pad, gy) });
        _gwDot = body.Controls[^1] as Label;
        var statusText = gwOk ? "运行中 (PID: " + pid + ")  " : "已停止  ";
        body.Controls.Add(new Label { Text = statusText, ForeColor = Theme.Fc, Font = Theme.Font(10f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(pad + (int)(24 * s), gy + 1) });
        _gwStatusLbl = body.Controls[^1] as Label;
        // 按钮从状态文字右侧开始，留足间距
        int bx = _gwStatusLbl.Location.X + _gwStatusLbl.PreferredWidth + (int)(20 * s);
        // 按钮行 Y 坐标
        int btnY = y + (int)(2 * s);
        bool narrow = body.ClientSize.Width < 800 || s > 1.15f;
        int btnH = narrow ? (int)(26 * s) : (int)(30 * s);
        Font btnFont = Theme.Font(narrow ? 8f : 9f, FontStyle.Bold);
        Padding btnPad = narrow ? new Padding((int)(6 * s), 1, (int)(6 * s), 1) : new Padding((int)(10 * s), 1, (int)(10 * s), 1);

        var startBtn = Theme.Btn("启动");
        startBtn.Font = btnFont; startBtn.Padding = btnPad; startBtn.Height = btnH;
        _startBtn = startBtn;
        if (gwOk) { startBtn.Enabled = false; startBtn.BackColor = Color.Gray; }
        else { startBtn.BackColor = Theme.Grn; }
        startBtn.Location = new Point(bx, btnY); body.Controls.Add(startBtn); bx += startBtn.PreferredSize.Width + (int)(6 * s);

        var stopBtn = Theme.Btn("停止");
        stopBtn.Font = btnFont; stopBtn.Padding = btnPad; stopBtn.Height = btnH;
        _stopBtn = stopBtn;
        if (!gwOk) { stopBtn.Enabled = false; stopBtn.BackColor = Color.Gray; }
        else { stopBtn.BackColor = Theme.Red; }
        stopBtn.Location = new Point(bx, btnY); body.Controls.Add(stopBtn); bx += stopBtn.PreferredSize.Width + (int)(6 * s);

        var restartBtn = Theme.Btn("重启");
        restartBtn.Font = btnFont; restartBtn.Padding = btnPad; restartBtn.Height = btnH;
        _restartBtn = restartBtn;
        if (!gwOk) { restartBtn.Enabled = false; restartBtn.BackColor = Color.Gray; }
        else { restartBtn.BackColor = Theme.QqOrange; }
        restartBtn.Location = new Point(bx, btnY); body.Controls.Add(restartBtn); bx += restartBtn.PreferredSize.Width + (int)(6 * s);

        var fixBtn = Theme.Btn("修复配置"); fixBtn.Font = btnFont; fixBtn.Padding = btnPad; fixBtn.Height = btnH;
        _fixBtn = fixBtn;
        fixBtn.Click += (_, _) => { RestoreConfig(); body.Controls.Clear(); Build(body); };
        fixBtn.Location = new Point(bx, btnY); body.Controls.Add(fixBtn); bx += fixBtn.PreferredSize.Width + (int)(6 * s);

        var btnConsole = new Button { Text = "Web 控制台", FlatStyle = FlatStyle.Flat, BackColor = gwOk ? Theme.Acc : Color.Gray, ForeColor = Theme.FcWhite, Font = btnFont, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, Padding = btnPad, Enabled = gwOk, Height = btnH };
        btnConsole.Click += (_, _) =>
        {
            try
            {
                string token = "";
                var cfg = JsonNode.Parse(File.ReadAllText(MainForm.CfgFullPath, Encoding.UTF8))?.AsObject();
                if (cfg != null && cfg.TryGetPropertyValue("gateway", out var gw) && gw is JsonObject gwo && gwo.TryGetPropertyValue("auth", out var au) && au is JsonObject auo && auo.TryGetPropertyValue("token", out var tk))
                    token = tk!.ToString();
                string url = MainForm.GatewayUrl + "?token=" + token;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { try { Process.Start(new ProcessStartInfo(MainForm.GatewayUrl) { UseShellExecute = true }); } catch { } }
        };
        btnConsole.Location = new Point(bx, btnY); body.Controls.Add(btnConsole);
        _btnWebConsole = btnConsole;

        y += (int)(34 * s);

        // 版本切换
        body.Controls.Add(SectionTitle("版本切换", pad, y)); y += (int)(22 * s);
        int vy = y + (int)(3 * s), vx = pad;
        void AddVerLbl(string t, Color? c = null, float s = 9f, FontStyle st = FontStyle.Regular)
        { var l = new Label { Text = t, ForeColor = c ?? Theme.Fc, Font = Theme.Font(s, st), AutoSize = true, BackColor = Color.Transparent, Location = new Point(vx, vy) }; body.Controls.Add(l); vx += l.PreferredSize.Width; }
        AddVerLbl("当前: v" + ver + "    目标版本: ", Theme.Fc2, 10f);
        vx += 6;
        var verCmb = new ComboBox { Width = 180, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, FlatStyle = FlatStyle.Popup, DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Font(9f), Location = new Point(vx, vy - 1) };
        verCmb.Items.Add("latest (最新)"); body.Controls.Add(verCmb); vx += 190;
        var swBtn = Theme.Btn("切换"); swBtn.Location = new Point(vx, y + 4); body.Controls.Add(swBtn); vx += swBtn.PreferredSize.Width + 8;
        var refBtn = Theme.Btn("刷新列表"); refBtn.Click += async (_, _) => { refBtn.Enabled = false; refBtn.Text = "加载中..."; var vs = await GetVersions(); verCmb.Items.Clear(); verCmb.Items.Add("latest (最新)"); foreach (var v in vs) verCmb.Items.Add(v); verCmb.SelectedIndex = 0; refBtn.Enabled = true; refBtn.Text = "刷新列表"; };
        refBtn.Location = new Point(vx, y + 4); body.Controls.Add(refBtn); vx += refBtn.PreferredSize.Width + 10;
        int verEndX = vx;
        y += (int)(34 * s);

        // 常驻实时控制台 — 高度自适应剩余空间
        int consoleH = Math.Max(100, availH - y - (int)(10 * s));
        
        // 首次打开面板时从持久化文件恢复日志（仅退出控制台不关网关的场景）
        if (_consoleLog.Length == 0 && File.Exists(_consoleLogPath))
        {
            try
            {
                var allLines = new List<string>();
                using var sr = new StreamReader(_consoleLogPath, new UTF8Encoding(false));
                string? l;
                while ((l = sr.ReadLine()) != null)
                    if (!string.IsNullOrWhiteSpace(l))
                        allLines.Add(l);
                // 只保留最后 100 行
                if (allLines.Count > 100)
                    allLines = allLines.GetRange(allLines.Count - 100, 100);
                _consoleLog.Append(string.Join("\r\n", allLines));
                if (allLines.Count > 0) _consoleLog.Append("\r\n");
            }
            catch { }
        }
        _consoleBox = new TextBox
        {
            Location = new Point(pad, y),
            Size = new Size(cw, consoleH),
            BackColor = Theme.IsDark ? Color.FromArgb(22, 22, 38) : Color.FromArgb(30, 30, 30),
            ForeColor = Theme.IsDark ? Color.FromArgb(120, 255, 120) : Color.FromArgb(0, 220, 0),
            Font = Theme.Mono(8f),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle
        };
        // 从 _consoleLog 加载已有内容（单次 Text 赋值，避免 Text+AppendText 重复渲染）
        var initText = _consoleLog.ToString();
        if (initText.Length > 50000) initText = TailText(initText, 50000);
        _consoleBox.Text = initText;
        _consoleBox.SelectionStart = _consoleBox.Text.Length;
        _consoleBox.ScrollToCaret();
        body.Controls.Add(_consoleBox);
        y += 190;

        // 自动附加守护：网关在线但无定时器
        if (gwOk && _healthTimer == null)
        {
            StartHealthTimer();
        }

        // 网关已运行但非本管理器启动 → 开始从文件跟踪日志
        if (gwOk && _gatewayProc == null)
        {
            _ = TailLogFile(null);
        }

        // 实时刷新卡片动态数据（5秒）
        if (_refreshTimer == null)
        {
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _refreshTimer.Tick += (_, _) => RefreshCards();
        }
        _refreshTimer.Start();

        // ── 按钮回调（定义在最后，捕获所有控件）──

        // 启动
        startBtn.Click += async (_, _) =>
        {
            startBtn.Enabled = false; startBtn.Text = "启动中..."; RepositionButtons();
            WriteConsole("━━ 启动网关 ━━");
            await Task.Run(KillGateway);
            if (!StartGatewayDirect()) { WriteConsole("❌ 启动失败"); body.Controls.Clear(); Build(body); return; }
            bool up = false;
            for (int i = 0; i < 15; i++) { await Task.Delay(2000); if (GwHttpOk()) { up = true; break; } }
            WriteConsole(up ? "✓ 网关就绪" : "⚠ 启动超时 (30s)");
            if (up) { StartHealthTimer(); SaveConfigBackup(); } else if (_gatewayProc != null) { WriteConsole("  网关可能启动较慢，守护将自动检测"); StartHealthTimer(); }
            UpdateGatewayStatus(up, _gatewayProc?.Id);
        };

        // 停止
        stopBtn.Click += async (_, _) =>
        {
            stopBtn.Enabled = false; stopBtn.Text = "停止中..."; RepositionButtons();
            WriteConsole("━━ 停止网关 ━━");
            StopHealthTimer();
            await Task.Run(KillGateway);
            try { _gatewayProc?.Kill(); } catch { }
            _gatewayProc?.Dispose(); _gatewayProc = null;
            WriteConsole("✓ 网关已停止");
            UpdateGatewayStatus(false, null);
        };

        // 重启
        restartBtn.Click += async (_, _) =>
        {
            restartBtn.Enabled = false; restartBtn.Text = "重启中..."; RepositionButtons();
            WriteConsole("━━ 重启网关 ━━");
            StopHealthTimer();
            await Task.Run(KillGateway);
            try { _gatewayProc?.Kill(); } catch { }
            _gatewayProc?.Dispose(); _gatewayProc = null;
            if (!StartGatewayDirect()) { WriteConsole("❌ 重启失败"); body.Controls.Clear(); Build(body); return; }
            bool up = false;
            for (int i = 0; i < 15; i++) { await Task.Delay(2000); if (GwHttpOk()) { up = true; break; } }
            WriteConsole(up ? "✓ 网关已重启" : "⚠ 重启超时 (30s)");
            if (up) StartHealthTimer(); else StartHealthTimer();
            UpdateGatewayStatus(up, _gatewayProc?.Id);
        };

        // 版本切换
        swBtn.Click += async (_, _) =>
        {
            if (!LicenseManager.CheckPro()) return;
            if (verCmb.SelectedItem == null) return;
            string t = verCmb.SelectedItem.ToString()!.Replace(" (最新)", "");
            swBtn.Enabled = false; refBtn.Enabled = false; verCmb.Enabled = false;
            WriteConsole($"━━ 版本切换 openclaw@{t} ━━");
            WriteConsole("停止网关...");
            StopHealthTimer();
            await Task.Run(KillGateway);
            try { _gatewayProc?.Kill(); } catch { }
            _gatewayProc?.Dispose(); _gatewayProc = null;

            var rtDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime");
            string registryArg = " --registry https://registry.npmjs.org";
            string installCmd = "npm install openclaw@" + t + " --prefix \"" + rtDir + "\" --omit=dev --scripts-prepend-node-path" + registryArg;
            bool installOk = await RunCmdLive(installCmd, 300000);

            if (!installOk)
            {
                WriteConsole("❌ 安装失败，请检查网络或版本号");
                swBtn.Enabled = true; refBtn.Enabled = true; verCmb.Enabled = true;
                return;
            }

            WriteConsole("✓ npm 安装完成");
            WriteConsole("启动网关...");
            if (!StartGatewayDirect()) { WriteConsole("❌ 启动失败"); swBtn.Enabled = true; refBtn.Enabled = true; verCmb.Enabled = true; return; }
            bool up = false;
            for (int i = 0; i < 15; i++) { await Task.Delay(2000); if (GwHttpOk()) { up = true; break; } }
            WriteConsole(up ? "✓ 版本切换完成" : "⚠ 启动超时 (30s)");
            if (up) { StartHealthTimer(); MainForm.RefreshOcVersion(); try { var f = body.FindForm(); if (f != null) f.Text = MainForm.WindowTitle(); } catch { } } else StartHealthTimer();
            swBtn.Enabled = true; refBtn.Enabled = true; verCmb.Enabled = true;
        };

        // 后台加载版本列表
        Task.Run(async () => {
            var vs = await GetVersions();
            body.Invoke(() => {
                if (vs.Count == 0)
                {
                    verCmb.Items.Clear(); verCmb.Items.Add("latest (最新) — 网络不可用");
                    verCmb.SelectedIndex = 0;
                    WriteConsole("⚠ 无法获取版本列表: 请检查网络连接");
                    return;
                }
                var sorted = new List<string> { "latest (最新)" };
                foreach (var v in vs) if (!sorted.Contains(v)) sorted.Add(v);
                verCmb.Items.Clear(); foreach (var v in sorted) verCmb.Items.Add(v);
                if (verCmb.SelectedIndex < 0) verCmb.SelectedIndex = 0;
                string lv = vs.FirstOrDefault() ?? "";
                string cv = VerSync();
                if (!string.IsNullOrEmpty(lv) && cv != "N/A" && lv != cv)
                {
                    var newVerLbl = Theme.Lbl("  有新版本 v" + lv + " !", Theme.QqOrange, 9f, FontStyle.Bold);
                    newVerLbl.Location = new Point(verEndX, vy);
                    body.Controls.Add(newVerLbl);
                }
            });
        });
    }

    // ── UI 辅助 ──

    FlowLayoutPanel Flow(int x, int y, int w, int h, bool wrap = false) => new FlowLayoutPanel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.Transparent, WrapContents = wrap };
    Label SectionTitle(string text, int x, int y) { var l = Theme.Lbl(text, Theme.Fc, 11f, FontStyle.Bold); l.Location = new Point(x, y); return l; }
    Panel Card(string title, string value, int w, int h, Color? valColor = null)
    {
        var p = new Panel { Size = new Size(w, h), BackColor = Theme.BgCard };
        float s = Theme.ScaleFactor;
        p.Paint += (s2, e) => { e.Graphics.DrawRectangle(new Pen(Theme.BdrLight), 0, 0, w - 1, h - 1); };
        p.Controls.Add(new Label { Text = title, ForeColor = Theme.Fc2, Font = Theme.Font(6.5f), AutoSize = true, Location = new Point((int)(8 * s), (int)(6 * s)), BackColor = Color.Transparent });
        p.Controls.Add(new Label { Text = value, ForeColor = valColor ?? Theme.Fc, Font = Theme.Font(9.5f, FontStyle.Bold), AutoSize = false, Size = new Size(w - (int)(16 * s), (int)(30 * s)), Location = new Point((int)(8 * s), (int)(24 * s)), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft });
        return p;
    }
    Button SmallBtn(string text) => new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = Theme.Acc, ForeColor = Theme.FcWhite, Font = Theme.Font(8f, FontStyle.Bold), Size = new Size(110, 22), FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };

    // ── 数据采集 ──

    (bool ok, int? pid) GwCheck()
    {
        try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(2) }; var r = h.GetAsync(MainForm.GatewayUrl).Result; if (!r.IsSuccessStatusCode) return (false, null); foreach (var proc in Process.GetProcessesByName("node")) { try { using var h2 = new HttpClient { Timeout = TimeSpan.FromSeconds(1) }; if (h2.GetAsync(MainForm.GatewayUrl).Result.IsSuccessStatusCode) return (true, proc.Id); } catch { } } return (false, null); } catch { return (false, null); }
    }
    bool GwInstalledCheck()
    {
        try
        {
            string node = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node.exe");
            string entry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "index.js");
            if (!File.Exists(entry)) entry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "openclaw.mjs");
            return File.Exists(node) && File.Exists(entry);
        }
        catch { return false; }
    }
    string CpuMemStr(int? p) { if (p == null) return "N/A"; try { using var pr = Process.GetProcessById(p.Value); return pr.TotalProcessorTime.TotalSeconds.ToString("F0") + "s / " + (pr.WorkingSet64 / 1048576.0).ToString("F1") + " MB"; } catch { return "N/A"; } }
    int ChCount()
    {
        try { var cfg = JsonNode.Parse(File.ReadAllText(MainForm.CfgFullPath, Encoding.UTF8))?.AsObject(); if (cfg != null && cfg.TryGetPropertyValue("channels", out var cn) && cn != null) { int c = 0; foreach (var kv in cn.AsObject()) if (kv.Value is JsonObject o && o.TryGetPropertyValue("enabled", out var e) && e != null && e.GetValueKind() == JsonValueKind.True) c++; return c; } } catch { }
        return 1;
    }
    string FindNodeExe()
    {
        var rt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node.exe");
        return File.Exists(rt) ? rt : "";
    }
    string VerSync()
    {
        try
        {
            string nodeExe = FindNodeExe();
            if (string.IsNullOrEmpty(nodeExe)) return "N/A";
            string ocEntry = OcEntryPath;
            if (string.IsNullOrEmpty(ocEntry)) return "N/A";
            var psi = new ProcessStartInfo { FileName = nodeExe, Arguments = "\"" + ocEntry + "\" --version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
            using var p = Process.Start(psi); if (p == null) return "N/A";
            p.WaitForExit(3000);
            var o = p.StandardOutput.ReadToEnd().Trim();
            var m = System.Text.RegularExpressions.Regex.Match(o, @"(\d+\.\d+\.\d+)");
            return m.Success ? m.Groups[1].Value : "N/A";
        }
        catch { return "N/A"; }
    }
    string StartTimeSync(int? p) { if (p == null) return "N/A"; try { using var pr = Process.GetProcessById(p.Value); return pr.StartTime.ToString("HH:mm:ss"); } catch { return "N/A"; } }
    string UpTimeSync(int? p) { if (p == null) return "N/A"; try { using var pr = Process.GetProcessById(p.Value); var ts = DateTime.Now - pr.StartTime; return ts.TotalHours >= 1 ? (int)ts.TotalHours + "h" + ts.Minutes + "m" : ts.Minutes + "m" + ts.Seconds + "s"; } catch { return "N/A"; } }
    string NodeVerSync() { try { string nodeExe = FindNodeExe(); if (string.IsNullOrEmpty(nodeExe)) return "未安装"; var psi = new ProcessStartInfo { FileName = nodeExe, Arguments = "-v", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 }; using var p = Process.Start(psi); if (p == null) return "未安装"; p.WaitForExit(3000); var v = p.StandardOutput.ReadToEnd().Trim(); return string.IsNullOrEmpty(v) ? "未安装" : v; } catch { return "未安装"; } }
    string OcVerSync() { var v = VerSync(); return v == "N/A" ? "未安装" : v; }
    string DefaultModelSync()
    {
        try { var cfg = JsonNode.Parse(File.ReadAllText(MainForm.CfgFullPath, Encoding.UTF8))?.AsObject(); if (cfg?.TryGetPropertyValue("agents", out var ag) == true) { if (ag!.AsObject().TryGetPropertyValue("defaults", out var df) && df is JsonObject d && d.TryGetPropertyValue("model", out var mn) && mn is JsonObject mobj && mobj.TryGetPropertyValue("primary", out var prim)) return prim!.ToString(); } } catch { }
        return "N/A";
    }
    string WorkspaceSync()
    {
        try
        {
            var cfg = JsonNode.Parse(File.ReadAllText(MainForm.CfgFullPath, Encoding.UTF8));
            var ws = cfg?["agents"]?["defaults"]?["workspace"]?.ToString();
            if (!string.IsNullOrEmpty(ws) && Directory.Exists(ws)) return ws;
        }
        catch { }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "workspace");
    }
    async Task<List<string>> GetVersions()
    {
        var list = new List<string>();
        // 直接用 npm view 获取版本列表（不用 HttpClient，避免代理/防火墙问题）
        try
        {
            string npmExe = NpmCmdPath;
            if (!string.IsNullOrEmpty(npmExe) && File.Exists(npmExe))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = npmExe,
                    Arguments = "view openclaw versions --json",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                // PATH 加 nodeDir 确保 npm 能找到 node
                var nodeDir = Path.GetDirectoryName(NodeExePath);
                if (!string.IsNullOrEmpty(nodeDir))
                    psi.EnvironmentVariables["PATH"] = nodeDir + ";" + Environment.GetEnvironmentVariable("PATH");
                using var p = Process.Start(psi);
                if (p != null)
                {
                    var stdout = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(30000);
                    if (!string.IsNullOrEmpty(stdout))
                    {
                        var arr = JsonNode.Parse(stdout);
                        if (arr is JsonArray ja)
                            foreach (var v in ja)
                            {
                                var s = v?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(s) && !s.Contains("-"))
                                    list.Add(s);
                            }
                    }
                }
            }
        }
        catch (Exception ex) { WriteConsole($"⚠ 版本列表获取失败: {ex.Message}"); }
        list.Sort((a, b) => { try { return new Version(b).CompareTo(new Version(a)); } catch { return string.Compare(b, a, StringComparison.OrdinalIgnoreCase); } });
        return list;
    }
    static string _configBackup = "";

    void SaveConfigBackup() { try { var p = MainForm.CfgFullPath; if (File.Exists(p)) _configBackup = File.ReadAllText(p, Encoding.UTF8); } catch { } }

    /// 恢复上一次能正常运行的配置备份
    void RestoreConfig()
    {
        try
        {
            if (string.IsNullOrEmpty(_configBackup)) { MessageBox.Show("没有可恢复的备份配置。", "提示"); return; }
            File.WriteAllText(MainForm.CfgFullPath, _configBackup, Encoding.UTF8);
            WriteConsole("✓ 配置已恢复到上一次正常状态");
        }
        catch (Exception ex) { WriteConsole("❌ 恢复失败: " + ex.Message); }
    }

    void FixConfig()
    {
        try { var psi = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c openclaw config validate 2>&1", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 }; using var p = Process.Start(psi); if (p == null) return; p.WaitForExit(15000); string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); MessageBox.Show(p.ExitCode == 0 ? ("配置正常\n\n" + o.Trim()) : ("配置问题:\n\n" + o.Trim()), "配置状态"); } catch (Exception ex) { MessageBox.Show("检查失败: " + ex.Message, "错误"); }
    }
    string NodeExePath => FindNodeExe();
    string NpmCmdPath
    {
        get
        {
            var dir = Path.GetDirectoryName(NodeExePath);
            if (string.IsNullOrEmpty(dir)) return "";
            var npmCmd = Path.Combine(dir, "npm.cmd");
            if (File.Exists(npmCmd) && Directory.Exists(Path.Combine(dir, "node_modules", "npm")))
                return npmCmd;
            return "";
        }
    }
    string OcEntryPath
    {
        get
        {
            var rt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "index.js");
            if (File.Exists(rt)) return rt;
            rt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "openclaw.mjs");
            if (File.Exists(rt)) return rt;
            return "";
        }
    }

    // ── 网关控制 ──

    /// <summary>TCP 端口探测（<1s），判断网关进程是否存活</summary>
    bool GwTcpOk()
    {
        try { using var tcp = new System.Net.Sockets.TcpClient(); var ar = tcp.BeginConnect("127.0.0.1", 18789, null, null); if (ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1))) { tcp.EndConnect(ar); return true; } return false; } catch { return false; }
    }

    /// <summary>TCP+HTTP 双重检测（必须在后台线程调用，不可在 UI 线程）</summary>
    bool? GwHealthOk()
    {
        // 先 TCP 检测，快
        if (!GwTcpOk()) return null; // 端口不通 → 真宕机
        // TCP 通了，做 HTTP 检测
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:18789/health");
            using var resp = GatewayHealthClient.Send(req);
            return resp.IsSuccessStatusCode ? true : false;
        }
        catch { return false; } // HTTP 超时 → 繁忙
    }

    bool GwHttpOk()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:18789/health");
            using var resp = GatewayHealthClient.Send(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    void KillGateway()
    {
        // 杀干净所有 node 和 cmd，防止旧进程残留导致日志重复
        try { foreach (var p in Process.GetProcessesByName("node")) { try { p.Kill(); } catch { } } } catch { }
        try { foreach (var p in Process.GetProcessesByName("cmd")) { try { p.Kill(); } catch { } } } catch { }
    }

    // ── 实时控制台 ──

    /// <summary>
    /// 写入控制台日志（带时间戳），同时更新缓冲区和可见控件
    /// </summary>
    void WriteConsole(string msg)
    {
        // 同一秒内相同消息去重
        var now = DateTime.Now;
        var sec = now.Hour * 3600 + now.Minute * 60 + now.Second;
        var line = $"[{now:HH:mm:ss}] {msg}\r\n";
        if (sec == _lastLogSec && line == _lastLogMsg) return;
        _lastLogSec = sec;
        _lastLogMsg = line;

        _consoleLog.Append(line);

        // 裁剪旧日志，防止内存无限增长
        if (_consoleLog.Length > 50000)
        {
            int cut = _consoleLog.Length - 40000;
            string full = _consoleLog.ToString();
            int nl = full.IndexOf('\n', cut);
            if (nl > 0) _consoleLog.Remove(0, nl + 1);
        }

        // 持久化到文件
        try { File.AppendAllText(_consoleLogPath, line, new UTF8Encoding(false)); } catch { }

        try { _consoleBox?.Invoke(() => { _consoleBox.AppendText(line); _consoleBox.ScrollToCaret(); }); }
        catch { }
    }

    // ── workspace 盘符修正 ──

    /// <summary>
    /// 修正 openclaw.json 中 workspace 的盘符，适配 U盘换电脑盘符变化
    /// </summary>
    void PatchWorkspaceDrive()
    {
        try
        {
            string cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "openclaw.json");
            if (!File.Exists(cfgPath)) return;

            var node = JsonNode.Parse(File.ReadAllText(cfgPath, Encoding.UTF8));
            if (node == null) return;

            string home = AppDomain.CurrentDomain.BaseDirectory;
            string ws = Path.Combine(home, "workspace");
            if (!Directory.Exists(ws)) { try { Directory.CreateDirectory(ws); } catch { ws = Path.Combine(home, ".openclaw", "workspace"); } }
            // 确保 workspace-state.json 存在（缺失则 Gateway 无法处理消息）
            try {
                var wsOc = Path.Combine(ws, ".openclaw");
                if (!Directory.Exists(wsOc)) Directory.CreateDirectory(wsOc);
                var statePath = Path.Combine(wsOc, "workspace-state.json");
                if (!File.Exists(statePath))
                    File.WriteAllText(statePath, "", Encoding.UTF8);
            } catch { }
            bool dirty = false;

            // agents.defaults.workspace
            var defs = node["agents"]?["defaults"];
            if (defs != null)
            {
                string? cur = defs["workspace"]?.ToString();
                if (!string.IsNullOrEmpty(cur) && cur != ws) { defs["workspace"] = ws; dirty = true; }
            }

            // agents.list[*].workspace（不改 agentDir 本地路径）
            var list = node["agents"]?["list"]?.AsArray();
            if (list != null)
            {
                foreach (var agent in list)
                {
                    string? cur = agent?["workspace"]?.ToString();
                    if (!string.IsNullOrEmpty(cur) && cur != ws) { agent!["workspace"] = ws; dirty = true; }
                }
            }

            if (dirty)
            {
                File.WriteAllText(cfgPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                WriteConsole($"已修正 workspace 盘符 -> {ws}");
            }
        }
        catch (Exception ex) { WriteConsole($"WARN: workspace 盘符修正失败: {ex.Message}"); }
    }

    // ── 直接启动 Gateway + 实时日志回传 ──

    /// <summary>
    /// 直接以独立进程启动 Gateway，stdout/stderr 重定向到文件（管理器退出后日志不丢失）
    /// </summary>
    bool StartGatewayDirect()
    {
        try
        {
            PatchWorkspaceDrive();

            string node = FindNodeExe();
            string entry = OcEntryPath;
            if (string.IsNullOrEmpty(node) || string.IsNullOrEmpty(entry))
            {
                WriteConsole("FATAL: Node.js 或 OpenClaw 入口未找到");
                return false;
            }

            // 清空旧日志文件，确保显示内容与当前网关运行对应
            try { Directory.CreateDirectory(Path.GetDirectoryName(_consoleLogPath)!); File.WriteAllText(_consoleLogPath, "", new UTF8Encoding(false)); } catch { }
            _consoleLog.Clear();
            // U盘版本：所有临时文件/日志都落在程序目录，不写系统盘
            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "temp");
            try { Directory.CreateDirectory(tempDir); } catch { }

            WriteConsole("启动网关...");

            // 用 cmd /c 启动：stdout/stderr 直接重定向到文件
            // 即使管理器退出，网关输出也会持续写入文件，重新打开面板时恢复
            // 注意：不能加外层引号，否则和内层路径引号冲突导致 cmd 解析失败
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // chcp 65001 强制 UTF-8，解决中文乱码；TrimEnd 防路径末尾反斜杠导致拼接异常
                Arguments = $"/c chcp 65001 > nul && set OPENCLAW_HOME={AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')} && set TMPDIR={tempDir} && set TEMP={tempDir} && set TMP={tempDir} && \"{node}\" \"{entry}\" gateway run --port 18789 >> \"{_consoleLogPath}\" 2>&1",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _gatewayProc = Process.Start(psi);
            if (_gatewayProc == null) return false;

            WriteConsole($"启动网关 PID:{_gatewayProc.Id}");

            // 后台持续从文件尾部读取新行，实时显示到控制台
            _ = TailLogFile(_gatewayProc);

            return true;
        }
        catch (Exception ex)
        {
            WriteConsole($"启动失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 后台从文件尾部持续读取，实时显示到控制台（管理器退出后文件仍被网关持续写入）
    /// proc 为 null 时表示网关由守护程序启动，持续跟踪直到面板关闭
    /// </summary>
    static CancellationTokenSource? _tailCts;

    async Task TailLogFile(Process? proc)
    {
        // 取消旧 tail，确保同时只有一个读文件
        _tailCts?.Cancel();
        _tailCts = new CancellationTokenSource();
        var ct = _tailCts.Token;
        long lastLen = 0;
        while ((proc == null || !proc.HasExited) && !ct.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(_consoleLogPath))
                {
                    var fi = new FileInfo(_consoleLogPath);
                    if (fi.Length > lastLen)
                    {
                        using var fs = new FileStream(_consoleLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        fs.Seek(lastLen, SeekOrigin.Begin);
                        using var sr = new StreamReader(fs, Encoding.UTF8);
                        string chunk = sr.ReadToEnd();
                        lastLen = fi.Length;
                        foreach (var raw in chunk.Split('\n'))
                        {
                            var line = raw.TrimEnd('\r', '\n');
                            if (string.IsNullOrEmpty(line)) continue;
                            string logLine = line + "\r\n";
                            _consoleLog.Append(logLine);
                            try { _consoleBox?.Invoke(() => { _consoleBox.AppendText(logLine); _consoleBox.ScrollToCaret(); }); } catch { }
                        }
                    }
                }
            }
            catch { }
            await Task.Delay(1500, ct);
        }
        if (proc != null) WriteConsole("网关进程已退出");
    }

    // ── 内建健康检查守护 ──

    /// <summary>
    /// 5秒定时刷新动态卡片数据（PID/CPU/内存/运行时长）
    /// </summary>
    void RefreshCards()
    {
        try
        {
            var (ok, pid) = GwCheck();
            var cpuMem = CpuMemStr(pid);
            var up = UpTimeSync(pid);
            var st = StartTimeSync(pid);
            _cardGw?.Invoke(() => { _cardGw.Text = ok ? "在线 (PID: " + pid + ")" : "离线"; _cardGw.ForeColor = ok ? Theme.Grn : Theme.Red; });
            _cardCpu?.Invoke(() => _cardCpu.Text = cpuMem);
            _cardUptime?.Invoke(() => _cardUptime.Text = up);
            _cardStart?.Invoke(() => _cardStart.Text = st);
            _btnWebConsole?.Invoke(() => { _btnWebConsole.Enabled = ok; _btnWebConsole.BackColor = ok ? Theme.Acc : Color.Gray; });
            _fixBtn?.Invoke(() => { _fixBtn.Enabled = ok; _fixBtn.BackColor = ok ? Theme.Acc : Color.Gray; });
        }
        catch { }
    }

    void UpdateGatewayStatus(bool gwOk, int? pid)
    {
        _gwDot?.Invoke(() => _gwDot.ForeColor = gwOk ? Theme.Grn : Theme.Red);
        string text = gwOk ? "运行中 (PID: " + pid + ")  " : "已停止  ";
        _gwStatusLbl?.Invoke(() => { _gwStatusLbl.Text = text; _gwStatusLbl.AutoSize = true; });
        _startBtn?.Invoke(() => { _startBtn.Enabled = !gwOk; _startBtn.BackColor = gwOk ? Color.Gray : Theme.Grn; _startBtn.Text = "启动"; });
        _stopBtn?.Invoke(() => { _stopBtn.Enabled = gwOk; _stopBtn.BackColor = !gwOk ? Color.Gray : Theme.Red; _stopBtn.Text = "停止"; });
        _restartBtn?.Invoke(() => { _restartBtn.Enabled = gwOk; _restartBtn.BackColor = !gwOk ? Color.Gray : Theme.QqOrange; _restartBtn.Text = "重启"; });
        // 重排按钮位置，适配状态文字宽度变化
        RepositionButtons();
    }

    void RepositionButtons()
    {
        _gwStatusLbl?.Invoke(() =>
        {
            if (_startBtn == null) return;
            float s = Theme.ScaleFactor;
            int gap = (int)(6 * s);
            int baseX = _gwStatusLbl.Location.X + _gwStatusLbl.PreferredWidth + (int)(20 * s);
            int btnY = _startBtn.Location.Y;
            _startBtn.Location = new Point(baseX, btnY);
            var bx = baseX + _startBtn.Width + gap;
            if (_stopBtn != null) { _stopBtn.Location = new Point(bx, btnY); bx += _stopBtn.Width + gap; }
            if (_restartBtn != null) { _restartBtn.Location = new Point(bx, btnY); bx += _restartBtn.Width + gap; }
            if (_fixBtn != null) { _fixBtn.Location = new Point(bx, btnY); bx += _fixBtn.Width + gap; }
            if (_btnWebConsole != null) { _btnWebConsole.Location = new Point(bx, btnY); }
        });
    }

    static bool _healthLogged;

    void StartHealthTimer()
    {
        StopHealthTimer();
        _healthTimer = new System.Windows.Forms.Timer { Interval = 60000 };
        _healthTimer.Tick += (_, _) => HealthCheck();
        _healthFailCount = 0;
        _healthTimer.Start();
        if (!_healthLogged) { WriteConsole("守护已附加 (60s检测)"); _healthLogged = true; }
    }

    void StopHealthTimer()
    {
        _healthTimer?.Stop();
        _healthTimer?.Dispose();
        _healthTimer = null;
        _healthFailCount = 0;
    }

    /// <summary>
    /// TCP+HTTP 双重探活：TCP 不通 = 真宕机；TCP 通但 HTTP 超时 = 繁忙
    /// </summary>
    static bool _recovering;
    static bool _checkRunning;

    void HealthCheck()
    {
        if (_recovering || _checkRunning) return;
        _checkRunning = true;
        // 后台线程执行 IO，避免阻塞 UI
        _ = Task.Run(() =>
        {
            var state = GwHealthOk();
            _consoleBox?.BeginInvoke(() =>
            {
                try
                {
                if (state == true) { _healthFailCount = 0; return; }
                if (state == false)
                {
                    WriteConsole("⏳ 网关繁忙 (/health 超时)，等待下次检测");
                    return;
                }
                // TCP 端口不通 → 真宕机
                _healthFailCount++;
                WriteConsole($"⚠ 健康检查失败 #{_healthFailCount} (端口不通)");
                if (_healthFailCount >= 3)
                {
                    WriteConsole("🔄 自动拉起网关...");
                    _healthFailCount = 0;
                    _recovering = true;
                    try { _gatewayProc?.Kill(); } catch { }
                    _gatewayProc?.Dispose();
                    _gatewayProc = null;
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        if (StartGatewayDirect())
                        {
                            for (int i = 0; i < 30; i++)
                            {
                                Thread.Sleep(2000);
                                if (GwHttpOk()) { WriteConsole("✓ 网关已恢复"); _recovering = false; _checkRunning = false; return; }
                            }
                            WriteConsole("⚠ 网关恢复超时 (60s)");
                        }
                        _recovering = false;
                    });
                }
                }
                finally { _checkRunning = false; }
            });
        });
    }

    // ── 命令行辅助（保留旧方法签名）──

    async Task RunCmd(string cmd, bool wait = true, bool admin = false) { try { string fileName, args; if (cmd.StartsWith("openclaw ")) { fileName = NodeExePath; args = "\"" + OcEntryPath + "\" " + cmd["openclaw ".Length..]; } else if (cmd.StartsWith("npm ") || cmd.StartsWith("npm.cmd ")) { var npm = NpmCmdPath; if (string.IsNullOrEmpty(npm) || !File.Exists(npm)) { fileName = "cmd.exe"; args = "/c " + cmd; } else { fileName = npm; args = cmd.StartsWith("npm.cmd ") ? cmd["npm.cmd ".Length..] : cmd["npm ".Length..]; } } else { fileName = "cmd.exe"; args = "/c " + cmd; } var psi = new ProcessStartInfo { FileName = fileName, Arguments = args, UseShellExecute = admin, CreateNoWindow = !admin, WindowStyle = admin ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal }; psi.EnvironmentVariables["OPENCLAW_HOME"] = AppDomain.CurrentDomain.BaseDirectory; if (!admin) { psi.RedirectStandardOutput = true; psi.RedirectStandardError = true; psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8; } else { psi.Verb = "runas"; } var p = Process.Start(psi); if (p != null && wait) { try { await p.WaitForExitAsync(new CancellationTokenSource(30000).Token); } catch (OperationCanceledException) { } if (!p.HasExited) { try { p.Kill(); } catch { } } } } catch { } }

    /// <summary>
    /// 执行命令行，实时输出到面板控制台
    /// </summary>
    async Task<bool> RunCmdLive(string cmd, int timeoutMs = 120000)
    {
        try
        {
            string fileName, args;
            if (cmd.StartsWith("npm ") || cmd.StartsWith("npm.cmd ")) { var npm = NpmCmdPath; if (string.IsNullOrEmpty(npm) || !File.Exists(npm)) { fileName = "cmd.exe"; args = "/c " + cmd; } else { fileName = npm; args = cmd.StartsWith("npm.cmd ") ? cmd["npm.cmd ".Length..] : cmd["npm ".Length..]; } }
            else if (cmd.StartsWith("openclaw ")) { fileName = NodeExePath; args = "\"" + OcEntryPath + "\" " + cmd["openclaw ".Length..]; }
            else { fileName = "cmd.exe"; args = "/c " + cmd; }

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["OPENCLAW_HOME"] = AppDomain.CurrentDomain.BaseDirectory;
            var nodeDir = Path.GetDirectoryName(NodeExePath);
            if (!string.IsNullOrEmpty(nodeDir))
                psi.EnvironmentVariables["PATH"] = nodeDir + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");

            using var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (s, e) => { if (e.Data != null) WriteConsole(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) WriteConsole(e.Data); };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            try
            {
                await p.WaitForExitAsync(new CancellationTokenSource(timeoutMs).Token);
            }
            catch (OperationCanceledException)
            {
                if (!p.HasExited) { try { p.Kill(); } catch { } }
                WriteConsole($"⏱ 命令超时 ({timeoutMs / 1000}s)");
                return false;
            }

            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            WriteConsole("❌ 错误: " + ex.Message);
            return false;
        }
    }

    async Task<string> RunCmdCapture(string cmd, int timeoutMs = 30000)
    {
        try
        {
            string fileName, args;
            if (cmd.StartsWith("openclaw ")) { fileName = NodeExePath; args = "\"" + OcEntryPath + "\" " + cmd["openclaw ".Length..]; }
            else if (cmd.StartsWith("npm ") || cmd.StartsWith("npm.cmd ")) { var npm = NpmCmdPath; if (string.IsNullOrEmpty(npm) || !File.Exists(npm)) { fileName = "cmd.exe"; args = "/c " + cmd; } else { fileName = npm; args = cmd.StartsWith("npm.cmd ") ? cmd["npm.cmd ".Length..] : cmd["npm ".Length..]; } }
            else { fileName = "cmd.exe"; args = "/c " + cmd; }

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["OPENCLAW_HOME"] = AppDomain.CurrentDomain.BaseDirectory;
            var nodeDir2 = Path.GetDirectoryName(NodeExePath);
            if (!string.IsNullOrEmpty(nodeDir2))
                psi.EnvironmentVariables["PATH"] = nodeDir2 + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");

            using var p = Process.Start(psi);
            if (p == null) return "";
            var outStr = await p.StandardOutput.ReadToEndAsync();
            var errStr = await p.StandardError.ReadToEndAsync();
            try { await p.WaitForExitAsync(new CancellationTokenSource(timeoutMs).Token); } catch (OperationCanceledException) { if (!p.HasExited) { try { p.Kill(); } catch { } } }
            return (outStr + "\n" + errStr).Trim();
        }
        catch { return ""; }
    }
}

