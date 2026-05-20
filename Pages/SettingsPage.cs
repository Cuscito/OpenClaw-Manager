using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawManager;

public class SettingsPage
{
    Panel body;
    Dictionary<string, Control> inputs = new();

    public void Build(Panel b)
    {
        body = b; body.Controls.Clear();
        inputs.Clear();

        body.AutoScroll = false;
        body.HorizontalScroll.Visible = false;
        body.HorizontalScroll.Enabled = false;

        var title = new Label { Text = "\u7CFB\u7EDF\u8BBE\u7F6E", ForeColor = Theme.Fc, Font = Theme.Font(13f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 12) };
        body.Controls.Add(title);

        var cfg = OpenClawRuntime.ReadConfig();

        // ---- Gateway Settings ----
        var gw = cfg?["gateway"]?.AsObject();
        string port = gw?["port"]?.ToString() ?? "18789";
        string bind = gw?["bind"]?.ToString() ?? "loopback";
        string authMode = gw?["auth"]?["mode"]?.ToString() ?? "token";
        string token = gw?["auth"]?["token"]?.ToString() ?? "";
        string logLevel = gw?["logging"]?["level"]?.ToString() ?? "info";
        int heartbeatMin = 30;
        try { heartbeatMin = int.TryParse(cfg?["agents"]?["defaults"]?["heartbeat"]?["everyMinutes"]?.ToString(), out var hm) ? hm : 30; } catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }

