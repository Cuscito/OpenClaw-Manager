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
        ("qqbot",       "QQ Bot",       "腾讯 QQ 机器人",       OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("telegram",    "Telegram",     "Bot API (grammY)",                         OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("discord",     "Discord",      "Bot API + Gateway",                        OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("whatsapp",    "WhatsApp",     "Baileys QR 配对",                  OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("signal",      "Signal",       "signal-cli 隐私通信",      OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("slack",       "Slack",        "Bolt SDK 工作区应用",  OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("webchat",     "WebChat",      "Gateway 内置网页聊天", OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("msteams",     "Teams",        "Microsoft Bot Framework",                  OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("line",        "LINE",         "LINE Messaging API",                       OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginDownload")),
        ("feishu",      "飞书", "Feishu/Lark WebSocket",                    OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("wechat",      "微信(企业)", "iLink Bot QR登录",   OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginExternal")),
        ("weixin",      "微信(个人)", "个人微信 QR扫码", OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginExternal")),
        ("matrix",      "Matrix",       "Matrix 协议",                      OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginDownload")),
        ("irc",         "IRC",          "经典 IRC 服务器",       OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("nostr",       "Nostr",        "去中心化 DM",             OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("bluebubbles", "BlueBubbles",  "iMessage macOS 服务",              OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("twitch",      "Twitch",       "Twitch 聊天室",               OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("googlechat",  "Google Chat",  "Google Chat API",                          OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginDownload")),
        ("nextcloud",   "Nextcloud",    "自托管 Talk",                  OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("zalo",        "Zalo",         "越南流行通信",      OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginBuiltIn")),
        ("mattermost",  "Mattermost",   "Bot API + WebSocket",                      OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginDownload")),
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
            body.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsProFeature"), ForeColor = Theme.Fc2, Font = Theme.Font(12f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 20) });
            body.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsActivatePro"), ForeColor = Theme.Fc2, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 50) });
            return;
        }

        body.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsManage"), ForeColor = Theme.Fc, Font = Theme.Font(13f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 12) });
        body.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsSubtitle"), ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 36) });

        grid = Theme.Grid();
        grid.Location = new Point(12, 64);
        grid.Size = new Size(body.ClientSize.Width - 24, body.ClientSize.Height - 80);
        grid.Columns.Add("name", OpenClawManager.Properties.LanguageManager.GetString("Channel")); grid.Columns["name"].FillWeight = 16;
        grid.Columns.Add("desc", OpenClawManager.Properties.LanguageManager.GetString("ChannelsDesc")); grid.Columns["desc"].FillWeight = 28;
        grid.Columns.Add("plugin", OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginStatus")); grid.Columns["plugin"].FillWeight = 12;
        grid.Columns.Add("enabled", OpenClawManager.Properties.LanguageManager.GetString("ChannelsSwitch")); grid.Columns["enabled"].FillWeight = 8;
        grid.Columns.Add("config", OpenClawManager.Properties.LanguageManager.GetString("ChannelsConfigure")); grid.Columns["config"].FillWeight = 12;
        grid.Columns.Add("status", OpenClawManager.Properties.LanguageManager.GetString("ChannelsStatus")); grid.Columns["status"].FillWeight = 24;

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
            if (ch.key == "webchat") status = OpenClawManager.Properties.LanguageManager.GetString("ChannelsBuiltInChannel");
            else if (!en) status = OpenClawManager.Properties.LanguageManager.GetString("NotEnabled");
            else if (hasCfg) status = OpenClawManager.Properties.LanguageManager.GetString("Configured");
            else status = OpenClawManager.Properties.LanguageManager.GetString("PendingConfig");

            grid.Rows.Add(ch.name, ch.desc, ch.pluginType, en ? "☑" : "☐", "⚙ " + OpenClawManager.Properties.LanguageManager.GetString("ChannelsConfigure"), status);
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

        bool isExternal = pluginType == OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginExternal");
        bool isDownload = pluginType == OpenClawManager.Properties.LanguageManager.GetString("ChannelsPluginDownload");
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
                Text = string.Format(OpenClawManager.Properties.LanguageManager.GetString("ChannelsConfigTitle"), name), Size = new Size(480, 270), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = Theme.Font(10f)
            };
            f.Controls.Add(new Label
            {
                Text = name + " \u901A\u8FC7 QR \u7801\u626B\u63CF\u767B\u5F55\u914D\u5BF9\uFF0C\u65E0\u9700\u624B\u52A8\u586B\u5199\u53C2\u6570\u3002\n\n\u64CD\u4F5C\u6B65\u9AA4\uFF1A\n1. \u70B9\u51FB\u4E0B\u65B9\u300C\u4FDD\u5B58\u300D\u542F\u7528\u8BE5\u9891\u9053\n2. \u70B9\u51FB\u300C\u5B89\u88C5\u63D2\u4EF6\u300D\uFF08\u5982\u672A\u5B89\u88C5\uFF09\n3. \u91CD\u542F OpenClaw Gateway\n4. \u67E5\u770B Gateway \u65E5\u5FD7\u83B7\u53D6\u4E8C\u7EF4\u7801\u8FDB\u884C\u626B\u63CF",
                Location = new Point(12, 12), AutoSize = true, BackColor = Color.Transparent
            });
            var cb1 = new CheckBox { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsEnableChannel"), Checked = myEn, Location = new Point(12, 160), AutoSize = true, BackColor = Color.Transparent };
            f.Controls.Add(cb1);
            var bs1 = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsSave"), FlatStyle = FlatStyle.Flat, BackColor = Theme.QqBlue, ForeColor = Theme.FcWhite, Location = new Point(12, 195), Size = new Size(90, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
            bs1.Click += (_, _) =>
            {
                chObj[key] = new JsonObject { ["enabled"] = cb1.Checked };
                if (cfg == null) { cfg = new JsonObject(); cfg["channels"] = chObj; } else cfg["channels"] = chObj;
                OpenClawRuntime.SaveConfig(cfg); f.Close();
                MessageBox.Show(name + (cb1.Checked ? OpenClawManager.Properties.LanguageManager.GetString("ChannelsEnabledMsg").Replace("{0}","") : OpenClawManager.Properties.LanguageManager.GetString("ChannelsDisabledMsg").Replace("{0}","")) + "，请重启 Gateway。", OpenClawManager.Properties.LanguageManager.GetString("SaveSuccess"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            f.Controls.Add(bs1);
            var bi1 = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsInstallPlugin"), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x52, 0xC4, 0x1A), ForeColor = Theme.FcWhite, Location = new Point(112, 195), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
            bi1.Click += (_, _) => TryInstallWeixin();
            f.Controls.Add(bi1);
            f.ShowDialog();
            return;
        }

        // === FORM-BASED CHANNELS ===
        var form = new Form
        {
            Text = string.Format(OpenClawManager.Properties.LanguageManager.GetString("ChannelsConfigTitle"), name), Size = new Size(500, 230 + fields.Length * 40 + extra),
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, Font = Theme.Font(10f)
        };

        var tbs = new List<(string field, TextBox tb)>();
        int y = 12;

        if (isExternal)
        {
            form.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsNeedExternal"), ForeColor = Color.OrangeRed, Font = Theme.Font(9f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y) });
            y += 50;
        }
        if (isDownload)
        {
            form.Controls.Add(new Label { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsNeedDownload"), ForeColor = Color.Orange, Font = Theme.Font(9f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, y) });
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
        var cb2 = new CheckBox { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsEnableChannel"), Checked = enabled2, Location = new Point(120, y), AutoSize = true, BackColor = Color.Transparent };
        form.Controls.Add(cb2);
        y += 34;

        var btnSave = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsSaveConfig"), FlatStyle = FlatStyle.Flat, BackColor = Theme.QqBlue, ForeColor = Theme.FcWhite, Location = new Point(120, y), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
        btnSave.Click += (_, __) =>
        {
            var nObj = new JsonObject { ["enabled"] = cb2.Checked };
            foreach (var (field, tb) in tbs) if (!string.IsNullOrWhiteSpace(tb.Text)) nObj[field] = tb.Text;
            chObj[key] = nObj;
            if (cfg == null) { cfg = new JsonObject(); cfg["channels"] = chObj; } else cfg["channels"] = chObj;
            OpenClawRuntime.SaveConfig(cfg); form.Close();
            MessageBox.Show(name +  string.Format(OpenClawManager.Properties.LanguageManager.GetString("ChannelsSavedMsg"), name), OpenClawManager.Properties.LanguageManager.GetString("SaveSuccess"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        form.Controls.Add(btnSave);

        if (isDownload)
        {
            var ib = new Button { Text = OpenClawManager.Properties.LanguageManager.GetString("ChannelsInstallPlugin"), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0x52, 0xC4, 0x1A), ForeColor = Theme.FcWhite, Location = new Point(230, y), Size = new Size(100, 32), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false };
            ib.Click += (_, __) => TryInstallPlugin(key, name);
            form.Controls.Add(ib);
        }

        form.ShowDialog();
    }

    void TryInstallPlugin(string key, string name)
    {
        var pm = new Dictionary<string, string> { ["line"] = "@openclaw/line", ["matrix"] = "@openclaw/matrix", ["googlechat"] = "@openclaw/googlechat", ["mattermost"] = "@openclaw/mattermost" };
        if (!pm.TryGetValue(key, out var pkg)) { MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("ChannelsInstallManual"), OpenClawManager.Properties.LanguageManager.GetString("ChannelsSaveConfig")); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c npm install -g " + pkg, UseShellExecute = true, CreateNoWindow = false }); MessageBox.Show(name +  string.Format(OpenClawManager.Properties.LanguageManager.GetString("ChannelsInstalledMsg"), name), OpenClawManager.Properties.LanguageManager.GetString("ChannelsInstalling")); }
        catch (Exception ex) { MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("ChannelsInstallFailed") + ": " + ex.Message); }
    }

    void TryInstallWeixin()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c npx -y @tencent-weixin/openclaw-weixin-cli@latest install", UseShellExecute = true, CreateNoWindow = false }); MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("ChannelsWeixinGuide"), OpenClawManager.Properties.LanguageManager.GetString("ChannelsInstalling")); }
        catch (Exception ex) { MessageBox.Show(OpenClawManager.Properties.LanguageManager.GetString("ChannelsWeixinStartFailed") + ": " + ex.Message + "\n\n" + OpenClawManager.Properties.LanguageManager.GetString("ChannelsWeixinManual")); }
    }

    }
