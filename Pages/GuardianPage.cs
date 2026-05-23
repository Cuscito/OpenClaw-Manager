using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawManager;

public class GuardianPage
{
    static readonly HttpClient GatewayHealthClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    Panel body;
    RichTextBox logBox;
    Label statusLbl, pathLbl;
    TextBox dirBox;
    Button btnInstall, btnToggle;
    System.Windows.Forms.Timer logTimer;
    string guardianDir;
    string detectedNode, detectedEntry;

    public void Build(Panel p)
    {
        body = p; body.Controls.Clear();

        if (!LicenseManager.CheckPro())
        {
            body.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianProFeature"), ForeColor = Theme.Fc2, Font = Theme.Font(12f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 20) });
            body.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianActivatePro"), ForeColor = Theme.Fc2, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 50) });
            return;
        }

        guardianDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guardian");

        var title = new Label { Text = "守护程序", ForeColor = Theme.Fc, Font = Theme.Font(13f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 12) };
        body.Controls.Add(title);

        // Status
        statusLbl = new Label { ForeColor = Theme.Fc2, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 38) };
        body.Controls.Add(statusLbl);

        // Detected paths
        pathLbl = new Label { ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 58) };
        body.Controls.Add(pathLbl);

        int y = 98;

        // Install directory row
        var dirRow = new FlowLayoutPanel { Location = new Point(12, y), Size = new Size(body.ClientSize.Width - 24, 34), BackColor = Color.Transparent, WrapContents = false };
        dirRow.Controls.Add(new Label { Text = "安装目录: ", ForeColor = Theme.Fc2, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 6, 0, 0) });
        dirBox = new TextBox { Text = guardianDir, Width = 260, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, Font = Theme.Font(9f), BorderStyle = BorderStyle.FixedSingle };
        dirRow.Controls.Add(dirBox);
        var btnBrowse = new Button { Text = "...", Width = 32, Height = 24, FlatStyle = FlatStyle.Flat, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, FlatAppearance = { BorderSize = 1 } };
        btnBrowse.Click += (_, _) => { using var dlg = new FolderBrowserDialog { SelectedPath = guardianDir }; if (dlg.ShowDialog() == DialogResult.OK) dirBox.Text = dlg.SelectedPath; };
        dirRow.Controls.Add(btnBrowse);
        body.Controls.Add(dirRow);
        y += 40;

        // Action buttons
        var btnRow = new FlowLayoutPanel { Location = new Point(12, y), Size = new Size(body.ClientSize.Width - 24, 40), BackColor = Color.Transparent };
        var btnDetect = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianDetectEnv"), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x66, 0x66, 0x66), ForeColor = Theme.FcWhite, Size = new Size(110, 34), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        btnDetect.Click += (_, _) => DetectEnvironment();
        btnRow.Controls.Add(btnDetect);

        btnInstall = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianInstall"), FlatStyle = FlatStyle.Flat, BackColor = Theme.QqBlue, ForeColor = Theme.FcWhite, Size = new Size(130, 34), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        btnInstall.Click += (_, _) => InstallGuardian();
        btnRow.Controls.Add(btnInstall);

        var btnUninstall = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianUninstall"), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Theme.FcWhite, Size = new Size(100, 34), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        btnUninstall.Click += (_, _) => UninstallGuardian();
        btnRow.Controls.Add(btnUninstall);

        btnToggle = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianStop"), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0xF4, 0x43, 0x36), ForeColor = Theme.FcWhite, Size = new Size(140, 34), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        btnToggle.Click += (_, _) => ToggleGuardian();
        btnRow.Controls.Add(btnToggle);

        body.Controls.Add(btnRow);
        y += 46;

        // Log viewer
        logBox = new RichTextBox
        {
            Location = new Point(12, y),
            Size = new Size(body.ClientSize.Width - 24, body.ClientSize.Height - y - 16),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(0xD4, 0xD4, 0xD4),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9f),
            ReadOnly = true, WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        body.Controls.Add(logBox);

        // Initial detect + status
        DetectEnvironment();
        UpdateStatus();
        RefreshLog();
        body.Resize += (_, _) => {
            logBox.Size = new Size(body.ClientSize.Width - 24, body.ClientSize.Height - y - 16);
        };

        // Auto-refresh log every 10 seconds
        logTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        logTimer.Tick += (_, _) => { try { if (Directory.Exists(guardianDir)) RefreshLog(); } catch { } };
        logTimer.Start();
    }

    #region Detection
    public void DetectEnvironment()
    {
        guardianDir = dirBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(guardianDir)) guardianDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guardian");

        detectedNode = FindNodeExe();
        detectedEntry = FindOpenClawEntry(detectedNode);

        string nodeInfo = string.IsNullOrEmpty(detectedNode) ? "❌ Node.js 未找到" : "✅ " + detectedNode;
        string ocInfo = string.IsNullOrEmpty(detectedEntry) ? "❌ OpenClaw 入口未找到" : "✅ " + detectedEntry;
        pathLbl.Text = nodeInfo + "\n" + ocInfo;
        pathLbl.AutoSize = true;

        UpdateStatus();
    }

    public static string FindNodeExe()
    {
        // 仅检测软件自带 runtime 目录
        var rt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node.exe");
        return File.Exists(rt) ? rt : "";
    }

    public static string FindOpenClawEntry(string nodeExe)
    {
        // 仅检测软件自带 runtime 目录
        var rtEntry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "index.js");
        if (File.Exists(rtEntry)) return rtEntry;
        rtEntry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "openclaw.mjs");
        if (File.Exists(rtEntry)) return rtEntry;
        return "";
    }
    #endregion

    #region Status & Log
    void UpdateStatus()
    {
        bool taskExists = false;
        try
        {
            var psi = new ProcessStartInfo { FileName = "schtasks.exe", Arguments = "/query /tn \"OpenClaw Gateway\"", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            var p = Process.Start(psi); p?.WaitForExit(3000);
            taskExists = p?.ExitCode == 0;
        }
        catch { }

        bool scriptsExist = Directory.Exists(guardianDir) &&
            File.Exists(Path.Combine(guardianDir, "launch-gateway.vbs")) &&
            File.Exists(Path.Combine(guardianDir, "launch-gateway.ps1"));

        statusLbl.Text = "目录: " + guardianDir + "  |  " +
            (scriptsExist ? "✅ 守护已安装" : "⚠ 守护未安装") + "  |  " +
            (taskExists ? "✅ 计划任务已注册" : "⚠ 计划任务未注册");

        // Update install button text
        if (btnInstall != null)
            btnInstall.Text = scriptsExist ? OpenClawManager.Properties.LanguageManager.GetString("GuardianReinstall") : OpenClawManager.Properties.LanguageManager.GetString("GuardianInstall");

        // Update toggle button state
        if (btnToggle != null)
        {
            bool gwRunning = IsGatewayRunning();
            bool taskEnabled = IsTaskEnabled();
            if (gwRunning || taskEnabled)
            {
                btnToggle.Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianStop");
                btnToggle.BackColor = Color.FromArgb(0xF4, 0x43, 0x36);
            }
            else
            {
                btnToggle.Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianStart");
                btnToggle.BackColor = Theme.Grn;
            }
        }
    }

    void RefreshLog()
    {
        try
        {
            var logPath = Path.Combine(guardianDir, "guardian.log");
            if (File.Exists(logPath))
            {
                var text = File.ReadAllText(logPath, Encoding.UTF8);
                if (text.Length > 100000)
                    text = "─── 文件过大，显示末尾 100KB ───\n" + text.Substring(text.Length - 100000);
                logBox.Text = text;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
            }
            else
            {
                logBox.Text = "守护日志还没有内容，安装守护后每分钟会自动记录。\n预期位置: " + logPath;
            }
        }
        catch (Exception ex)
        {
            logBox.Text = "读取日志失败: " + ex.Message;
        }
    }
    #endregion

    #region Install / Uninstall

    void InstallGuardian()
    {
        guardianDir = dirBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(guardianDir))
        {
            MessageBox.Show("请先指定安装目录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Re-detect paths
        DetectEnvironment();

        if (string.IsNullOrEmpty(detectedNode) || string.IsNullOrEmpty(detectedEntry))
        {
            var dr = MessageBox.Show(
                "检测未完成：\n" +
                "Node.js: " + (string.IsNullOrEmpty(detectedNode) ? "未找到" : detectedNode) + "\n" +
                "OpenClaw: " + (string.IsNullOrEmpty(detectedEntry) ? "未找到" : detectedEntry) + "\n\n" +
                "是否仍然继续安装？（守护可能无法正常工作）",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;
        }

        try
        {
            Directory.CreateDirectory(guardianDir);

            // Generate launch-gateway.vbs
            var vbsPath = Path.Combine(guardianDir, "launch-gateway.vbs");
            var ps1Path = Path.Combine(guardianDir, "launch-gateway.ps1");
            File.WriteAllText(vbsPath,
                "Set sh = CreateObject(\"WScript.Shell\")\r\n" +
                "sh.Run \"powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"\"" + ps1Path + "\"\"\", 0, False\r\n" +
                "Set sh = Nothing\r\n", new UTF8Encoding(false));

            // Generate launch-gateway.ps1 with full recovery logic
            var logPath = Path.Combine(guardianDir, "guardian.log");

            var ps1 = new StringBuilder();
            ps1.AppendLine("$port = 18789");
            ps1.AppendLine("$logFile = \"" + logPath + "\"");
            ps1.AppendLine("$nodePath = \"" + detectedNode + "\"");
            ps1.AppendLine("$entryPath = \"" + detectedEntry + "\"");
            ps1.AppendLine();
            ps1.AppendLine("$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path");
            ps1.AppendLine("$parentDir = Split-Path -Parent $scriptDir");
            ps1.AppendLine("$cfgPath = Join-Path $parentDir '.openclaw\\openclaw.json'");
            ps1.AppendLine("$lockFile = Join-Path $scriptDir 'guardian.lock'");
            ps1.AppendLine();
            ps1.AppendLine("# Prevent concurrent guardian runs");
            ps1.AppendLine("if (Test-Path $lockFile) {");
            ps1.AppendLine("    $lockAge = ((Get-Date) - (Get-Item $lockFile).LastWriteTime).TotalSeconds");
            ps1.AppendLine("    if ($lockAge -lt 90) { Write-Log \"Guardian locked (age: $([math]::Round($lockAge,0))s), exiting\"; exit 0 }");
            ps1.AppendLine("    Write-Log \"Stale lock ($([math]::Round($lockAge,0))s), removing\"");
            ps1.AppendLine("    Remove-Item $lockFile -Force");
            ps1.AppendLine("}");
            ps1.AppendLine("New-Item -ItemType File -Path $lockFile -Force | Out-Null");
            ps1.AppendLine();
            ps1.AppendLine("function Write-Log($msg) {");
            ps1.AppendLine("    $ts = Get-Date -Format \"yyyy-MM-dd HH:mm:ss\"");
            ps1.AppendLine("    \"[$ts] $msg\" | Out-File -FilePath $logFile -Append -Encoding UTF8");
            ps1.AppendLine("}");
            ps1.AppendLine();
            ps1.AppendLine("# Runtime fallback: re-detect if paths changed");
            ps1.AppendLine("if (-not (Test-Path $nodePath)) {");
            ps1.AppendLine("    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path");
            ps1.AppendLine("    $parentDir = Split-Path -Parent $scriptDir");
            ps1.AppendLine("    $rtNode = Join-Path $parentDir 'runtime\\node.exe'");
            ps1.AppendLine("    if (Test-Path $rtNode) { $nodePath = $rtNode }");
            ps1.AppendLine("    else { Write-Log 'FATAL: Node.js not found'; exit 1 }");
            ps1.AppendLine("}");
            ps1.AppendLine("if (-not (Test-Path $entryPath)) {");
            ps1.AppendLine("    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path");
            ps1.AppendLine("    $parentDir = Split-Path -Parent $scriptDir");
            ps1.AppendLine("    $rtEntry = Join-Path $parentDir 'runtime\\node_modules\\openclaw\\dist\\index.js'");
            ps1.AppendLine("    if (-not (Test-Path $rtEntry)) { $rtEntry = Join-Path $parentDir 'runtime\\node_modules\\openclaw\\openclaw.mjs' }");
            ps1.AppendLine("    if (Test-Path $rtEntry) { $entryPath = $rtEntry }");
            ps1.AppendLine("    else { Write-Log 'FATAL: OpenClaw entry not found'; exit 1 }");
            ps1.AppendLine("}");
            ps1.AppendLine();
            ps1.AppendLine("# HTTP health check");
            ps1.AppendLine("try {");
            ps1.AppendLine("    $req = [System.Net.WebRequest]::Create(\"http://127.0.0.1:$port/health\")");
            ps1.AppendLine("    $req.Timeout = 5000");
            ps1.AppendLine("    $resp = $req.GetResponse()");
            ps1.AppendLine("    $resp.Close()");
            ps1.AppendLine("    Write-Log \"OK\"");
            ps1.AppendLine("    exit 0");
            ps1.AppendLine("} catch {}");
            ps1.AppendLine();
            ps1.AppendLine("Write-Log \"Gateway down, launching...\"");
            ps1.AppendLine("# Ensure config is accessible to gateway");
            ps1.AppendLine("$ocDir = Join-Path $parentDir '.openclaw'");
            ps1.AppendLine("if (-not (Test-Path $ocDir)) { New-Item -ItemType Directory -Path $ocDir -Force | Out-Null }");
            ps1.AppendLine("Copy-Item -Path $cfgPath -Destination (Join-Path $ocDir 'openclaw.json') -Force");
            ps1.AppendLine("$env:OPENCLAW_HOME = $parentDir");
            ps1.AppendLine("$env:TMPDIR = $env:TMP");
            ps1.AppendLine("$env:OPENCLAW_GATEWAY_PORT = \"$port\"");
            ps1.AppendLine();
            ps1.AppendLine("$psi = New-Object System.Diagnostics.ProcessStartInfo");
            ps1.AppendLine("$psi.FileName = $nodePath");
            ps1.AppendLine("$psi.Arguments = \"`\"$entryPath`\" gateway run --port $port\"");
            ps1.AppendLine("$psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden");
            ps1.AppendLine("$psi.CreateNoWindow = $true");
            ps1.AppendLine("$psi.UseShellExecute = $true");
            ps1.AppendLine();
            ps1.AppendLine("try {");
            ps1.AppendLine("    $proc = [System.Diagnostics.Process]::Start($psi)");
            ps1.AppendLine("    $pidNum = $proc.Id");
            ps1.AppendLine("    Write-Log \"Started PID=$pidNum\"");
            ps1.AppendLine("    $startTime = Get-Date");
            ps1.AppendLine("    while ($true) {");
            ps1.AppendLine("        Start-Sleep -Seconds 3");
            ps1.AppendLine("        try {");
            ps1.AppendLine("            $req = [System.Net.WebRequest]::Create(\"http://127.0.0.1:$port/health\")");
            ps1.AppendLine("            $req.Timeout = 5000");
            ps1.AppendLine("            $resp = $req.GetResponse()");
            ps1.AppendLine("            $resp.Close()");
            ps1.AppendLine("            $elapsedSec = [math]::Round((Get-Date - $startTime).TotalSeconds, 1)");
            ps1.AppendLine("            Write-Log \"Recovered in ${elapsedSec}s\"");
            ps1.AppendLine("            break");
            ps1.AppendLine("        } catch {}");
            ps1.AppendLine("        if (((Get-Date) - $startTime).TotalSeconds -ge 120) {");
            ps1.AppendLine("            Write-Log \"Timeout\"");
            ps1.AppendLine("            break");
            ps1.AppendLine("        }");
            ps1.AppendLine("    }");
            ps1.AppendLine("} catch {");
            ps1.AppendLine("    Write-Log \"ERROR: $_\"");
            ps1.AppendLine("}");
            ps1.AppendLine("exit 0");
            File.WriteAllText(ps1Path, ps1.ToString(), new UTF8Encoding(false));

            // Register scheduled task (needs admin)
            // Delete old tasks first
            try { RunSchTasks("/delete /tn \"OpenClaw Gateway\" /f", admin: true); } catch { }
            try { RunSchTasks("/delete /tn \"OpenClaw Gateway Watchdog\" /f", admin: true); } catch { }
            try { RunSchTasks("/delete /tn \"OpenClaw-Guardian\" /f", admin: true); } catch { }

            string userName = Environment.UserName;
            var result = RunSchTasks($"/create /tn \"OpenClaw Gateway\" /tr \"wscript.exe //nologo \"{vbsPath}\"\" /sc minute /mo 1 /ru \"{userName}\" /f /it", admin: true);

            if (!result.success)
            {
                logBox.Text = "⚠ 守护脚本已生成，但计划任务注册失败。\n\n" +
                    "点击“安装/重新安装”时会弹出UAC提权窗口，请点击“是”授权。";
                UpdateStatus();
                return;
            }

            // Trigger first run
            try { RunSchTasks("/run /tn \"OpenClaw Gateway\"", admin: true); } catch { }

            logBox.Text = "✅ 守护程序安装成功！\n\n" +
                "安装目录: " + guardianDir + "\n" +
                "Node.js: " + (detectedNode ?? "N/A") + "\n" +
                "OpenClaw: " + (detectedEntry ?? "N/A") + "\n" +
                "计划任务: OpenClaw Gateway (每1分钟)\n" +
                "检测方式: HTTP GET /health\n" +
                "日志文件: " + Path.Combine(guardianDir, "guardian.log") + "\n\n" +
                "守护已开始运行。";
            logBox.SelectionStart = 0;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show("安装失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void UninstallGuardian()
    {
        try
        {
            RunSchTasks("/delete /tn \"OpenClaw Gateway\" /f", admin: true);
            RunSchTasks("/delete /tn \"OpenClaw Gateway Watchdog\" /f", admin: true);
            RunSchTasks("/delete /tn \"OpenClaw-Guardian\" /f", admin: true);

            var dr = MessageBox.Show(
                "计划任务已删除。\n\n是否同时删除守护文件目录？\n" + guardianDir,
                "确认删除文件", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                try { Directory.Delete(guardianDir, true); }
                catch (Exception ex) { MessageBox.Show("删除目录失败：" + ex.Message, "提示"); }
            }

            UpdateStatus();
            logBox.Text = "✅ 守护程序已卸载。";
        }
        catch (Exception ex)
        {
            MessageBox.Show("卸载失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    (bool success, string output) RunSchTasks(string args, bool admin = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = args,
                UseShellExecute = admin,
                RedirectStandardOutput = !admin,
                RedirectStandardError = !admin,
                CreateNoWindow = !admin,
                WindowStyle = admin ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };
            if (admin) psi.Verb = "runas";
            using var p = Process.Start(psi);
            if (p == null) return (false, "Failed to start schtasks");
            if (!admin)
            {
                p.WaitForExit(10000);
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                return (p.ExitCode == 0, output);
            }
            else
            {
                p.WaitForExit(15000);
                return (p.ExitCode == 0, p.ExitCode == 0 ? "OK" : "ExitCode=" + p.ExitCode);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
    #endregion

    #region Toggle Guardian
    async void ToggleGuardian()
    {
        bool gwRunning = IsGatewayRunning();
        bool taskEnabled = IsTaskEnabled();
        btnToggle.Enabled = false;

        if (gwRunning || taskEnabled)
        {
            // === STOP: disable task + kill gateway ===
            btnToggle.Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianStopping");
            btnToggle.BackColor = Color.Gray;
            logBox.Text = "⏳ 正在停止守护和网关...";

            RunSchTasks("/change /tn \"OpenClaw Gateway\" /disable", admin: true);
            try { foreach (var p in Process.GetProcessesByName("node")) { try { p.Kill(); } catch { } } } catch { }

            await Task.Delay(2000);
            bool down = !IsGatewayRunning();
            int waited = 2;
            while (!down && waited < 10)
            {
                await Task.Delay(1000);
                down = !IsGatewayRunning();
                waited++;
            }

            logBox.Text = down
                ? "✅ 守护已停止，网关已关闭。"
                : "⚠ 网关可能仍在运行，请手动检查。";
        }
        else
        {
            // === START: enable task, let guardian auto-launch gateway ===
            btnToggle.Text = OpenClawManager.Properties.LanguageManager.GetString("GuardianStarting");
            btnToggle.BackColor = Color.Gray;
            logBox.Text = "⏳ 守护已启用，等待守护自动拉起网关...";

            RunSchTasks("/change /tn \"OpenClaw Gateway\" /enable", admin: true);
            
            bool up = false;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(2000);
                if (IsGatewayRunning()) { up = true; break; }
            }

            logBox.Text = up
                ? "✅ 网关和守护启动成功！"
                : "⚠ 启动超时（60秒），请点“刷新状态”查看日志。";
        }

        logBox.SelectionStart = 0;
        btnToggle.Enabled = true;
        UpdateStatus();
        await Task.Delay(500);
        RefreshLog();
    }

    bool IsGatewayRunning()
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

    bool IsTaskEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = "schtasks.exe", Arguments = "/query /tn \"OpenClaw Gateway\" /fo CSV", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            var output = p.StandardOutput.ReadToEnd();
            return output.Contains("\"Ready\"") || output.Contains("\"Running\"");
        }
        catch { return false; }
    }
    #endregion
}

