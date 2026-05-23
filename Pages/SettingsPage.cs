using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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

        var title = new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsTitle"), ForeColor = Theme.Fc, Font = Theme.Font(13f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 12) };
        body.Controls.Add(title);

        var cfg = OpenClawRuntime.ReadConfig();

        // ---- Gateway Settings ----
        var gw = cfg?["gateway"]?.AsObject();
        string port = gw?["port"]?.ToString() ?? "18789";
        string bind = gw?["bind"]?.ToString() ?? "loopback";
        string authMode = NormalizeGatewayAuthMode(gw?["auth"]?["mode"]?.ToString());
        string token = gw?["auth"]?["token"]?.ToString() ?? "";
        string logLevel = cfg?["logging"]?["level"]?.ToString() ?? "info";
        int heartbeatMin = 30;
        try { var hb = cfg?["agents"]?["defaults"]?["heartbeat"]?.AsObject(); var every = hb?["every"]?.ToString(); if (!string.IsNullOrEmpty(every) && every.EndsWith("m") && int.TryParse(every.TrimEnd('m'), out var hm)) heartbeatMin = hm; } catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }

        // ---- Agents Defaults ----
        var agents = cfg?["agents"]?.AsObject();
        var defs = agents?["defaults"]?.AsObject();
        int maxTokens = 0;
        try { maxTokens = int.TryParse(defs?["maxTokens"]?.ToString(), out var mt) ? mt : 0; } catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }
        // UI controls the runtime cap (contextTokens), not the native model contextWindow metadata.
        int contextWin = 0;
        try
        {
            contextWin = ReadPositiveInt(defs?["contextTokens"]);
            if (contextWin <= 0)
            {
                var provModels = cfg?["models"]?["providers"]?.AsObject();
                if (provModels != null)
                    foreach (var pk in provModels)
                        if (pk.Value is JsonObject po && po["models"] is JsonArray arr)
                            foreach (var m in arr)
                                if (m is JsonObject mo)
                                {
                                    contextWin = ReadPositiveInt(mo["contextTokens"]);
                                    if (contextWin <= 0) contextWin = ReadPositiveInt(mo["contextWindow"]);
                                    if (contextWin > 0) break;
                                }
            }
        }
        catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }
        string modelName = "";
        try { modelName = defs?["model"]?["primary"]?.ToString() ?? ""; } catch (Exception ex) { OpenClawRuntime.LogError("SettingsPage.Build", ex); }
        string sessionLimit = "100";

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
        AddSection(ref y, OpenClawManager.Properties.LanguageManager.GetString("SettingsSectionGateway"));
        y = AddCombo(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsBindAddr"), bind, new[] { "loopback", "lan", "public" });
        y = AddText(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsPort"), port, "18789");
        y = AddCombo(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsAuthMode"), authMode, new[] { "token", "none", "password" });
        y = AddText(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsAuthToken"), token, OpenClawManager.Properties.LanguageManager.GetString("SettingsTokenPlaceholder"), true);
        y = AddCombo(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsLogLevel"), logLevel, new[] { "debug", "info", "warn", "error" });
        y = AddText(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsHeartbeatMin"), heartbeatMin.ToString(), "30", false);

        // --- SECTION: AI Models ---
        AddSection(ref y, OpenClawManager.Properties.LanguageManager.GetString("SettingsSectionAIModels"));
        y = AddCombo(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsDefaultModel"), modelName, modelList.ToArray());
        y = AddText(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsContextWindow"), contextWin > 0 ? contextWin.ToString() : "", OpenClawManager.Properties.LanguageManager.GetString("SettingsContextWindowPH"));
        y = AddText(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsMaxOutputLen"), maxTokens > 0 ? maxTokens.ToString() : "", OpenClawManager.Properties.LanguageManager.GetString("SettingsMaxOutputPH"));
        y = AddText(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsSessionLimit"), sessionLimit, "100");

        // --- SECTION: Web Search ---
        AddSection(ref y, OpenClawManager.Properties.LanguageManager.GetString("SettingsSectionWebSearch"));
        y = AddText(y, "Brave API Key", braveKey, OpenClawManager.Properties.LanguageManager.GetString("SettingsBraveKeyPH"), true);

        // --- SECTION: Workspace ---
        AddSection(ref y, OpenClawManager.Properties.LanguageManager.GetString("SettingsSectionStorage"));
        // 从 agents.defaults.workspace 读取实际生效的工作目录
        string ws = defs?["workspace"]?.ToString() 
            ?? cfg?["workspace"]?.ToString() 
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "workspace");
        y = AddText(y, OpenClawManager.Properties.LanguageManager.GetString("SettingsWorkspaceDir"), ws, "");

        // --- SECTION: License ---
        AddSection(ref y, OpenClawManager.Properties.LanguageManager.GetString("SettingsSectionLicense"));
        var statusColor = LicenseManager.IsPro ? Theme.Grn : (LicenseManager.IsTrialExpired ? Theme.Red : Theme.QqOrange);
        var statusLbl = new Label { Text = "  " + LicenseManager.StatusText, ForeColor = statusColor, Font = Theme.Font(10f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y) };
        body.Controls.Add(statusLbl);
        y += 20;

        // 按钮行：激活 + 绑定类型标签 + 下拉框，垂直居中对齐
        var activateBtn = new Button { Text = LicenseManager.IsPro ? OpenClawManager.Properties.LanguageManager.GetString("SettingsUpdateLicense") : OpenClawManager.Properties.LanguageManager.GetString("SettingsEnterLicense"), FlatStyle = FlatStyle.Flat, BackColor = LicenseManager.IsPro ? Theme.Grn : Theme.Acc, ForeColor = Theme.FcWhite, Location = new Point(12, y), Size = new Size(120, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        var isPro = LicenseManager.IsPro;
        var licenseBinding = LicenseManager.LicenseBindingType;
        var bindLbl = new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsBindingType"), ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(138, y + 7) };
        var bindingCb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Font(9f), Size = new Size(60, 32), Location = new Point(bindLbl.Left + bindLbl.PreferredWidth, y + 4) };
        bindingCb.Items.AddRange([OpenClawManager.Properties.LanguageManager.GetString("SettingsBindingPC"), OpenClawManager.Properties.LanguageManager.GetString("SettingsBindingUSB")]);
        bindingCb.SelectedIndex = (isPro && licenseBinding == OpenClawManager.Properties.LanguageManager.GetString("SettingsUSBBinding")) ? 1 : 0;
        bindingCb.Enabled = !isPro;
        y += 34;

        // 设备码行：限制宽度防止 64 位哈希冲出窗口产生水平滚动条
        var machineLbl = new Label { ForeColor = Theme.Fc2, Font = Theme.Font(7.5f), AutoSize = false, BackColor = Color.Transparent, Location = new Point(14, y + 2), Size = new Size(Math.Max(body.ClientSize.Width - 120, 300), 18) };
        var copyBtn = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsCopy"), FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Theme.Acc, Font = Theme.Font(8f), Size = new Size(64, 22), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } };

        Action updateDeviceCode = () => {
            var isUsb = bindingCb.SelectedIndex == 1;
            var code = isUsb ? LicenseValidator.GetUsbDeviceId() : LicenseValidator.GetMachineCode();
            machineLbl.Text = (isUsb ? OpenClawManager.Properties.LanguageManager.GetString("SettingsUSBCode") : OpenClawManager.Properties.LanguageManager.GetString("SettingsMachineCode")) + code;
            copyBtn.Location = new Point(machineLbl.Right + 6, y);
        };
        bindingCb.SelectedIndexChanged += (_, _) => updateDeviceCode();
        updateDeviceCode(); // 初始赋值

        copyBtn.MouseEnter += (_, _) => copyBtn.ForeColor = Theme.QqBlue;
        copyBtn.MouseLeave += (_, _) => copyBtn.ForeColor = Theme.Acc;
        copyBtn.Click += (_, _) => {
            var code = bindingCb.SelectedIndex == 1 ? LicenseValidator.GetUsbDeviceId() : LicenseValidator.GetMachineCode();
            Clipboard.SetText(code); copyBtn.Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsCopied"); copyBtn.ForeColor = Theme.Grn;
            Task.Run(async () => { await Task.Delay(2000); copyBtn.Invoke(() => { copyBtn.Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsCopy"); copyBtn.ForeColor = Theme.Acc; }); });
        };
        body.Controls.Add(activateBtn); body.Controls.Add(bindLbl);
        body.Controls.Add(bindingCb);
        body.Controls.Add(machineLbl);
        body.Controls.Add(copyBtn);

        activateBtn.Click += (_, _) =>
        {
            var dlg = new Form
            {
                Text = LicenseManager.IsPro ? OpenClawManager.Properties.LanguageManager.GetString("SettingsUpdateLicense") : OpenClawManager.Properties.LanguageManager.GetString("SettingsEnterLicense"),
                Size = new Size(520, 240),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = Theme.BgWhite, ForeColor = Theme.Fc,
                Font = Theme.Font(9f)
            };
            dlg.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsLicensePrompt"), ForeColor = Theme.Fc, Font = Theme.Font(10f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(20, 20) });
            var codeBox = new TextBox { Location = new Point(20, 50), Size = new Size(460, 28), Font = Theme.Font(10f), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "OCM-XXXXX-XXXXX-XXXXX-..." };
            dlg.Controls.Add(codeBox);

            var hintLbl = new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsMachineCode") + LicenseValidator.GetMachineCode() + "\nU盘码: " + LicenseValidator.GetUsbDeviceId(), ForeColor = Theme.Fc2, Font = Theme.Font(8f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(20, 78) };
            dlg.Controls.Add(hintLbl);

            var btnOK = new Button { Text = LicenseManager.IsPro ? OpenClawManager.Properties.LanguageManager.GetString("SettingsUpdate") : OpenClawManager.Properties.LanguageManager.GetString("SettingsActivate"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Acc, ForeColor = Theme.FcWhite, Size = new Size(90, 34), Location = new Point(260, 130), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } };
            var btnCancel = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("Cancel"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Fc2, ForeColor = Theme.FcWhite, Size = new Size(90, 34), Location = new Point(360, 130), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } };

            btnOK.Click += (_, _) =>
            {
                btnOK.Enabled = false;
                btnOK.Text = OpenClawManager.Properties.LanguageManager.GetString("SettingsVerifying");
                var (ok, msg) = LicenseManager.Activate(codeBox.Text.Trim());
                MessageBox.Show(msg, ok ? OpenClawManager.Properties.LanguageManager.GetString("SettingsActivateSuccess") : OpenClawManager.Properties.LanguageManager.GetString("SettingsActivateFailed"), MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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

        var btnSave = Theme.Btn(OpenClawManager.Properties.LanguageManager.GetString("SaveConfig"));
        btnSave.Click += (_, _) => SaveAll();
        actions.Controls.Add(btnSave);

        var btnRestart = Theme.BtnDanger(OpenClawManager.Properties.LanguageManager.GetString("RestartGateway"));
        btnRestart.Click += (_, _) => RunCmd("openclaw gateway restart", OpenClawManager.Properties.LanguageManager.GetString("RestartGateway"));
        actions.Controls.Add(btnRestart);

        var btnStop = Theme.BtnWhite(OpenClawManager.Properties.LanguageManager.GetString("StopGateway"));
        btnStop.Click += (_, _) => RunCmd("openclaw gateway stop", OpenClawManager.Properties.LanguageManager.GetString("StopGateway"));
        actions.Controls.Add(btnStop);

        var btnDoctor = Theme.StyledButton(OpenClawManager.Properties.LanguageManager.GetString("ConfigDiagnostic"), Theme.Warn, Theme.FcWhite, 0);
        btnDoctor.Click += (_, _) => RunCmd("openclaw doctor --fix", OpenClawManager.Properties.LanguageManager.GetString("ConfigFix"));
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

    static int ReadPositiveInt(JsonNode? node)
        => int.TryParse(node?.ToString(), out var value) && value > 0 ? value : 0;

    static string NormalizeGatewayAuthMode(string? mode)
        => mode?.Trim().ToLowerInvariant() switch
        {
            "none" => "none",
            "password" or "basic" => "password",
            "trusted-proxy" => "trusted-proxy",
            _ => "token"
        };

    static bool TryParseTokenCount(string text, out int value)
    {
        value = 0;
        var raw = text.Trim().Replace("_", "").Replace(",", "");
        if (raw.Length == 0) return false;

        var multiplier = 1m;
        var suffix = char.ToUpperInvariant(raw[^1]);
        if (suffix == 'K' || suffix == 'M')
        {
            multiplier = suffix == 'K' ? 1024m : 1024m * 1024m;
            raw = raw[..^1].Trim();
        }

        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            return false;

        var tokens = parsed * multiplier;
        if (tokens > int.MaxValue) return false;
        value = (int)Math.Round(tokens, MidpointRounding.AwayFromZero);
        return value > 0;
    }

    void SaveAll()
    {
        try
        {
            var cfg = OpenClawRuntime.ReadConfig() ?? new JsonObject();
            var gw = cfg["gateway"]?.AsObject(); if (gw == null) { gw = new JsonObject(); cfg["gateway"] = gw; }
            if (int.TryParse(Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsPort")), out var p)) gw["port"] = p;
            var bindMode = Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsBindAddr"));
            gw["bind"] = bindMode;
            if (gw["auth"] is not JsonObject a) { a = new JsonObject(); gw["auth"] = a; }
            var authModeToSave = NormalizeGatewayAuthMode(Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsAuthMode")));
            if (authModeToSave == "none" && bindMode != "loopback")
                authModeToSave = "token";
            a["mode"] = authModeToSave;
            string tk = Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsAuthToken")); if (!string.IsNullOrEmpty(tk)) a["token"] = tk;
            if (authModeToSave == "token" && string.IsNullOrWhiteSpace(a["token"]?.ToString()))
                a["token"] = Guid.NewGuid().ToString("N");
            // 日志级别 → 顶层 logging 配置
            var topLogging = cfg["logging"]?.AsObject(); if (topLogging == null) { topLogging = new JsonObject(); cfg["logging"] = topLogging; }
            topLogging["level"] = Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsLogLevel"));

            // Agents defaults
            var agents = cfg["agents"]?.AsObject(); if (agents == null) { agents = new JsonObject(); cfg["agents"] = agents; }
            var defs = agents["defaults"]?.AsObject(); if (defs == null) { defs = new JsonObject(); agents["defaults"] = defs; }
            string model = Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsDefaultModel"));
            if (!string.IsNullOrEmpty(model)) { var m = defs["model"]?.AsObject(); if (m == null) { m = new JsonObject(); defs["model"] = m; } m["primary"] = model; }
            if (int.TryParse(Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsMaxOutputLen")), out var mt) && mt > 0) defs["maxTokens"] = mt;
            // 上下文窗口 → 只写运行时 cap。不要覆盖 contextWindow，它是模型原生窗口元数据。
            string cw = Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsContextWindow"));
            var previousContextCap = ReadPositiveInt(defs["contextTokens"]);
            if (!string.IsNullOrEmpty(cw) && TryParseTokenCount(cw, out var cwVal) && cwVal >= 8192)
            {
                defs["contextTokens"] = cwVal;
                var provs = cfg["models"]?["providers"]?.AsObject();
                if (provs != null)
                    foreach (var pk in provs)
                        if (pk.Value is JsonObject po && po["models"] is JsonArray arr)
                            foreach (var m in arr)
                                if (m is JsonObject mo)
                                {
                                    var modelContextTokens = ReadPositiveInt(mo["contextTokens"]);
                                    var modelContextWindow = ReadPositiveInt(mo["contextWindow"]);
                                    if (modelContextWindow > 0 && (modelContextWindow == modelContextTokens || modelContextWindow == previousContextCap || modelContextWindow == cwVal))
                                        mo.Remove("contextWindow");
                                    mo["contextTokens"] = cwVal;
                                }
            }
            if (int.TryParse(Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsHeartbeatMin")), out var hm) && hm > 0) { var hb = defs["heartbeat"]?.AsObject(); if (hb == null) { hb = new JsonObject(); defs["heartbeat"] = hb; } hb["every"] = hm + "m"; }

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
string ws = Val(OpenClawManager.Properties.LanguageManager.GetString("SettingsWorkspaceDir"));
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
            MessageBox.Show(string.Format(OpenClawManager.Properties.LanguageManager.GetString("SettingsMigrateMsg"), Directory.GetFiles(oldWs, "*", SearchOption.AllDirectories).Length, oldWs, ws), OpenClawManager.Properties.LanguageManager.GetString("SettingsMigrateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("SettingsMigrateFailed") + ex.Message, OpenClawManager.Properties.LanguageManager.GetString("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    // 写入 agents.defaults.workspace（网关实际读取的路径）
    if (defs == null) { defs = new JsonObject(); agents["defaults"] = defs; }
    defs["workspace"] = ws;
}

            File.WriteAllText(MainForm.CfgFullPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("ConfigSavedRestart"), OpenClawManager.Properties.LanguageManager.GetString("SaveSuccess"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("SaveFailed") + ": " + ex.Message, OpenClawManager.Properties.LanguageManager.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
                MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("SettingsNodeNotFound") + entry, OpenClawManager.Properties.LanguageManager.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = node,
                Arguments = $"\"{entry}\" {args}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            MessageBox.Show(name + OpenClawManager.Properties.LanguageManager.GetString("SettingsCmdExecuted"), name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(name + " " + OpenClawManager.Properties.LanguageManager.GetString("SettingsCmdFailed") + ex.Message, OpenClawManager.Properties.LanguageManager.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void RefreshMainForm()
    {
        var mf = body.FindForm() as MainForm;
        if (mf == null) return;
        mf.Text = $"OpenClaw 管理器 v{MainForm.AppVersion}{(LicenseManager.IsPro ? OpenClawManager.Properties.LanguageManager.GetString("SettingsPro") : OpenClawManager.Properties.LanguageManager.GetString("SettingsTrial") + LicenseManager.TrialDaysRemaining + "天")}";
        mf.BuildSidebar();
    }
}
