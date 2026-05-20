using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace OpenClawManager;

public class ChannelsPage
{
    Panel body;
    DataGridView grid;

    static readonly (string key, string name, string desc, string pluginType)[] AllChannels = {
        ("qqbot",       "QQ Bot",       "\u817E\u8BAF QQ \u673A\u5668\u4EBA",       "\u5185\u7F6E"),
        ("telegram",    "Telegram",     "Bot API (grammY)",                         "\u5185\u7F6E"),
        ("discord",     "Discord",      "Bot API + Gateway",                        "\u5185\u7F6E"),
        ("whatsapp",    "WhatsApp",     "Baileys QR \u914D\u5BF9",                  "\u5185\u7F6E"),
        ("signal",      "Signal",       "signal-cli \u9690\u79C1\u901A\u4FE1",      "\u5185\u7F6E"),
        ("slack",       "Slack",        "Bolt SDK \u5DE5\u4F5C\u533A\u5E94\u7528",  "\u5185\u7F6E"),
        ("webchat",     "WebChat",      "Gateway \u5185\u7F6E\u7F51\u9875\u804A\u5929", "\u5185\u7F6E"),
        ("msteams",     "Teams",        "Microsoft Bot Framework",                  "\u5185\u7F6E"),
        ("line",        "LINE",         "LINE Messaging API",                       "\u9700\u4E0B\u8F7D"),
        ("feishu",      "\u98DE\u4E66", "Feishu/Lark WebSocket",                    "\u5185\u7F6E"),
        ("wechat",      "\u5FAE\u4FE1(\u4F01\u4E1A)", "iLink Bot QR\u767B\u5F55",   "\u5916\u90E8\u63D2\u4EF6"),
        ("weixin",      "\u5FAE\u4FE1(\u4E2A\u4EBA)", "\u4E2A\u4EBA\u5FAE\u4FE1 QR\u626B\u7801", "\u5916\u90E8\u63D2\u4EF6"),
        ("matrix",      "Matrix",       "Matrix \u534F\u8BAE",                      "\u9700\u4E0B\u8F7D"),
        ("irc",         "IRC",          "\u7ECF\u5178 IRC \u670D\u52A1\u5668",       "\u5185\u7F6E"),
        ("nostr",       "Nostr",        "\u53BB\u4E2D\u5FC3\u5316 DM",             "\u5185\u7F6E"),
        ("bluebubbles", "BlueBubbles",  "iMessage macOS \u670D\u52A1",              "\u5185\u7F6E"),
        ("twitch",      "Twitch",       "Twitch \u804A\u5929\u5BA4",               "\u5185\u7F6E"),
        ("googlechat",  "Google Chat",  "Google Chat API",                          "\u9700\u4E0B\u8F7D"),
        ("nextcloud",   "Nextcloud",    "\u81EA\u6258\u7BA1 Talk",                  "\u5185\u7F6E"),
        ("zalo",        "Zalo",         "\u8D8A\u5357\u6D41\u884C\u901A\u4FE1",      "\u5185\u7F6E"),
        ("mattermost",  "Mattermost",   "Bot API + WebSocket",                      "\u9700\u4E0B\u8F7D"),
    };

    static readonly Dictionary<string, (string label, string field, bool isPassword)[]> ChannelFields = new()
    {
        ["qqbot"] = new[] { ("App ID", "appId", false), ("Client Secret", "clientSecret", true) },
        ["telegram"] = new[] { ("Bot Token", "botToken", true) },
        ["discord"] = new[] { ("Bot Token", "botToken", true), ("Application ID", "applicationId", false) },
        ["whatsapp"] = new[] { ("Phone Number", "phoneNumber", false) },
        ["signal"] = new[] { ("Phone Number", "phoneNumber", false) },
        ["slack"] = new[] { ("Bot Token", "botToken", true), ("Signing Secret", "signingSecret", true) },
        ["msteams"] = new[] { ("App ID", "appId", false), ("App Password", "appPassword", true) },
        ["line"] = new[] { ("Channel Secret", "channelSecret", true), ("Channel Token", "channelToken", true) },
        ["feishu"] = new[] { ("App ID", "appId", false), ("App Secret", "appSecret", true) },
        ["irc"] = new[] { ("Server", "server", false), ("Nickname", "nick", false), ("Password", "password", true) },
        ["matrix"] = new[] { ("Homeserver URL", "homeserverUrl", false), ("Access Token", "accessToken", true) },
        ["bluebubbles"] = new[] { ("Server URL", "serverUrl", false), ("Password", "password", true) },
        ["twitch"] = new[] { ("Nickname", "nickname", false), ("OAuth Token", "oauthToken", true) },
        ["googlechat"] = new[] { ("Service Account JSON Path", "serviceAccount", false) },
        ["zalo"] = new[] { ("App ID", "appId", false), ("Secret Key", "secretKey", true) },
        ["mattermost"] = new[] { ("Server URL", "serverUrl", false), ("Access Token", "accessToken", true) },
    };