        // ---- Agents Defaults ----
        var agents = cfg?["agents"]?.AsObject();
        var defs = agents?["defaults"]?.AsObject();
        int maxTokens = 0;
        try { maxTokens = int.TryParse(defs?["maxTokens"]?.ToString(), out var mt) ? mt : 0; } catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }
        // contextWindow 从 models.providers 读取（真正生效的位置）
        int contextWin = 0;
        try
        {
            var provModels = cfg?["models"]?["providers"]?.AsObject();
            if (provModels != null)
                foreach (var pk in provModels)
                    if (pk.Value is JsonObject po && po["models"] is JsonArray arr)
                        foreach (var m in arr)
                            if (m is JsonObject mo && mo["contextWindow"] != null)
                            { int.TryParse(mo["contextWindow"]?.ToString(), out contextWin); break; }
        }
        catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }
        string modelName = "";
        try { modelName = defs?["model"]?["primary"]?.ToString() ?? ""; } catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }
        string sessionLimit = defs?["session"]?["limits"]?["maxTurnsPerSession"]?.ToString() ?? "100";

        // ---- Model list - from agents.defaults.models ----
        var modelList = new List<string>();
        try
        {
            var models = cfg?["agents"]?["defaults"]?["models"]?.AsObject();
            if (models != null)
                foreach (var kv in models)
                    modelList.Add(kv.Key);
            // Also check providers.entries if exists
            var provModels = cfg?["providers"]?.AsObject()?["entries"]?.AsObject();
            if (provModels != null)
                foreach (var kv in provModels)
                    if (kv.Value is JsonObject po)
                    {
                        string pid = kv.Key;
                        var pmodels = po["models"]?.AsObject();
                        if (pmodels != null)
                            foreach (var mk in pmodels)
                                if (!modelList.Contains(pid + "/" + mk.Key))
                                    modelList.Add(pid + "/" + mk.Key);
                    }
        }
        catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }
        if (modelList.Count == 0) { modelList.Add("deepseek/deepseek-v4-pro"); modelList.Add("deepseek/deepseek-v4-flash"); }

        // ---- Web Search (Brave) ----
        string braveKey = "";
        try { braveKey = cfg?["plugins"]?["entries"]?["brave"]?["config"]?["webSearch"]?["apiKey"]?.ToString() ?? ""; }
        catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }

        int y = 44;

        // --- SECTION: Gateway ---
        AddSection(ref y, "\u2501 Gateway");
        y = AddCombo(y, "\u7ED1\u5B9A\u5730\u5740 (Bind)", bind, new[] { "loopback", "lan", "public" });
        y = AddText(y, "\u7AEF\u53E3 (Port)", port, "18789");
        y = AddCombo(y, "\u8BA4\u8BC1\u6A21\u5F0F (Auth)", authMode, new[] { "token", "none", "basic" });
        y = AddText(y, "\u8BBF\u95EE\u4EE4\u724C (Token)", token, "\u81EA\u52A8\u751F\u6210\u7684 token", true);
        y = AddCombo(y, "\u65E5\u5FD7\u7EA7\u522B (Log)", logLevel, new[] { "debug", "info", "warn", "error" });
        y = AddText(y, "\u5FC3\u8DF3\u95F4\u9694\u5206\u949F", heartbeatMin.ToString(), "30", false);

        // --- SECTION: AI Models ---
        AddSection(ref y, "\u2501 AI \u6A21\u578B");
        y = AddCombo(y, "\u9ED8\u8BA4\u6A21\u578B", modelName, modelList.ToArray());
        y = AddText(y, "\u4E0A\u4E0B\u6587\u7A97\u53E3", contextWin > 0 ? contextWin.ToString() : "", "1M=1048576, \u9ED8\u8BA4200K");
        y = AddText(y, "\u6700\u5927\u8F93\u51FA\u957F\u5EA6", maxTokens > 0 ? maxTokens.ToString() : "", "0=\u9ED8\u8BA4(8192)");
        y = AddText(y, "\u4F1A\u8BDD\u8F6E\u6570\u9650\u5236", sessionLimit, "100");

        // --- SECTION: Web Search ---
        AddSection(ref y, "\u2501 \u7F51\u7EDC\u641C\u7D22 (Brave)");
        y = AddText(y, "Brave API Key", braveKey, "\u586B\u5199\u540E\u5F00\u542F\u7F51\u7EDC\u641C\u7D22\u80FD\u529B", true);

        // --- SECTION: Workspace ---
        AddSection(ref y, "\u2501 \u5B58\u50A8");
        // 从 agents.defaults.workspace 读取实际生效的工作目录
        string ws = defs?["workspace"]?.ToString() 
            ?? cfg?["workspace"]?.ToString() 
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "workspace");
        y = AddText(y, "\u5DE5\u4F5C\u76EE\u5F55 (Workspace)", ws, "");

        // --- SECTION: License ---
        AddSection(ref y, "\u2501 \u6CE8\u518C\u72B6\u6001");
        var statusColor = LicenseManager.IsPro ? Theme.Grn : (LicenseManager.IsTrialExpired ? Theme.Red : Theme.QqOrange);
        var statusLbl = new Label { Text = "  " + LicenseManager.StatusText, ForeColor = statusColor, Font = Theme.Font(10f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y) };
        body.Controls.Add(statusLbl);
        y += 20;

        // 按钮行：激活 + 绑定类型标签 + 下拉框，垂直居中对齐
        var activateBtn = new Button { Text = LicenseManager.IsPro ? "\u66F4\u65B0\u6CE8\u518C\u7801" : "\u8F93\u5165\u6CE8\u518C\u7801", FlatStyle = FlatStyle.Flat, BackColor = LicenseManager.IsPro ? Theme.Grn : Theme.Acc, ForeColor = Theme.FcWhite, Location = new Point(12, y), Size = new Size(120, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        var isPro = LicenseManager.IsPro;
        var licenseBinding = LicenseManager.LicenseBindingType;
        var bindLbl = new Label { Text = "\u7ED1\u5B9A\u7C7B\u578B:", ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(138, y + 7) };
        var bindingCb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Font(9f), Size = new Size(60, 32), Location = new Point(bindLbl.Left + bindLbl.PreferredWidth, y + 4) };
        bindingCb.Items.AddRange(["\u7535\u8111", "U\u76D8"]);
        bindingCb.SelectedIndex = (isPro && licenseBinding == "U\u76D8\u7ED1\u5B9A") ? 1 : 0;
        bindingCb.Enabled = !isPro;
        y += 34;

        // 设备码行：限制宽度防止 64 位哈希冲出窗口产生水平滚动条
        var machineLbl = new Label { ForeColor = Theme.Fc2, Font = Theme.Font(7.5f), AutoSize = false, BackColor = Color.Transparent, Location = new Point(14, y + 2), Size = new Size(Math.Max(body.ClientSize.Width - 120, 300), 18) };
        var copyBtn = new Button { Text = "\uD83D\uDCCB \u590D\u5236", FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Theme.Acc, Font = Theme.Font(8f), Size = new Size(64, 22), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } };

        Action updateDeviceCode = () => {
            var isUsb = bindingCb.SelectedIndex == 1;
            var code = isUsb ? LicenseValidator.GetUsbDeviceId() : LicenseValidator.GetMachineCode();
            machineLbl.Text = (isUsb ? "U\u76D8\u7801: " : "\u673A\u5668\u7801: ") + code;
            copyBtn.Location = new Point(machineLbl.Right + 6, y);
        };
        bindingCb.SelectedIndexChanged += (_, _) => updateDeviceCode();
        updateDeviceCode(); // 初始赋值

        copyBtn.MouseEnter += (_, _) => copyBtn.ForeColor = Theme.QqBlue;
        copyBtn.MouseLeave += (_, _) => copyBtn.ForeColor = Theme.Acc;
        copyBtn.Click += (_, _) => {
            var code = bindingCb.SelectedIndex == 1 ? LicenseValidator.GetUsbDeviceId() : LicenseValidator.GetMachineCode();
            Clipboard.SetText(code); copyBtn.Text = "\u2713 \u5DF2\u590D\u5236"; copyBtn.ForeColor = Theme.Grn;
            Task.Run(async () => { await Task.Delay(2000); copyBtn.Invoke(() => { copyBtn.Text = "\uD83D\uDCCB \u590D\u5236"; copyBtn.ForeColor = Theme.Acc; }); });
        };
        body.Controls.Add(activateBtn); body.Controls.Add(bindLbl);
        body.Controls.Add(bindingCb);
        body.Controls.Add(machineLbl);
        body.Controls.Add(copyBtn);

        activateBtn.Click += (_, _) =>
        {
            var dlg = new Form
            {
                Text = LicenseManager.IsPro ? "\u66F4\u65B0\u6CE8\u518C\u7801" : "\u8F93\u5165\u6CE8\u518C\u7801",
                Size = new Size(520, 240),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = Theme.BgWhite, ForeColor = Theme.Fc,
                Font = Theme.Font(9f)
            };
            dlg.Controls.Add(new Label { Text = "\u8BF7\u8F93\u5165\u6CE8\u518C\u7801:", ForeColor = Theme.Fc, Font = Theme.Font(10f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(20, 20) });
            var codeBox = new TextBox { Location = new Point(20, 50), Size = new Size(460, 28), Font = Theme.Font(10f), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "OCM-XXXXX-XXXXX-XXXXX-..." };
            dlg.Controls.Add(codeBox);

            var hintLbl = new Label { Text = "\u673A\u5668\u7801: " + LicenseValidator.GetMachineCode() + "\nU\u76D8\u7801: " + LicenseValidator.GetUsbDeviceId(), ForeColor = Theme.Fc2, Font = Theme.Font(8f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(20, 78) };
            dlg.Controls.Add(hintLbl);

            var btnOK = new Button { Text = LicenseManager.IsPro ? "\u66F4\u65B0" : "\u6FC0\u6D3B", FlatStyle = FlatStyle.Flat, BackColor = Theme.Acc, ForeColor = Theme.FcWhite, Size = new Size(90, 34), Location = new Point(260, 130), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } };
            var btnCancel = new Button { Text = "\u53D6\u6D88", FlatStyle = FlatStyle.Flat, BackColor = Theme.Fc2, ForeColor = Theme.FcWhite, Size = new Size(90, 34), Location = new Point(360, 130), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } };

            btnOK.Click += (_, _) =>
            {
                btnOK.Enabled = false;
                btnOK.Text = "\u9A8C\u8BC1\u4E2D...";
                var (ok, msg) = LicenseManager.Activate(codeBox.Text.Trim());
                MessageBox.Show(msg, ok ? "\u6FC0\u6D3B\u6210\u529F" : "\u6FC0\u6D3B\u5931\u8D25", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                dlg.Close();
                if (ok) RefreshMainForm();
                body.Controls.Clear(); Build(body);
            };
            btnCancel.Click += (_, _) => dlg.Close();
            dlg.Controls.Add(btnOK);
            dlg.Controls.Add(btnCancel);
            dlg.ShowDialog();
            body.Controls.Clear(); Build(body);
        };
        y += 20;

        // --- Buttons ---
        y += 12;
        var actions = new FlowLayoutPanel
        {
            Location = new Point(12, y),
            Size = new Size(Math.Max(body.ClientSize.Width - 24, 260), 76),
            BackColor = Color.Transparent,
            WrapContents = true,
            AutoSize = false
        };
        body.Controls.Add(actions);

        var btnSave = Theme.Btn("保存配置");
        btnSave.Click += (_, _) => SaveAll();
        actions.Controls.Add(btnSave);

        var btnRestart = Theme.BtnDanger("重启 Gateway");
        btnRestart.Click += (_, _) => RunCmd("openclaw gateway restart", "\u91CD\u542F");
        actions.Controls.Add(btnRestart);

        var btnStop = Theme.BtnWhite("停止 Gateway");
        btnStop.Click += (_, _) => RunCmd("openclaw gateway stop", "\u505C\u6B62");
        actions.Controls.Add(btnStop);

        var btnDoctor = Theme.StyledButton("配置诊断", Theme.Warn, Theme.FcWhite, 0);
        btnDoctor.Click += (_, _) => RunCmd("openclaw doctor --fix", "\u914D\u7F6E\u4FEE\u590D");
        actions.Controls.Add(btnDoctor);

        body.Resize += (_, _) =>
        {
            if (!actions.IsDisposed)
                actions.Width = Math.Max(body.ClientSize.Width - 24, 260);
            // 调整所有输入框和下拉框的宽度，适配面板尺寸变化
            int boxW = Math.Max(body.ClientSize.Width - 230, 200);
            foreach (var kv in inputs)
            {
                var c = kv.Value;
                if (c is TextBox tb) { tb.Width = boxW; }
                else if (c is ComboBox cb) { cb.Width = boxW; }
            }
        };
        // 控件都添加完后，开启 AutoScroll（只垂直），只在内容超出时滚动
        body.AutoScrollMinSize = new Size(0, y + 40);
        body.AutoScroll = body.AutoScrollMinSize.Height > body.ClientSize.Height;
    }

    void AddSection(ref int y, string title)
    {
        y += 8;
        body.Controls.Add(new Label { Text = title, ForeColor = Theme.QqBlue, Font = Theme.Font(10f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y) });
        y += 22;
    }

    int AddText(int y, string label, string value, string placeholder, bool isPwd = false)
    {
        body.Controls.Add(new Label { Text = label, ForeColor = Theme.Fc2, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y + 6) });
        int w = body.ClientSize.Width - 220;
        if (w < 200) w = 200;
        var tb = new TextBox { Text = value, PlaceholderText = placeholder, Location = new Point(200, y + 2), Size = new Size(w, 26), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(10f), UseSystemPasswordChar = isPwd };
        body.Controls.Add(tb);
        inputs[label] = tb;
        return y + 28;
    }

    int AddCombo(int y, string label, string value, string[] items)
    {
        body.Controls.Add(new Label { Text = label, ForeColor = Theme.Fc2, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y + 6) });
        int w = body.ClientSize.Width - 220;
        if (w < 200) w = 200;
        var cb = new ComboBox { Location = new Point(200, y + 2), Size = new Size(w, 26), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Font(10f), FlatStyle = FlatStyle.Flat };
        cb.Items.AddRange(items);
        cb.SelectedIndex = Math.Max(0, items.ToList().IndexOf(value));
        body.Controls.Add(cb);
        inputs[label] = cb;
        return y + 28;
    }

    string Val(string key) => inputs.TryGetValue(key, out var c) ? (c is TextBox tb ? tb.Text : c is ComboBox cb ? cb.SelectedItem?.ToString() ?? "" : "") : "";

    void SaveAll()
    {
        try
        {
            var cfg = OpenClawRuntime.ReadConfig() ?? new JsonObject();
            var gw = cfg["gateway"]?.AsObject(); if (gw == null) { gw = new JsonObject(); cfg["gateway"] = gw; }
            if (int.TryParse(Val("\u7AEF\u53E3 (Port)"), out var p)) gw["port"] = p;
            gw["bind"] = Val("\u7ED1\u5B9A\u5730\u5740 (Bind)");
            if (gw["auth"] is not JsonObject a) { a = new JsonObject(); gw["auth"] = a; }
            a["mode"] = Val("\u8BA4\u8BC1\u6A21\u5F0F (Auth)");
            string tk = Val("\u8BBF\u95EE\u4EE4\u724C (Token)"); if (!string.IsNullOrEmpty(tk)) a["token"] = tk;
            if (gw["logging"] is not JsonObject lg) { lg = new JsonObject(); gw["logging"] = lg; }
            lg["level"] = Val("\u65E5\u5FD7\u7EA7\u522B (Log)");

            // Agents defaults
            var agents = cfg["agents"]?.AsObject(); if (agents == null) { agents = new JsonObject(); cfg["agents"] = agents; }
            var defs = agents["defaults"]?.AsObject(); if (defs == null) { defs = new JsonObject(); agents["defaults"] = defs; }
            string model = Val("\u9ED8\u8BA4\u6A21\u578B");
            if (!string.IsNullOrEmpty(model)) { var m = defs["model"]?.AsObject(); if (m == null) { m = new JsonObject(); defs["model"] = m; } m["primary"] = model; }
            if (int.TryParse(Val("\u6700\u5927\u8F93\u51FA\u957F\u5EA6"), out var mt) && mt > 0) defs["maxTokens"] = mt;
            string sl = Val("\u4F1A\u8BDD\u8F6E\u6570\u9650\u5236");
            if (!string.IsNullOrEmpty(sl)) { var s = defs["session"]?.AsObject(); if (s == null) { s = new JsonObject(); defs["session"] = s; } var l = s["limits"]?.AsObject(); if (l == null) { l = new JsonObject(); s["limits"] = l; } l["maxTurnsPerSession"] = int.TryParse(sl, out var nsl) ? nsl : 100; }
            // 上下文窗口 → 写入 models.providers 所有模型的 contextWindow + agents.defaults.contextTokens 做 cap
            string cw = Val("\u4E0A\u4E0B\u6587\u7A97\u53E3");
            if (!string.IsNullOrEmpty(cw) && int.TryParse(cw, out var cwVal) && cwVal > 0)
            {
                defs["contextTokens"] = cwVal;
                var provs = cfg["models"]?["providers"]?.AsObject();
                if (provs != null)
                    foreach (var pk in provs)
                        if (pk.Value is JsonObject po && po["models"] is JsonArray arr)
                            foreach (var m in arr)
                                if (m is JsonObject mo) { mo["contextWindow"] = cwVal; mo["contextTokens"] = cwVal; }
            }
            if (int.TryParse(Val("\u5FC3\u8DF3\u95F4\u9694\u5206\u949F"), out var hm) && hm > 0) { var hb = defs["heartbeat"]?.AsObject(); if (hb == null) { hb = new JsonObject(); defs["heartbeat"] = hb; } hb["everyMinutes"] = hm; }

            // Brave web search
            string bk = Val("Brave API Key");
            if (!string.IsNullOrEmpty(bk))
            {
                var plugins = cfg["plugins"]?.AsObject(); if (plugins == null) { plugins = new JsonObject(); cfg["plugins"] = plugins; }
                var entries = plugins["entries"]?.AsObject(); if (entries == null) { entries = new JsonObject(); plugins["entries"] = entries; }
                var brave = entries["brave"]?.AsObject(); if (brave == null) { brave = new JsonObject(); entries["brave"] = brave; }
                brave["enabled"] = true;
                var bc = brave["config"]?.AsObject(); if (bc == null) { bc = new JsonObject(); brave["config"] = bc; }
                var bs = bc["webSearch"]?.AsObject(); if (bs == null) { bs = new JsonObject(); bc["webSearch"] = bs; }
                bs["apiKey"] = bk;
            }

            // 工作目录变更时迁移旧文件到新目录
string ws = Val("\u5DE5\u4F5C\u76EE\u5F55 (Workspace)");
if (!string.IsNullOrEmpty(ws))
{
    string oldWs = defs?["workspace"]?.ToString() ?? cfg?["workspace"]?.ToString() ?? "";
    if (!string.IsNullOrEmpty(oldWs) && !string.Equals(oldWs, ws, StringComparison.OrdinalIgnoreCase) && Directory.Exists(oldWs) && !Directory.Exists(ws))
    {
        try
        {
            Directory.CreateDirectory(ws);
            foreach (var f in Directory.GetFiles(oldWs, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(oldWs.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(ws, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(f, dest, false);
            }
            MessageBox.Show("已从旧目录迁移 " + Directory.GetFiles(oldWs, "*", SearchOption.AllDirectories).Length + " 个文件到新工作目录。\n\n旧目录: " + oldWs + "\n新目录: " + ws, "迁移完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("文件迁移失败: " + ex.Message, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    // 写入 agents.defaults.workspace（网关实际读取的路径）
    if (defs == null) { defs = new JsonObject(); agents["defaults"] = defs; }
    defs["workspace"] = ws;
}

            File.WriteAllText(MainForm.CfgFullPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            MessageBox.Show("\u914D\u7F6E\u5DF2\u4FDD\u5B58\uFF0C\u8BF7\u91CD\u542F Gateway \u751F\u6548\u3002", "\u4FDD\u5B58\u6210\u529F", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("\u4FDD\u5B58\u5931\u8D25: " + ex.Message, "\u9519\u8BEF", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void RunCmd(string args, string name)
    {
        try
        {
            // U盘版本：用程序目录自带的 Node.js + OpenClaw，不依赖 PATH
            string node = OpenClawRuntime.NodeExe;
            string entry = OpenClawRuntime.OpenClawEntry;
            if (string.IsNullOrEmpty(node) || string.IsNullOrEmpty(entry))
            {
                MessageBox.Show("未找到 Node.js 或 OpenClaw 入口。\n预期路径: " + entry, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = node,
                Arguments = $"\"{entry}\" {args}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            MessageBox.Show(name + " 命令已执行。", name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(name + "失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void RefreshMainForm()
    {
        var mf = body.FindForm() as MainForm;
        if (mf == null) return;
        mf.Text = $"OpenClaw 管理器 v{MainForm.AppVersion}{(LicenseManager.IsPro ? " 专业版" : " 试用" + LicenseManager.TrialDaysRemaining + "天")}";
        mf.BuildSidebar();
    }
}
