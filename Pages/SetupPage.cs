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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawManager;

public class SetupPage
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);

    static string ToShortPath(string longPath)
    {
        try
        {
            var sb = new StringBuilder(260);
            if (GetShortPathName(longPath, sb, (uint)sb.Capacity) > 0)
                return sb.ToString();
        }
        catch { }
        return longPath;
    }

    Panel body, card;
    FlowLayoutPanel stepBar;
    Label statusLbl;
    int step = 0;
    string ocCmd = "";

    static string NExe => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
    static string NCmd => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd");
    static string OcPs => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "openclaw.ps1");
    static string Cfg => MainForm.CfgFullPath;
    int S(float v) => (int)(v * Theme.ScaleFactor);

    // 国际化快捷方法
    static string LGet(string key) => OpenClawManager.Properties.LanguageManager.GetString(key);

    int port = 18789; string bind = "loopback"; string auth = "token"; string gwt = "";
    string pUrl = "https://api.deepseek.com"; string pKey = ""; string pApi = "openai-completions"; string pId = "deepseek";
    string defM = "deepseek/deepseek-v4-pro"; string ws = "";
    HashSet<string> checkedModels = new();

    public void Build(Panel pnl, bool local = true)
    {
        body = pnl; body.Controls.Clear(); ocCmd = File.Exists(OcPs) ? OcPs : ""; Ld();

        // 订阅语言切换事件，切换时重建整个 UI
        OpenClawManager.Properties.LanguageManager.LanguageChanged -= RebuildOnLangChange;
        OpenClawManager.Properties.LanguageManager.LanguageChanged += RebuildOnLangChange;

        var runtimeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime");
        if (File.Exists(Path.Combine(runtimeDir, "node.exe")))
            ocCmd = Path.Combine(runtimeDir, "node.exe");

        Panel b = new Panel { Location = new Point(0, 0), Size = new Size(body.ClientSize.Width, S(40)), BackColor = Theme.Acc };
        b.Controls.Add(new Label { Text = "  \u2691 " + LGet("SetupTitle"), ForeColor = Theme.FcWhite, Font = Theme.Font(12f, FontStyle.Bold), Location = new Point(S(10), S(6)), AutoSize = true, BackColor = Color.Transparent });
        body.Controls.Add(b);

        stepBar = new FlowLayoutPanel { Location = new Point(S(18), S(50)), Size = new Size(body.ClientSize.Width - S(36), S(28)), BackColor = Color.Transparent };
        var ls = new[] { LGet("SetupSubtitleSystem"), LGet("SetupSubtitleRegister") };
        for (int i = 0; i < ls.Length; i++) { Panel dp = new Panel { Size = new Size(S(150), S(26)), BackColor = Color.Transparent }; dp.Controls.Add(new Label { Text = i == 0 ? "\u25CF" : "\u25CB", ForeColor = i == 0 ? Theme.Acc : Theme.Fc2, Font = Theme.Font(9f), Size = new Size(S(12), S(26)), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent }); dp.Controls.Add(new Label { Text = (i + 1) + ". " + ls[i], ForeColor = i == 0 ? Theme.Fc : Theme.Fc2, Font = Theme.Font(8f, i == 0 ? FontStyle.Bold : FontStyle.Regular), Location = new Point(S(12), 0), Size = new Size(S(138), S(26)), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent }); stepBar.Controls.Add(dp); }
        body.Controls.Add(stepBar);

        card = new Panel { Location = new Point(S(18), S(84)), Size = new Size(body.ClientSize.Width - S(40), body.ClientSize.Height - S(124)), BackColor = Theme.BgWhite, AutoScroll = true };
        card.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(Theme.BdrLight), 0, 0, card.Width - 1, card.Height - 1);
        body.Controls.Add(card);

        statusLbl = new Label { Location = new Point(S(18), body.ClientSize.Height - S(28)), Size = new Size(body.ClientSize.Width - S(40), S(16)), Text = "", ForeColor = Theme.Fc2, Font = Theme.Font(8f), BackColor = Color.Transparent };
        body.Controls.Add(statusLbl);

        Show(0);
    }

    void RebuildOnLangChange()
    {
        // 语言切换时重建 UI，保留当前步骤
        int savedStep = step;
        // 先保存当前配置状态
        if (body != null) { body.Controls.Clear(); step = savedStep; Build(body, false); }
    }

    void Ld() { try { if (!File.Exists(Cfg)) return; var n = JsonNode.Parse(File.ReadAllText(Cfg, Encoding.UTF8)); if (n == null) return; var g = n["gateway"]; if (g != null) { port = (int?)g["port"] ?? 18789; bind = g["bind"]?.ToString() ?? "loopback"; auth = g["auth"]?["mode"]?.ToString() ?? "token"; gwt = g["auth"]?["token"]?.ToString() ?? ""; } defM = n["agents"]?["defaults"]?["model"]?["primary"]?.ToString() ?? defM; ws = n["agents"]?["defaults"]?["workspace"]?.ToString() ?? ws; } catch { } }

    void Rf() { for (int i = 0; i < stepBar.Controls.Count; i++) if (stepBar.Controls[i] is Panel p && p.Controls.Count >= 2 && p.Controls[0] is Label d1 && p.Controls[1] is Label l1) { d1.Text = step >= i ? "\u25CF" : "\u25CB"; d1.ForeColor = step >= i ? Theme.Acc : Theme.Fc2; l1.ForeColor = step == i ? Theme.Fc : Theme.Fc2; l1.Font = Theme.Font(8f, step == i ? FontStyle.Bold : FontStyle.Regular); } }

    void Show(int s) { step = s; Rf(); card.Controls.Clear(); if (s == 0) S1(); else S2(); }
    void St(string t, Color? c = null) { statusLbl.Text = t; if (c != null) statusLbl.ForeColor = c.Value; }

    Button Bb(string t, Color c) => new Button { Text = t, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Theme.FcWhite, Font = Theme.Font(9f, FontStyle.Bold), AutoSize = true, FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand, Padding = new Padding(S(14), S(4), S(14), S(4)) };
    Button Bw(string t) => new Button { Text = t, FlatStyle = FlatStyle.Flat, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, Font = Theme.Font(9f), AutoSize = true, FlatAppearance = { BorderColor = Theme.Bdr, BorderSize = 1 }, Cursor = Cursors.Hand, Padding = new Padding(S(10), S(4), S(10), S(4)) };

    void Nav(bool canNext = true) { if (step < stepBar.Controls.Count - 1) { var b = Bb(LGet("SetupNext") + " →", canNext ? Theme.Acc : Theme.Fc2); b.Location = new Point(card.ClientSize.Width - S(180), card.ClientSize.Height - S(48)); b.Enabled = canNext; if (canNext) b.Click += (_, _) => Show(step + 1); card.Controls.Add(b); } if (step > 0) Prev(); }
    void Prev() { var b = Bw("← " + LGet("SetupPrev")); b.Location = new Point(S(22), card.ClientSize.Height - S(48)); b.Click += (_, _) => Show(step - 1); card.Controls.Add(b); }

    void S0() { Show(0); }

    // ============= S1 =============
    void S1() {
        int y = S(14); Title("1. " + LGet("SetupSubtitleSystem"), ref y);
        int W = card.ClientSize.Width - S(44);

        // ---- 网关设置 ----
        Panel gc = Card(LGet("SetupGatewaySettings"), W, ref y, S(130));
        Cl(LGet("SetupBind") + ":", S(14), S(26), gc, 9f); var bc = new ComboBox { Location = new Point(S(69), S(24)), Size = new Size(S(140), S(24)), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, FlatStyle = FlatStyle.Flat, Font = Theme.Font(9f) }; bc.Items.AddRange(new[] { "loopback", "lan", "public" }); bc.SelectedIndex = bind == "lan" ? 1 : bind == "public" ? 2 : 0; gc.Controls.Add(bc);
        Cl(LGet("SetupPort") + ":", S(220), S(26), gc, 9f); var pb = new TextBox { Location = new Point(S(260), S(24)), Size = new Size(S(50), S(24)), Text = port.ToString(), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; gc.Controls.Add(pb);
        Cl(LGet("SetupAuth") + ":", S(340), S(26), gc, 9f); var ac = new ComboBox { Location = new Point(S(385), S(24)), Size = new Size(S(140), S(24)), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, FlatStyle = FlatStyle.Flat, Font = Theme.Font(9f) }; ac.Items.AddRange(new[] { "token", "none", "password" }); ac.SelectedIndex = auth == "none" ? 1 : auth == "password" ? 2 : 0; gc.Controls.Add(ac);

        Panel ar = new Panel { Location = new Point(S(14), S(56)), Size = new Size(W - S(28), S(26)), BackColor = Color.Transparent }; gc.Controls.Add(ar);
        TextBox tb = null, pw = null;
        Action<int> ra = (s) => { ar.Controls.Clear(); if (s == 0) { ar.Controls.Add(L("Token:", 9f)); tb = new TextBox { Location = new Point(S(55), 0), Size = new Size(S(360), S(24)), Text = gwt, PlaceholderText = LGet("SetupTokenPlaceholder"), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; ar.Controls.Add(tb); } else if (s == 2) { ar.Controls.Add(L(LGet("SetupPassword") + ":", 9f)); pw = new TextBox { Location = new Point(S(55), 0), Size = new Size(S(360), S(24)), Text = gwt, UseSystemPasswordChar = true, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; ar.Controls.Add(pw); } };
        ac.SelectedIndexChanged += (_, _) => ra(ac.SelectedIndex); ra(ac.SelectedIndex);

        if (string.IsNullOrEmpty(ws)) ws = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
        Cl(LGet("SetupWorkspace") + ":", S(14), S(90), gc, 9f); var wb = new TextBox { Location = new Point(S(69), S(88)), Size = new Size(S(360), S(24)), Text = ws, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; gc.Controls.Add(wb);
        var wbtn = new Button { Text = "...", Size = new Size(S(28), S(24)), Location = new Point(S(433), S(88)), FlatStyle = FlatStyle.Flat, BackColor = Theme.BgWhite, Cursor = Cursors.Hand }; wbtn.Click += (_, _) => { using var dlg = new FolderBrowserDialog { SelectedPath = wb.Text }; if (dlg.ShowDialog() == DialogResult.OK) wb.Text = dlg.SelectedPath; }; gc.Controls.Add(wbtn);
        y = gc.Bottom + S(10);

        // ---- API 供应商 ----
        Panel pc = Card(LGet("SetupConfigApi"), W, ref y, S(120));
        Cl(LGet("SetupProvider") + ":", S(14), S(28), pc, 9f); var pv = new ComboBox { Location = new Point(S(70), S(26)), Size = new Size(S(140), S(24)), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, FlatStyle = FlatStyle.Flat, Font = Theme.Font(9f) }; pv.Items.AddRange(new[] { "DeepSeek", "OpenAI", "Anthropic", "Google Gemini", "xAI Grok", "Mistral", "Groq", "OpenRouter", "Together AI", "阿里百炼", "百度千帆", "通义千问", "智谱 GLM", "月之暗面", "豆包", "Ollama 本地", "LM Studio 本地", LGet("SetupCustom") }); pv.SelectedIndex = 0; pc.Controls.Add(pv);
        var regBtn = new Button { Text = LGet("SetupApiRegister"), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, 152, 0), ForeColor = Theme.FcWhite, Font = Theme.Font(8f, FontStyle.Bold), Size = new Size(S(64), S(24)), Location = new Point(S(224), S(26)), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, Visible = true };
        regBtn.Click += (_, _) => { var u = regBtn.Tag?.ToString(); if (!string.IsNullOrEmpty(u)) try { Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); } catch { } };
        pc.Controls.Add(regBtn);
        var pnl = new Label { Text = LGet("SetupProviderName") + ":", ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, Location = new Point(S(218), S(30)), Visible = false, BackColor = Color.Transparent }; pc.Controls.Add(pnl);
        var pn = new TextBox { Location = new Point(S(255), S(26)), Size = new Size(S(140), S(24)), Text = pId, PlaceholderText = LGet("SetupProviderNameHint"), Visible = false, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; pc.Controls.Add(pn);
        Cl("URL:", S(14), S(60), pc, 9f); var pu = new TextBox { Location = new Point(S(55), S(58)), Size = new Size(W - S(73), S(24)), Text = pUrl, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; pc.Controls.Add(pu);
        Cl(LGet("SetupApiType") + ":", S(14), S(92), pc, 9f); var pi = new ComboBox { Location = new Point(S(70), S(90)), Size = new Size(S(170), S(24)), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, FlatStyle = FlatStyle.Flat, Font = Theme.Font(9f) }; pi.Items.AddRange(new[] { "openai-completions", "anthropic-messages", "google-generative", "ollama" }); pi.SelectedIndex = 0; pc.Controls.Add(pi);
        Cl(LGet("SetupApiKey") + ":", S(260), S(92), pc, 9f); var pk = new TextBox { Location = new Point(S(315), S(90)), Size = new Size(W - S(333), S(24)), Text = pKey, UseSystemPasswordChar = !string.IsNullOrEmpty(pKey), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; pc.Controls.Add(pk);
        pv.SelectedIndexChanged += (_, _) => { var s = pv.Text; bool custom = s == LGet("SetupCustom"); pnl.Visible = custom; pn.Visible = custom;
            pId = custom ? (string.IsNullOrWhiteSpace(pn.Text) ? "custom" : pn.Text.Trim()) : s switch { "DeepSeek" => "deepseek", "OpenAI" => "openai", "Anthropic" => "anthropic", "Google Gemini" => "google", "xAI Grok" => "xai", "Mistral" => "mistral", "Groq" => "groq", "OpenRouter" => "openrouter", "Together AI" => "togetherai", "阿里百炼" => "alibaba", "百度千帆" => "qianfan", "通义千问" => "qwen", "智谱 GLM" => "glm", "月之暗面" => "moonshot", "豆包" => "volcengine", "Ollama 本地" => "ollama", "LM Studio 本地" => "lmstudio", _ => "custom" };
            string regUrl = s switch { "DeepSeek" => "https://platform.deepseek.com/api_keys", "OpenAI" => "https://platform.openai.com/api-keys", "Anthropic" => "https://console.anthropic.com/settings/keys", "Google Gemini" => "https://aistudio.google.com/apikey", "xAI Grok" => "https://console.x.ai", "Mistral" => "https://console.mistral.ai/api-keys", "Groq" => "https://console.groq.com/keys", "OpenRouter" => "https://openrouter.ai/keys", "Together AI" => "https://api.together.xyz/settings/api-keys", "阿里百炼" => "https://bailian.console.aliyun.com/", "百度千帆" => "https://console.bce.baidu.com/qianfan/ais/console/applicationConsole/application", "通义千问" => "https://dashscope.console.aliyun.com/apiKey", "智谱 GLM" => "https://open.bigmodel.cn/usercenter/apikeys", "月之暗面" => "https://platform.moonshot.cn/console/api-keys", "豆包" => "https://console.volcengine.com/ark/region:ark+cn-beijing/apiKey", "Ollama 本地" => "https://ollama.com/", "LM Studio 本地" => "https://lmstudio.ai/", _ => "" };
            regBtn.Visible = !string.IsNullOrEmpty(regUrl); regBtn.Tag = regUrl;
            if (custom) { pu.Text = ""; pn.Text = ""; pi.SelectedIndex = 0; }
            else if (s == "DeepSeek") { pu.Text = "https://api.deepseek.com"; pi.SelectedIndex = 0; }
            else if (s == "OpenAI") { pu.Text = "https://api.openai.com/v1"; pi.SelectedIndex = 0; }
            else if (s == "Anthropic") { pu.Text = "https://api.anthropic.com/v1"; pi.SelectedIndex = 1; }
            else if (s == "Google Gemini") { pu.Text = "https://generativelanguage.googleapis.com/v1beta"; pi.SelectedIndex = 2; }
            else if (s == "xAI Grok") { pu.Text = "https://api.x.ai/v1"; pi.SelectedIndex = 0; }
            else if (s == "Mistral") { pu.Text = "https://api.mistral.ai/v1"; pi.SelectedIndex = 0; }
            else if (s == "Groq") { pu.Text = "https://api.groq.com/openai/v1"; pi.SelectedIndex = 0; }
            else if (s == "OpenRouter") { pu.Text = "https://openrouter.ai/api/v1"; pi.SelectedIndex = 0; }
            else if (s == "Together AI") { pu.Text = "https://api.together.xyz/v1"; pi.SelectedIndex = 0; }
            else if (s == "阿里百炼") { pu.Text = "https://dashscope.aliyuncs.com/compatible-mode/v1"; pi.SelectedIndex = 0; }
            else if (s == "百度千帆") { pu.Text = "https://qianfan.baidubce.com/v2"; pi.SelectedIndex = 0; }
            else if (s == "通义千问") { pu.Text = "https://dashscope.aliyuncs.com/compatible-mode/v1"; pi.SelectedIndex = 0; }
            else if (s == "智谱 GLM") { pu.Text = "https://open.bigmodel.cn/api/paas/v4"; pi.SelectedIndex = 0; }
            else if (s == "月之暗面") { pu.Text = "https://api.moonshot.cn/v1"; pi.SelectedIndex = 0; }
            else if (s == "豆包") { pu.Text = "https://ark.cn-beijing.volces.com/api/v3"; pi.SelectedIndex = 0; }
            else if (s == "Ollama 本地") { pu.Text = "http://localhost:11434/v1"; pi.SelectedIndex = 3; }
            else if (s == "LM Studio 本地") { pu.Text = "http://localhost:1234/v1"; pi.SelectedIndex = 0; }
        };
        regBtn.Tag = "https://platform.deepseek.com/api_keys";

        // ---- 模型管理 ----
        Panel mc = Card(LGet("SetupModelMgmt"), W, ref y, S(160));
        Cl(LGet("SetupManualFetch") + ":", S(14), S(28), mc, 9f); var mb = new TextBox { Location = new Point(S(130), S(26)), Size = new Size(S(200), S(24)), PlaceholderText = LGet("SetupInputModelHint"), BackColor = Theme.BgWhite, ForeColor = Theme.Fc, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(9f) }; mc.Controls.Add(mb);
        var ab = new Button { Text = "+ " + LGet("SetupAdd"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Acc, ForeColor = Theme.FcWhite, Font = Theme.Font(9f), Size = new Size(S(60), S(24)), Location = new Point(S(338), S(26)), FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand }; mc.Controls.Add(ab);
        var fb = new Button { Text = LGet("SetupFetchModels"), FlatStyle = FlatStyle.Flat, BackColor = Theme.Grn, ForeColor = Theme.FcWhite, Font = Theme.Font(9f), Size = new Size(S(80), S(24)), Location = new Point(S(406), S(26)), FlatAppearance = { BorderSize = 0 }, Cursor = Cursors.Hand }; mc.Controls.Add(fb);
        var fs = new Label { Location = new Point(S(492), S(28)), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.Fc2, Font = Theme.Font(8f) }; mc.Controls.Add(fs);

        CheckedListBox clb = new CheckedListBox { Location = new Point(S(14), S(56)), Size = new Size(W - S(28), S(66)), BackColor = Color.FromArgb(252, 252, 254), ForeColor = Theme.Fc, Font = Theme.Font(9f), CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle };
        mc.Controls.Add(clb);

        var dc = new ComboBox { Location = new Point(S(85), S(126)), Size = new Size(S(320), S(24)), DropDownStyle = ComboBoxStyle.DropDown, BackColor = Theme.BgWhite, ForeColor = Theme.Fc, FlatStyle = FlatStyle.Flat, Font = Theme.Font(9f) }; mc.Controls.Add(dc);
        mc.Controls.Add(new Label { Text = LGet("SetupDefaultModel") + ":", ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, Location = new Point(S(14), S(130)), BackColor = Color.Transparent });
        y = mc.Bottom + S(10);

        string FullModelId(string name) => name.Contains('/') ? name : pId + "/" + name;

        Action validateS1 = null;
        ab.Click += (_, _) => { if (!string.IsNullOrEmpty(mb.Text)) { clb.Items.Add(mb.Text, true); dc.Items.Add(mb.Text); mb.Clear(); validateS1?.Invoke(); } };
        mb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ab.PerformClick(); e.SuppressKeyPress = true; } };
        
        dc.SelectedIndexChanged += (_, _) => { if (dc.SelectedIndex >= 0) defM = FullModelId(dc.SelectedItem?.ToString() ?? defM); };
        fb.Click += (_, _) => { if (string.IsNullOrEmpty(pk.Text) && !pv.Text.StartsWith("Ollama")) { fs.Text = LGet("SetupNeedKey"); fs.ForeColor = Theme.Red; return; } fs.Text = LGet("SetupFetching"); fs.ForeColor = Theme.Acc; Task.Run(() => { try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }; if (!string.IsNullOrEmpty(pk.Text)) h.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pk.Text); var r = h.GetStringAsync(pu.Text.Trim().TrimEnd('/') + "/models").Result; var d = JsonDocument.Parse(r); var ids = new List<string>(); if (d.RootElement.TryGetProperty("data", out var el)) foreach (var m in el.EnumerateArray()) if (m.TryGetProperty("id", out var did)) ids.Add(did.GetString() ?? ""); 
                            for (int i = 0; i < clb.Items.Count; i++) { var item = clb.Items[i].ToString(); if (clb.GetItemChecked(i)) checkedModels.Add(item); else checkedModels.Remove(item); }
                            card.Invoke(() => { clb.Items.Clear(); dc.Items.Clear(); foreach (var id in ids) { bool wasChecked = checkedModels.Contains(id); clb.Items.Add(id, wasChecked); dc.Items.Add(id); } dc.Text = defM; fs.Text = "\u2713 " + ids.Count + " " + LGet("SetupModelMgmt"); fs.ForeColor = Theme.Grn; validateS1?.Invoke(); }); } catch (Exception ex) { card.Invoke(() => { fs.Text = ex.Message.Length > 30 ? ex.Message[..30] : ex.Message; fs.ForeColor = Theme.Red; }); } }); };

        var nb = Bb(LGet("SetupNextSave"), Theme.Fc2); nb.Location = new Point(card.ClientSize.Width - S(220), card.ClientSize.Height - S(48)); nb.Enabled = false;
        validateS1 = () => {
            bool isLocal = pv.Text.StartsWith("Ollama") || pv.Text.StartsWith("LM Studio");
            bool ok = int.TryParse(pb.Text.Trim(), out int vp) && vp >= 1 && vp <= 65535
                && !string.IsNullOrWhiteSpace(pu.Text)
                && (isLocal || !string.IsNullOrWhiteSpace(pk.Text))
                && (pv.Text != LGet("SetupCustom") || !string.IsNullOrWhiteSpace(pn.Text))
                && (clb.CheckedItems.Count > 0 || dc.SelectedIndex >= 0);
            nb.Enabled = ok; nb.BackColor = ok ? Theme.Acc : Theme.Fc2;
        };
        pb.TextChanged += (_, _) => validateS1();
        pu.TextChanged += (_, _) => validateS1();
        pk.TextChanged += (_, _) => validateS1();
        pn.TextChanged += (_, _) => { if (pv.Text == LGet("SetupCustom")) pId = string.IsNullOrWhiteSpace(pn.Text) ? "custom" : pn.Text.Trim(); validateS1(); };
        pv.SelectedIndexChanged += (_, _) => validateS1();
        clb.ItemCheck += (_, e) => card.BeginInvoke(new Action(validateS1));
        dc.SelectedIndexChanged += (_, _) => validateS1();
        validateS1();
        nb.Click += (_, _) => {
            port = int.TryParse(pb.Text, out int pp) ? pp : 18789;
            bind = bc.SelectedIndex == 1 ? "lan" : bc.SelectedIndex == 2 ? "public" : "loopback";
            auth = ac.SelectedIndex == 1 ? "none" : ac.SelectedIndex == 2 ? "password" : "token";
            if (ac.SelectedIndex == 0 && tb != null) gwt = tb.Text.Trim(); else if (ac.SelectedIndex == 2 && pw != null) gwt = pw.Text.Trim();
            if (ac.SelectedIndex == 0 && string.IsNullOrEmpty(gwt)) gwt = Guid.NewGuid().ToString("N")[..32];
            defM = FullModelId(dc.SelectedItem?.ToString() ?? defM); pUrl = pu.Text.Trim(); pKey = pk.Text.Trim();
            pApi = pi.SelectedIndex switch { 1 => "anthropic-messages", 2 => "google-generative", 3 => "ollama", _ => "openai-completions" };
            pId = pv.Text == LGet("SetupCustom") ? pn.Text.Trim() : pv.Text switch { "DeepSeek" => "deepseek", "OpenAI" => "openai", "Anthropic" => "anthropic", "Google Gemini" => "google", "xAI Grok" => "xai", "Mistral" => "mistral", "Groq" => "groq", "OpenRouter" => "openrouter", "Together AI" => "togetherai", "阿里百炼" => "alibaba", "百度千帆" => "qianfan", "通义千问" => "qwen", "智谱 GLM" => "glm", "月之暗面" => "moonshot", "豆包" => "volcengine", "Ollama 本地" => "ollama", "LM Studio 本地" => "lmstudio", _ => "custom" };
            ws = wb.Text.Trim();
            var mo = new JsonObject();
            var modelArray = new JsonArray();
            for (int i = 0; i < clb.Items.Count; i++) if (clb.GetItemChecked(i)) { var item = clb.Items[i].ToString(); var modelKey = FullModelId(item); mo[modelKey] = new JsonObject { ["alias"] = item }; modelArray.Add(new JsonObject { ["id"] = item, ["name"] = item }); }
            if (mo.Count == 0) { mo[defM] = new JsonObject { ["alias"] = dc.SelectedItem?.ToString() ?? defM }; modelArray.Add(new JsonObject { ["id"] = dc.SelectedItem?.ToString() ?? defM, ["name"] = dc.SelectedItem?.ToString() ?? defM }); }
            var cfg = new JsonObject { ["gateway"] = new JsonObject { ["mode"] = "local", ["port"] = port, ["bind"] = bind, ["auth"] = new JsonObject { ["mode"] = auth }, ["http"] = new JsonObject { ["endpoints"] = new JsonObject { ["chatCompletions"] = new JsonObject { ["enabled"] = true } } } }, ["agents"] = new JsonObject { ["defaults"] = new JsonObject { ["model"] = new JsonObject { ["primary"] = defM }, ["models"] = mo, ["workspace"] = ws }, ["list"] = new JsonArray { new JsonObject { ["id"] = "main", ["workspace"] = ws } } }, ["models"] = new JsonObject { ["providers"] = new JsonObject { [pId] = new JsonObject { ["baseUrl"] = pUrl, ["api"] = pApi, ["models"] = modelArray } } }, ["plugins"] = new JsonObject { ["entries"] = new JsonObject() } };
            if (!string.IsNullOrEmpty(gwt)) cfg["gateway"]!["auth"]!["token"] = gwt;
            if (!string.IsNullOrEmpty(pKey)) cfg["models"]!["providers"]![pId]!["apiKey"] = pKey;
            try {
                var dir = Path.GetDirectoryName(Cfg);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!string.IsNullOrEmpty(ws) && !Directory.Exists(ws)) Directory.CreateDirectory(ws);
                try { Directory.CreateDirectory(Path.Combine(ws, "memory")); } catch { }
                try {
                    var wsOc = Path.Combine(ws, ".openclaw");
                    if (!Directory.Exists(wsOc)) Directory.CreateDirectory(wsOc);
                    var statePath = Path.Combine(wsOc, "workspace-state.json");
                    if (!File.Exists(statePath))
                        File.WriteAllText(statePath, "", Encoding.UTF8);
                } catch { }
                File.WriteAllText(Cfg, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                try { Directory.CreateDirectory(Path.Combine(dir!, "agents", "main", "sessions")); } catch { }
                St("\u2713 " + LGet("SetupSaveSuccess"), Theme.Grn); Show(2);
            } catch (UnauthorizedAccessException) {
                St(LGet("SetupPermissionDenied"), Theme.Red);
            } catch (Exception ex) {
                St(LGet("SetupFailed") + ": " + ex.Message, Theme.Red);
            }
        };
        card.Controls.Add(nb); if (step > 0) Prev();
    }

    // ============= S2 =============
    static bool StartGatewayForSetup()
    {
        try
        {
            string ws = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
            try { if (!Directory.Exists(ws)) Directory.CreateDirectory(ws); } catch { }
            try { if (!Directory.Exists(Path.Combine(ws, "memory"))) Directory.CreateDirectory(Path.Combine(ws, "memory")); } catch { }
            try {
                var wsOc = Path.Combine(ws, ".openclaw");
                if (!Directory.Exists(wsOc)) Directory.CreateDirectory(wsOc);
                var statePath = Path.Combine(wsOc, "workspace-state.json");
                if (!File.Exists(statePath))
                    File.WriteAllText(statePath, "", Encoding.UTF8);
            } catch { }
            string cfgPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "openclaw.json");
            if (File.Exists(cfgPath))
            {
                try
                {
                    var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(cfgPath, Encoding.UTF8));
                    if (node != null)
                    {
                        var defs = node["agents"]?["defaults"];
                        if (defs != null) defs["workspace"] = ws;
                        var list = node["agents"]?["list"]?.AsArray();
                        if (list != null) foreach (var a in list) a!["workspace"] = ws;
                        File.WriteAllText(cfgPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                    }
                }
                catch { }
            }

            string nodeExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node.exe");
            if (!File.Exists(nodeExe)) return false;
            string entry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "index.js");
            if (!File.Exists(entry)) entry = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "openclaw.mjs");
            if (!File.Exists(entry)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = nodeExe,
                Arguments = $"\"{entry}\" gateway run --port 18789",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            string tempDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "temp");
            psi.EnvironmentVariables["OPENCLAW_HOME"] = AppDomain.CurrentDomain.BaseDirectory;
            psi.EnvironmentVariables["TMPDIR"] = tempDir;
            psi.EnvironmentVariables["TEMP"] = tempDir;
            psi.EnvironmentVariables["TMP"] = tempDir;
            var p = Process.Start(psi);
            if (p == null) return false;
            _ = Task.Run(() => { try { while (!p.HasExited) { var line = p.StandardOutput.ReadLine(); if (line != null) DashboardPage.LogToConsole(line); } } catch { } });
            _ = Task.Run(() => { try { while (!p.HasExited) { var line = p.StandardError.ReadLine(); if (line != null) DashboardPage.LogToConsole($"[ERR] {line}"); } } catch { } });
            return true;
        }
        catch { return false; }
    }

    void S2() {
        int y = S(14); Title("2. " + LGet("SetupSubtitleRegister"), ref y);
        Inf(LGet("SetupLocalNote"), Theme.Fc2, ref y); y += S(8);
        var ul = new Label { Location = new Point(S(22), y), AutoSize = true, ForeColor = Theme.Fc2, Font = Theme.Font(9f), BackColor = Color.Transparent }; card.Controls.Add(ul); y += S(22);
        var ba = new ProgressBar { Location = new Point(S(22), y), Size = new Size(S(420), S(14)), Style = ProgressBarStyle.Marquee, Visible = false }; card.Controls.Add(ba); y += S(22);
        var bn = Bb("\u25B6 " + LGet("SetupStartGateway"), Theme.Grn); bn.Location = new Point(S(22), y);
        bn.Click += (_, _) => { bn.Enabled = false; ul.Text = LGet("SetupStartingGateway"); ba.Visible = true;
            DashboardPage.LogToConsole(LGet("GatewayStarted"));
            Task.Run(async () => {
                bool started = StartGatewayForSetup();
                if (started) DashboardPage.LogToConsole(LGet("SetupStartingGateway"));
                else DashboardPage.LogToConsole(LGet("LaunchFailed"));
                ul.Invoke(() => ul.Text = LGet("SetupWaitForGateway"));
                bool ok = false; string tok = "";
                for (int i = 0; i < 15; i++) { await Task.Delay(2000); try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; if ((await h.GetAsync("http://127.0.0.1:18789/")).IsSuccessStatusCode) { ok = true; break; } } catch { } }
                try { tok = JsonNode.Parse(File.ReadAllText(Cfg, Encoding.UTF8))?["gateway"]?["auth"]?["token"]?.ToString() ?? ""; } catch { }
                string wu = string.IsNullOrEmpty(tok) ? "http://127.0.0.1:18789/" : "http://127.0.0.1:18789/?token=" + tok;
                ba.Invoke(() => ba.Visible = false);
                if (ok) { ul.Invoke(() => { ul.Text = "\u2713 " + LGet("SetupRunning") + " " + wu; ul.ForeColor = Theme.Grn; }); St("\u2713 " + LGet("SetupGatewayRunning"), Theme.Grn); DashboardPage.LogToConsole(LGet("GatewayReady")); _ = DashboardPage.ApproveAllDevices(); bn.Invoke(() => { bn.Text = "\u25B6 " + LGet("SetupRestart"); bn.Enabled = true; }); card.Invoke(() => Done(wu)); }
                else { ul.Invoke(() => { ul.Text = "\u2717 " + LGet("SetupStartTimeout"); ul.ForeColor = Theme.QqOrange; }); DashboardPage.LogToConsole(LGet("LaunchTimeout")); bn.Invoke(() => bn.Enabled = true); }
            });
        };
        card.Controls.Add(bn);
        var ck = Bw(LGet("SetupCheckStatus")); ck.Location = new Point(S(22) + bn.PreferredSize.Width + S(10), y);
        ck.Click += async (_, _) => { ul.Text = LGet("SetupChecking"); ul.ForeColor = Theme.Acc; bool ok = false; string tok = ""; try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; ok = (await h.GetAsync("http://127.0.0.1:18789/")).IsSuccessStatusCode; } catch { } if (ok) { try { tok = JsonNode.Parse(File.ReadAllText(Cfg, Encoding.UTF8))?["gateway"]?["auth"]?["token"]?.ToString() ?? ""; } catch { } string wu = string.IsNullOrEmpty(tok) ? "http://127.0.0.1:18789/" : "http://127.0.0.1:18789/?token=" + tok; ul.Text = "\u2713 " + LGet("SetupRunning") + " " + wu; ul.ForeColor = Theme.Grn; Done(wu); } else { ul.Text = "\u2717 " + LGet("SetupNotRunning"); ul.ForeColor = Theme.Red; } };
        card.Controls.Add(ck); y += S(36); Nav();
        Task.Run(async () => { try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; if ((await h.GetAsync("http://127.0.0.1:18789/")).IsSuccessStatusCode) { string tok = ""; try { tok = JsonNode.Parse(File.ReadAllText(Cfg, Encoding.UTF8))?["gateway"]?["auth"]?["token"]?.ToString() ?? ""; } catch { } card.Invoke(() => Done(string.IsNullOrEmpty(tok) ? "http://127.0.0.1:18789/" : "http://127.0.0.1:18789/?token=" + tok)); } } catch { } });
    }

    void Done(string u) {
        card.Controls.Clear(); int y = S(80);
        card.Controls.Add(new Label { Text = "\u2713 " + LGet("SetupComplete"), ForeColor = Theme.Grn, Font = Theme.Font(18f, FontStyle.Bold), Location = new Point(S(22), y), AutoSize = true, BackColor = Color.Transparent }); y += S(50);
        card.Controls.Add(new Label { Text = u, ForeColor = Theme.Fc, Font = Theme.Font(10f), Location = new Point(S(22), y), AutoSize = true, BackColor = Color.Transparent }); y += S(40);
        var ob = Bb(LGet("SetupOpenConsole"), Theme.Acc); ob.Location = new Point(S(22), y); ob.Font = Theme.Font(10f, FontStyle.Bold); ob.Click += (_, _) => Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); card.Controls.Add(ob); y += S(50);
        var db = Bb(LGet("SetupDoneReturn"), Theme.Grn); db.Location = new Point(card.ClientSize.Width - S(220), card.ClientSize.Height - S(48)); db.Click += (_, _) => { var mf = body.FindForm() as MainForm; if (mf != null) { mf.BuildSidebar(); mf.ShowPanel("dashboard"); } else { body.Controls.Clear(); new DashboardPage().Build(body); } }; card.Controls.Add(db);
        Prev();
    }

    // ==== helpers ====
    void Title(string t, ref int y) { var l = new Label { Text = t, ForeColor = Theme.Acc, Font = Theme.Font(12f, FontStyle.Bold), Location = new Point(S(22), y), AutoSize = true, BackColor = Color.Transparent }; card.Controls.Add(l); y += S(28); }
    void Inf(string t, Color c, ref int y) { var l = new Label { Text = t, ForeColor = c, Font = Theme.Font(9f), Location = new Point(S(22), y), AutoSize = true, BackColor = Color.Transparent }; card.Controls.Add(l); y += S(24); }
    Panel Card(string t, int w, ref int y, int h) { var cardBg = Theme.IsDark ? Color.FromArgb(42, 42, 60) : Color.FromArgb(252, 252, 254); var p = new Panel { Location = new Point(S(22), y), Size = new Size(w, h), BackColor = cardBg }; p.Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(Theme.BdrLight), 0, 0, p.Width - 1, p.Height - 1); var tl = new Label { Text = "  " + t, ForeColor = Theme.Acc, Font = Theme.Font(9f, FontStyle.Bold), Location = new Point(S(6), S(2)), AutoSize = true, BackColor = cardBg }; p.Controls.Add(tl); card.Controls.Add(p); y += h + S(12); return p; }
    void Cl(string t, int x, int y, Panel p, float sz) { p.Controls.Add(new Label { Text = t, ForeColor = Theme.Fc2, Font = Theme.Font(sz), Location = new Point(x, y + S(2)), AutoSize = true, BackColor = Color.Transparent }); }
    Label L(string t, float sz) => new Label { Text = t, ForeColor = Theme.Fc2, Font = Theme.Font(sz), AutoSize = true, BackColor = Color.Transparent };
}