    public void Build(Panel p)
    {
        body = p;
        body.Controls.Clear();

        if (!LicenseManager.CheckPro())
        {
            body.Controls.Add(new Label { Text = "🔒 频道管理是专业版功能", ForeColor = Theme.Fc2, Font = Theme.Font(12f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 20) });
            body.Controls.Add(new Label { Text = "请前往 系统设置 → 输入注册码 激活专业版", ForeColor = Theme.Fc2, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 50) });
            return;
        }

        body.Controls.Add(new Label { Text = "\u9891\u9053\u7BA1\u7406", ForeColor = Theme.Fc, Font = Theme.Font(13f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 12) });
        body.Controls.Add(new Label { Text = "\u70B9\u51FB\u914D\u7F6E\u8BBE\u7F6E\u5404\u9891\u9053\u5BF9\u63A5\u53C2\u6570\uFF0C\u4FEE\u6539\u540E\u8BF7\u91CD\u542F Gateway \u751F\u6548", ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 36) });

        grid = Theme.Grid();
        grid.Location = new Point(12, 64);
        grid.Size = new Size(body.ClientSize.Width - 24, body.ClientSize.Height - 80);
        grid.Columns.Add("name", "\u9891\u9053"); grid.Columns["name"].FillWeight = 16;
        grid.Columns.Add("desc", "\u8BF4\u660E"); grid.Columns["desc"].FillWeight = 28;
        grid.Columns.Add("plugin", "\u63D2\u4EF6\u72B6\u6001"); grid.Columns["plugin"].FillWeight = 12;
        grid.Columns.Add("enabled", "\u5F00\u5173"); grid.Columns["enabled"].FillWeight = 8;
        grid.Columns.Add("config", "\u914D\u7F6E"); grid.Columns["config"].FillWeight = 12;
        grid.Columns.Add("status", "\u72B6\u6001"); grid.Columns["status"].FillWeight = 24;

        var config = OpenClawRuntime.ReadConfig();
        var chObj = config?["channels"]?.AsObject() ?? new JsonObject();

        foreach (var ch in AllChannels)
        {
            bool en = false, hasCfg = false;
            if (chObj.TryGetPropertyValue(ch.key, out var node) && node is JsonObject obj)
            {
                en = obj.TryGetPropertyValue("enabled", out var v) && v?.ToString() == "true";
                int n = obj.Count; if (obj.ContainsKey("enabled")) n--;
                hasCfg = n > 0;
            }
            if (ch.key == "webchat") { en = true; hasCfg = true; }

            string status;
            if (ch.key == "webchat") status = "\u5185\u7F6E\u901A\u9053";
            else if (!en) status = "\u672A\u542F\u7528";
            else if (hasCfg) status = "\u5DF2\u914D\u7F6E";
            else status = "\u5F85\u914D\u7F6E";

            grid.Rows.Add(ch.name, ch.desc, ch.pluginType, en ? "\u2611" : "\u2610", "\u2699 \u914D\u7F6E", status);
        }

        grid.CellContentClick += OnGridClick;
        body.Controls.Add(grid);
    }

    void OnGridClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = grid.Rows[e.RowIndex];
        string name = row.Cells[0].Value?.ToString() ?? "";
        var ch = AllChannels.FirstOrDefault(c => c.name == name);
        if (string.IsNullOrEmpty(ch.key)) return;

        if (e.ColumnIndex == 3) { ToggleChannel(ch.key); Rebuild(); }
        else if (e.ColumnIndex == 4 && ch.key != "webchat") { ConfigChannel(ch.key, ch.name, ch.pluginType); Rebuild(); }
    }

    void Rebuild() => Build(body);

    void ToggleChannel(string key)
    {
        var c = OpenClawRuntime.ReadConfig() ?? new JsonObject();
        var chObj = c["channels"]?.AsObject();
        if (chObj == null) { chObj = new JsonObject(); c["channels"] = chObj; }
        if (!chObj.TryGetPropertyValue(key, out var n) || n is not JsonObject o) { chObj[key] = new JsonObject { ["enabled"] = false }; o = chObj[key]!.AsObject(); }
        bool cur = o.TryGetPropertyValue("enabled", out var v) && v?.ToString() == "true";
        o["enabled"] = !cur;
        OpenClawRuntime.SaveConfig(c);
    }

    void ConfigChannel(string key, string name, string pluginType)
    {
        if (!ChannelFields.TryGetValue(key, out var fields))
            fields = new[] { ("Allow From (* = all)", "allowFrom", false) };

        bool isExternal = pluginType == "\u5916\u90E8\u63D2\u4EF6";
        bool isDownload = pluginType == "\u9700\u4E0B\u8F7D";
        int extra = (isExternal ? 60 : 0) + (isDownload ? 30 : 0);

        var cfg = OpenClawRuntime.ReadConfig();
        var chObj = cfg?["channels"]?.AsObject() ?? new JsonObject();
        var curObj = chObj.TryGetPropertyValue(key, out var cn) && cn is JsonObject co ? co : new JsonObject();

        // === QR LOGIN CHANNELS ===
        if (key == "weixin" || key == "wechat")
        {
            bool myEn = curObj.TryGetPropertyValue("enabled", out var enVal) && enVal?.ToString() == "true";
            var f = new Form
            {
                Text = "\u914D\u7F6E " + name, Size = new Size(480, 270), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Theme.Font(10f)
            };
            f.Controls.Add(new Label
            {
                Text = name + " \u901A\u8FC7 QR \u7801\u626B\u63CF\u767B\u5F55\u914D\u5BF9\uFF0C\u65E0\u9700\u624B\u52A8\u586B\u5199\u53C2\u6570\u3002\n\n\u64CD\u4F5C\u6B65\u9AA4\uFF1A\n1. \u70B9\u51FB\u4E0B\u65B9\u300C\u4FDD\u5B58\u300D\u542F\u7528\u8BE5\u9891\u9053\n2. \u70B9\u51FB\u300C\u5B89\u88C5\u63D2\u4EF6\u300D\uFF08\u5982\u672A\u5B89\u88C5\uFF09\n3. \u91CD\u542F OpenClaw Gateway\n4. \u67E5\u770B Gateway \u65E5\u5FD7\u83B7\u53D6\u4E8C\u7EF4\u7801\u8FDB\u884C\u626B\u63CF",
                Location = new Point(12, 12), AutoSize = true, BackColor = Color.Transparent
            });
            var cb1 = new CheckBox { Text = "\u542F\u7528\u8BE5\u9891\u9053", Checked = myEn, Location = new Point(12, 160), AutoSize = true, BackColor = Color.Transparent };
            f.Controls.Add(cb1);
            var bs1 = new Button { Text = "\u4FDD\u5B58", FlatStyle = FlatStyle.Flat, BackColor = Theme.QqBlue, ForeColor = Theme.FcWhite, Location = new Point(12, 195), Size = new Size(90, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
            bs1.Click += (_, _) =>
            {
                chObj[key] = new JsonObject { ["enabled"] = cb1.Checked };
                if (cfg == null) { cfg = new JsonObject(); cfg["channels"] = chObj; } else cfg["channels"] = chObj;
                OpenClawRuntime.SaveConfig(cfg); f.Close();
                MessageBox.Show(name + (cb1.Checked ? " \u5DF2\u542F\u7528" : " \u5DF2\u5173\u95ED") + "\uFF0C\u8BF7\u91CD\u542F Gateway\u3002", "\u4FDD\u5B58\u6210\u529F", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            f.Controls.Add(bs1);
            var bi1 = new Button { Text = "\u5B89\u88C5\u63D2\u4EF6", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x52, 0xC4, 0x1A), ForeColor = Theme.FcWhite, Location = new Point(112, 195), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
            bi1.Click += (_, _) => TryInstallWeixin();
            f.Controls.Add(bi1);
            f.ShowDialog();
            return;
        }

        // === FORM-BASED CHANNELS ===
        var form = new Form
        {
            Text = "\u914D\u7F6E " + name, Size = new Size(500, 230 + fields.Length * 40 + extra),
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, Font = Theme.Font(10f)
        };

        var tbs = new List<(string field, TextBox tb)>();
        int y = 12;

        if (isExternal)
        {
            form.Controls.Add(new Label { Text = "\u26A0 \u8BE5\u9891\u9053\u9700\u8981\u5148\u624B\u52A8\u5B89\u88C5\u5916\u90E8\u63D2\u4EF6\uFF01", ForeColor = Color.OrangeRed, Font = Theme.Font(9f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y) });
            y += 50;
        }
        if (isDownload)
        {
            form.Controls.Add(new Label { Text = "\u26A0 \u8BE5\u9891\u9053\u9700\u8981\u5148\u4E0B\u8F7D\u5B89\u88C5\u63D2\u4EF6\uFF01", ForeColor = Color.Orange, Font = Theme.Font(9f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y) });
            y += 24;
        }

        foreach (var (label, field, isPwd) in fields)
        {
            form.Controls.Add(new Label { Text = label + ":", Location = new Point(12, y + 4), AutoSize = true, BackColor = Color.Transparent });
            var tb = new TextBox
            {
                Location = new Point(120, y), Width = 350,
                Text = curObj.TryGetPropertyValue(field, out var v) ? (v?.ToString() ?? "") : "",
                UseSystemPasswordChar = isPwd
            };
            form.Controls.Add(tb);
            tbs.Add((field, tb));
            y += 34;
        }

        bool enabled2 = curObj.TryGetPropertyValue("enabled", out var ev2) && ev2?.ToString() == "true";
        var cb2 = new CheckBox { Text = "\u542F\u7528\u8BE5\u9891\u9053", Checked = enabled2, Location = new Point(120, y), AutoSize = true, BackColor = Color.Transparent };
        form.Controls.Add(cb2);
        y += 34;

        var btnSave = new Button { Text = "\u4FDD\u5B58\u914D\u7F6E", FlatStyle = FlatStyle.Flat, BackColor = Theme.QqBlue, ForeColor = Theme.FcWhite, Location = new Point(120, y), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        btnSave.Click += (_, __) =>
        {
            var nObj = new JsonObject { ["enabled"] = cb2.Checked };
            foreach (var (field, tb) in tbs) if (!string.IsNullOrWhiteSpace(tb.Text)) nObj[field] = tb.Text;
            chObj[key] = nObj;
            if (cfg == null) { cfg = new JsonObject(); cfg["channels"] = chObj; } else cfg["channels"] = chObj;
            OpenClawRuntime.SaveConfig(cfg); form.Close();
            MessageBox.Show(name + " \u914D\u7F6E\u5DF2\u4FDD\u5B58\uFF0C\u8BF7\u91CD\u542F Gateway \u751F\u6548\u3002", "\u4FDD\u5B58\u6210\u529F", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        form.Controls.Add(btnSave);

        if (isDownload)
        {
            var ib = new Button { Text = "\u5B89\u88C5\u63D2\u4EF6", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x52, 0xC4, 0x1A), ForeColor = Theme.FcWhite, Location = new Point(230, y), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
            ib.Click += (_, __) => TryInstallPlugin(key, name);
            form.Controls.Add(ib);
        }

        form.ShowDialog();
    }

    void TryInstallPlugin(string key, string name)
    {
        var pm = new Dictionary<string, string> { ["line"] = "@openclaw/line", ["matrix"] = "@openclaw/matrix", ["googlechat"] = "@openclaw/googlechat", ["mattermost"] = "@openclaw/mattermost" };
        if (!pm.TryGetValue(key, out var pkg)) { MessageBox.Show("\u8BF7\u624B\u52A8\u5B89\u88C5: npm install -g <\u5305\u540D>", "\u624B\u52A8\u5B89\u88C5"); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c npm install -g " + pkg, UseShellExecute = true, CreateNoWindow = false }); MessageBox.Show(name + " \u63D2\u4EF6\u5B89\u88C5\u5DF2\u542F\u52A8\u3002", "\u5B89\u88C5\u4E2D"); }
        catch (Exception ex) { MessageBox.Show("\u5B89\u88C5\u5931\u8D25: " + ex.Message); }
    }

    void TryInstallWeixin()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c npx -y @tencent-weixin/openclaw-weixin-cli@latest install", UseShellExecute = true, CreateNoWindow = false }); MessageBox.Show("\u4E2A\u4EBA\u5FAE\u4FE1\u63D2\u4EF6\u5B89\u88C5\u7A0B\u5E8F\u5DF2\u542F\u52A8\u3002\n\n\u5B89\u88C5\u5B8C\u6210\u540E\uFF1A\n1. \u5728\u9891\u9053\u5217\u8868\u542F\u7528\u300C\u5FAE\u4FE1(\u4E2A\u4EBA)\u300D\n2. \u91CD\u542F Gateway\n3. \u67E5\u770B\u65E5\u5FD7\u83B7\u53D6\u4E8C\u7EF4\u7801\u626B\u63CF", "\u5B89\u88C5\u4E2D"); }
        catch (Exception ex) { MessageBox.Show("\u542F\u52A8\u5931\u8D25: " + ex.Message + "\n\n\u8BF7\u624B\u52A8\u8FD0\u884C:\nnpx -y @tencent-weixin/openclaw-weixin-cli@latest install"); }
    }

    }
