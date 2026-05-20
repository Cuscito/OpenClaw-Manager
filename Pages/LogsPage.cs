using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenClawManager;

public class LogsPage
{
    RichTextBox logBox = null!;
    Panel body = null!;
    System.Windows.Forms.Timer? autoTimer;
    string logDir = "";
    string? _currentLogPath;

    public void Build(Panel p)
    {
        body = p;
        body.Controls.Clear();

        var title = new Label { Text = "运行日志", ForeColor = Theme.Fc, Font = Theme.Font(13f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 12) };
        body.Controls.Add(title);

        // 状态指示灯
        var statusDot = new Label { Text = "●", ForeColor = Theme.Grn, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(110, 14) };
        body.Controls.Add(statusDot);

        var refreshBtn = new Button { Text = "刷新", FlatStyle = FlatStyle.Flat, BackColor = Theme.Acc, ForeColor = Color.White, Font = Theme.Font(9f), Location = new Point(body.ClientSize.Width - 180, 10), Size = new Size(56, 28), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        refreshBtn.Click += (_, _) => RefreshLog(statusDot);
        body.Controls.Add(refreshBtn);

        var autoBtn = new Button { Text = "实时", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x52, 0xC4, 0x1A), ForeColor = Color.White, Font = Theme.Font(9f), Location = new Point(body.ClientSize.Width - 118, 10), Size = new Size(56, 28), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        autoBtn.Click += (_, _) => ToggleAuto(autoBtn);
        body.Controls.Add(autoBtn);

        var clearBtn = new Button { Text = "清屏", FlatStyle = FlatStyle.Flat, BackColor = Theme.BgElevated, ForeColor = Theme.Fc, Font = Theme.Font(9f), Location = new Point(body.ClientSize.Width - 56, 10), Size = new Size(44, 28), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        clearBtn.Click += (_, _) => { logBox.Clear(); };
        body.Controls.Add(clearBtn);

        logBox = new RichTextBox
        {
            Location = new Point(12, 44),
            Size = new Size(body.ClientSize.Width - 24, body.ClientSize.Height - 60),
            BackColor = Theme.IsDark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(0xF5, 0xF5, 0xF5),
            ForeColor = Theme.IsDark ? Color.FromArgb(0xD4, 0xD4, 0xD4) : Color.FromArgb(0x33, 0x33, 0x33),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f),
            ReadOnly = true, WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        body.Controls.Add(logBox);

        // 自动扫描最新 log
        logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "logs");
        RefreshLog(statusDot);
    }

    void RefreshLog(Label statusDot)
    {
        try
        {
            // 找最新的 gateway log
            if (!Directory.Exists(logDir))
            {
                logBox.Text = "日志目录不存在:\n" + logDir + "\n\n请确保网关已启动。";
                statusDot.ForeColor = Theme.Red;
                return;
            }

            var latest = Directory.GetFiles(logDir, "*.log")
                .Where(f => !f.Contains("config-"))
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .FirstOrDefault();

            if (latest == null)
            {
                logBox.Text = "暂无网关日志。\n\n请确保网关已启动并产生日志输出。";
                statusDot.ForeColor = Theme.Warn;
                return;
            }

            _currentLogPath = latest;
            statusDot.ForeColor = Theme.Grn;

            var size = new FileInfo(latest).Length;
            // 文件共享读，避免被仪表盘独占锁阻塞
            int maxBytes = 200 * 1024;
            string text;
            using (var fs = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (size > maxBytes) fs.Seek(-maxBytes, SeekOrigin.End);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                text = sr.ReadToEnd();
            }
            if (size > maxBytes) text = "┄┄ 结尾 " + (maxBytes / 1024) + "KB ┄┄\n" + text;

            logBox.Text = text;
            logBox.SelectionStart = logBox.Text.Length;
            logBox.ScrollToCaret();
        }
        catch (Exception ex)
        {
            logBox.Text = "日志读取失败: " + ex.Message;
            statusDot.ForeColor = Theme.Red;
        }
    }

    void ToggleAuto(Button btn)
    {
        if (autoTimer != null)
        {
            autoTimer.Stop(); autoTimer.Dispose(); autoTimer = null;
            btn.Text = "实时"; btn.BackColor = Color.FromArgb(0x52, 0xC4, 0x1A);
            return;
        }
        autoTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        autoTimer.Tick += (_, _) =>
        {
            if (!body.IsHandleCreated || body.IsDisposed) { autoTimer?.Stop(); autoTimer?.Dispose(); autoTimer = null; return; }
            if (_currentLogPath != null)
            {
                try
                {
                    string txt;
                    using (var fs = new FileStream(_currentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                        txt = sr.ReadToEnd();
                    if (txt.Length > 200 * 1024)
                        txt = txt.Substring(txt.Length - 200 * 1024);
                    if (logBox.Text != txt)
                    {
                        logBox.Text = txt;
                        logBox.SelectionStart = logBox.Text.Length;
                        logBox.ScrollToCaret();
                    }
                }
                catch { }
            }
        };
        autoTimer.Start();
        btn.Text = "暂停"; btn.BackColor = Color.FromArgb(0xF4, 0x43, 0x36);
    }
}
