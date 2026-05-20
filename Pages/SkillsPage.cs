using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawManager;

public class SkillsPage
{
    Panel body = null!;
    DataGridView grid = null!;
    Label statusBar = null!;
    ProgressBar progressBar = null!;
    Button cancelBtn = null!;
    TextBox searchBox = null!;
    CancellationTokenSource? cts;

    readonly List<Entry> entries = new();
    readonly Dictionary<string, SkillInfo> skillInfos = new();
    string workspaceDir = "";

    static readonly Dictionary<string, (string name, string desc)> KnownPlugins = new()
    {
        ["qqbot"] = ("QQ Bot", "QQ 机器人消息收发"),
        ["weather"] = ("天气查询", "实时天气与预报查询"),
        ["browser"] = ("浏览器自动化", "网页浏览与自动化控制"),
        ["memory-core"] = ("记忆管理", "会话记忆存储与检索"),
        ["cron"] = ("定时任务", "提醒与周期任务"),
        ["telegram"] = ("Telegram", "Telegram Bot 频道���入"),
        ["discord"] = ("Discord", "Discord Bot 频道接入"),
        ["slack"] = ("Slack", "Slack 工作区接入"),
        ["matrix"] = ("Matrix", "Matrix 协议接入"),
        ["document-extract"] = ("文档提取", "PDF 与 Office 文档解析"),
        ["web-readability"] = ("网页正文提取", "抽取网页可读正文"),
        ["duckduckgo"] = ("DuckDuckGo", "免费搜索后端"),
        ["device-pair"] = ("设备配对", "生成配对码连接设备")
    };

    class Entry
    {
        public string Id = "";
        public string Name = "";
        public string Desc = "";
        public string Type = "";
        public string Status = "";
        public string Action = "";
        public string Source = "";
        public string Origin = "";
        public bool Detected;
        public bool Enabled;
        public bool IsSkill;
        public bool IsBundled;
    }

    class SkillInfo
    {
        public string Name = "";
        public string Desc = "";
        public bool Eligible;
        public bool Disabled;
        public bool IsBundled;
        public string MissingReason = "";
        public List<string> MissingBins = new();
        public List<string> MissingAnyBins = new();
    }

    string SkillsDir => string.IsNullOrEmpty(workspaceDir)
        ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "workspace", "skills")
        : Path.Combine(workspaceDir, "skills");

    public void Build(Panel p)
    {
        body = p;
        body.Controls.Clear();
        body.BackColor = Theme.Bg;
        body.AutoScroll = false;

        int pad = Theme.S(12);
        int width = body.ClientSize.Width - pad * 2;
        int y = pad;

        body.Controls.Add(new Label
        {
            Text = "技能管理",
            ForeColor = Theme.Fc,
            Font = Theme.Font(13f, FontStyle.Bold),
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(pad, y)
        });

        var refreshBtn = Theme.BtnWhite("刷新");
        refreshBtn.Location = new Point(body.ClientSize.Width - pad - refreshBtn.PreferredSize.Width, y - 2);
        refreshBtn.Click += async (_, _) => await LoadAsync();
        body.Controls.Add(refreshBtn);
        y += Theme.S(34);

        searchBox = Theme.TextBox(placeholder: "搜索 ClawHub 技能市场...");
        searchBox.Location = new Point(pad, y);
        searchBox.Size = new Size(Math.Max(220, width - Theme.S(92)), Theme.S(28));
        searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                using var dlg = new SearchDialog(searchBox.Text.Trim());
                dlg.ShowDialog(body.FindForm());
            }
        };
        body.Controls.Add(searchBox);

        var searchBtn = Theme.Btn("搜索");
        searchBtn.Location = new Point(pad + searchBox.Width + Theme.S(8), y - 1);
        searchBtn.Click += (_, _) =>
        {
            using var dlg = new SearchDialog(searchBox.Text.Trim());
            dlg.ShowDialog(body.FindForm());
        };
        body.Controls.Add(searchBtn);
        y += Theme.S(38);

        grid = Theme.Grid();
        grid.Location = new Point(pad, y);
        grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        grid.Size = new Size(width, body.ClientSize.Height - y - Theme.S(42));
        grid.Columns.Add("name", "名称");
        grid.Columns.Add("desc", "说明");
        grid.Columns.Add("type", "类型");
        grid.Columns.Add("status", "状态");
        grid.Columns.Add("source", "来源");
        grid.Columns.Add(new DataGridViewButtonColumn { Name = "action", HeaderText = "操作", UseColumnTextForButtonValue = false, FlatStyle = FlatStyle.Flat });
        grid.Columns["name"].FillWeight = 16;
        grid.Columns["desc"].FillWeight = 34;
        grid.Columns["type"].FillWeight = 12;
        grid.Columns["status"].FillWeight = 16;
        grid.Columns["source"].FillWeight = 12;
        grid.Columns["action"].FillWeight = 10;
        grid.CellFormatting += GridCellFormatting;
        grid.CellContentClick += OnGridAction;
        body.Controls.Add(grid);

        // ── 底部状态栏，用 Panel 包裹，Dock=Bottom，不被 grid 覆盖 ──
        var bottomBar = new Panel { Height = Theme.S(32), Dock = DockStyle.Bottom, BackColor = Color.Transparent };
        body.Controls.Add(bottomBar);

        statusBar = new Label
        {
            Text = "正在加载...", ForeColor = Theme.Acc, Font = Theme.Font(9f),
            AutoSize = false, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(pad, Theme.S(5))
        };
        bottomBar.Controls.Add(statusBar);

        progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee, Visible = true,
            Size = new Size(Theme.S(100), Theme.S(14)),
            Location = new Point(Theme.S(120), Theme.S(9))
        };
        bottomBar.Controls.Add(progressBar);

        cancelBtn = new Button
        {
            Text = "取消", AutoSize = true,
            FlatStyle = FlatStyle.Flat, BackColor = Theme.Red, ForeColor = Color.White,
            Font = Theme.Font(9f), Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false,
            Visible = false
        };
        cancelBtn.Location = new Point(progressBar.Right + Theme.S(12), Theme.S(4));
        cancelBtn.Click += (_, _) => { cts?.Cancel(); SetIdle("已取消当前操作"); };
        bottomBar.Controls.Add(cancelBtn);

        // Resize 自适应
        bottomBar.Resize += (_, _) =>
        {
            int bw = bottomBar.Width;
            statusBar.Size = new Size(bw - Theme.S(170), Theme.S(22));
            progressBar.Location = new Point(Theme.S(120), Theme.S(9));
            cancelBtn.Location = new Point(progressBar.Right + Theme.S(12), Theme.S(4));
        };

        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        SetBusy("正在读取插件与技能...");
        var pluginsTask = Task.Run(ListPlugins);
        var skillsTask = Task.Run(ListSkills);
        var configTask = Task.Run(OpenClawRuntime.ReadConfig);
        await Task.WhenAll(pluginsTask, skillsTask, configTask);

        BuildEntries(pluginsTask.Result, skillsTask.Result, configTask.Result);
        if (body.IsDisposed) return;
        body.BeginInvoke(() =>
        {
            PopulateGrid();
            SetIdle($"就绪，共 {entries.Count} 项");
        });
    }

    void BuildEntries(Dictionary<string, (string origin, bool enabled)> plugins, List<SkillInfo> skills, JsonObject? cfg)
    {
        entries.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enabledEntries = cfg?["plugins"]?["entries"]?.AsObject() ?? new JsonObject();

        foreach (var kv in plugins.OrderByDescending(x => x.Value.enabled).ThenBy(x => x.Key))
        {
            if (!seen.Add(kv.Key)) continue;
            var (name, desc) = Lookup(kv.Key);
            bool enabled = enabledEntries.TryGetPropertyValue(kv.Key, out var node)
                && node is JsonObject obj
                && obj.TryGetPropertyValue("enabled", out var en)
                && en?.ToString() == "true";

            entries.Add(new Entry
            {
                Id = kv.Key, Name = name, Desc = desc,
                Type = kv.Value.origin == "bundled" ? "内置插件" : "已安装插件",
                Status = enabled ? "已启用" : "已停用",
                Action = enabled ? "停用" : "启用",
                Source = kv.Value.origin == "bundled" ? "官方内置" : "外部安装",
                Origin = kv.Value.origin, Detected = true,
                Enabled = enabled, IsBundled = kv.Value.origin == "bundled"
            });
        }

        foreach (var skill in skills.OrderByDescending(s => s.Eligible && !s.Disabled).ThenBy(s => s.Name))
        {
            if (!seen.Add(skill.Name)) continue;
            bool enabled = skill.Eligible && !skill.Disabled;
            entries.Add(new Entry
            {
                Id = skill.Name, Name = skill.Name, Desc = skill.Desc,
                Type = skill.Eligible ? "技能" : "不可用",
                Status = skill.Eligible ? (enabled ? "已启用" : "已停用") : skill.MissingReason,
                Action = skill.Eligible ? "-" : "安装依赖",
                Source = skill.IsBundled ? "官方内置" : "工作区",
                Origin = "skill", Detected = true,
                Enabled = enabled, IsSkill = true, IsBundled = skill.IsBundled
            });
        }

        foreach (var kv in KnownPlugins)
        {
            if (!seen.Add(kv.Key)) continue;
            entries.Add(new Entry
            {
                Id = kv.Key, Name = kv.Value.name, Desc = kv.Value.desc,
                Type = "可安装", Status = "未安装", Action = "安装", Source = "ClawHub",
                Detected = false
            });
        }
    }

    void PopulateGrid()
    {
        grid.Rows.Clear();
        foreach (var entry in entries)
        {
            int row = grid.Rows.Add(entry.Name, entry.Desc, entry.Type, entry.Status, entry.Source, entry.Action);
            grid.Rows[row].Tag = entry;
        }
    }

    void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (grid.Rows[e.RowIndex].Tag is not Entry entry) return;

        if (grid.Columns[e.ColumnIndex].Name == "status")
        {
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (entry.Status.Contains("已启用")) cell.Style.ForeColor = Theme.Grn;
            else if (entry.Status.Contains("未安装") || entry.Status.Contains("不可用")) cell.Style.ForeColor = Theme.Warn;
            else cell.Style.ForeColor = Theme.Fc2;
        }

        if (grid.Columns[e.ColumnIndex].Name == "action" && grid.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewButtonCell cellBtn)
        {
            cellBtn.Style.BackColor = entry.Action switch
            {
                "安装" or "启用" or "安装依赖" => Theme.Acc,
                "停用" => Theme.Red,
                _ => Theme.BgElevated
            };
            cellBtn.Style.ForeColor = entry.Action == "-" ? Theme.Fc2 : Theme.FcWhite;
        }
    }

    async void OnGridAction(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != "action") return;
        if (grid.Rows[e.RowIndex].Tag is not Entry entry || entry.Action == "-") return;

        if (entry.Action == "安装") await InstallPluginAsync(entry);
        else if (entry.Action == "启用") TogglePlugin(entry, true);
        else if (entry.Action == "停用") TogglePlugin(entry, false);
        else if (entry.Action == "安装依赖") await InstallDepsAsync(entry);

        await LoadAsync();
    }

    void TogglePlugin(Entry entry, bool enable)
    {
        SetPluginConfig(entry.Id, enable);
    }

    async Task InstallPluginAsync(Entry entry)
    {
        SetBusy($"正在安装 {entry.Name}...");
        try
        {
            await Task.Run(() => OpenClawRuntime.RunOpenClawSync($"plugins install {entry.Id}", 30000));
            SetIdle($"{entry.Name} 安装完成");
        }
        catch (Exception ex) { SetError($"安装失败: {ex.Message}"); }
    }

    async Task InstallDepsAsync(Entry entry)
    {
        if (!skillInfos.TryGetValue(entry.Name, out var info)) return;

        var allBins = new List<string>(info.MissingBins);
        foreach (var bin in info.MissingAnyBins) allBins.Add(bin);

        if (allBins.Count == 0) return;

        int ok = 0, fail = 0;
        foreach (var bin in allBins)
        {
            SetBusy($"安装 {bin} ...");
            try
            {
                var result = await Task.Run(() => OpenClawRuntime.RunOpenClawSync($"skills install-deps {entry.Name}", 60000));
                if (result.code == 0) ok++; else fail++;
            }
            catch { fail++; }
        }
        SetIdle($"依赖安装完成: 成功 {ok}，失败 {fail}");
    }

    Dictionary<string, (string origin, bool enabled)> ListPlugins()
    {
        var result = new Dictionary<string, (string, bool)>();
        var output = OpenClawRuntime.RunOpenClawSync("plugins list --json", 7000);
        if (string.IsNullOrWhiteSpace(output.stdout)) return result;

        try
        {
            var root = JsonNode.Parse(output.stdout);
            var plugins = root?["plugins"]?.AsArray();
            if (plugins == null) return result;
            foreach (var item in plugins)
            {
                var id = item?["id"]?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;
                result[id] = (item?["origin"]?.ToString() ?? "", item?["enabled"]?.ToString() == "true");
            }
        }
        catch { }
        return result;
    }

    List<SkillInfo> ListSkills()
    {
        skillInfos.Clear();
        var list = new List<SkillInfo>();
        var output = OpenClawRuntime.RunOpenClawSync("skills list --json", 7000);
        if (string.IsNullOrWhiteSpace(output.stdout)) return list;

        try
        {
            var root = JsonNode.Parse(output.stdout);
            workspaceDir = root?["workspaceDir"]?.ToString() ?? "";
            var skills = root?["skills"]?.AsArray();
            if (skills == null) return list;

            foreach (var item in skills)
            {
                var name = item?["name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                var info = new SkillInfo
                {
                    Name = name,
                    Desc = OpenClawRuntime.Trim(item?["description"]?.ToString() ?? "", 90),
                    Eligible = item?["eligible"]?.GetValue<bool>() ?? false,
                    Disabled = item?["disabled"]?.GetValue<bool>() ?? true,
                    IsBundled = item?["bundled"]?.GetValue<bool>() ?? false
                };

                var missing = item?["missing"]?.AsObject();
                info.MissingBins = ReadArray(missing, "bins");
                info.MissingAnyBins = ReadArray(missing, "anyBins");
                var parts = new List<string>();
                if (info.MissingBins.Count > 0) parts.Add("需安装 " + string.Join(", ", info.MissingBins));
                if (info.MissingAnyBins.Count > 0) parts.Add("需安装其一: " + string.Join(", ", info.MissingAnyBins));
                info.MissingReason = parts.Count > 0 ? string.Join("; ", parts) : "不可用";

                list.Add(info);
                skillInfos[name] = info;
            }
        }
        catch { }
        return list;
    }

    (string name, string desc) Lookup(string id)
        => KnownPlugins.TryGetValue(id, out var val) ? val : (id, "");

    void SetPluginConfig(string id, bool enabled)
    {
        var cfg = OpenClawRuntime.ReadConfig() ?? new JsonObject();
        if (cfg["plugins"] is not JsonObject plugins) { plugins = new JsonObject(); cfg["plugins"] = plugins; }
        if (plugins["entries"] is not JsonObject entriesNode) { entriesNode = new JsonObject(); plugins["entries"] = entriesNode; }
        if (!entriesNode.TryGetPropertyValue(id, out var node) || node is not JsonObject plugin)
        {
            plugin = new JsonObject();
            entriesNode[id] = plugin;
        }
        plugin["enabled"] = enabled;
        OpenClawRuntime.SaveConfig(cfg);
    }

    static List<string> ReadArray(JsonObject? obj, string key)
        => OpenClawRuntime.ReadArray(obj, key);

    void SetBusy(string text)
    {
        statusBar.Text = text;
        statusBar.ForeColor = Theme.Acc;
        progressBar.Visible = true;
        cancelBtn.Visible = true;
    }

    void SetIdle(string text)
    {
        statusBar.Text = text;
        statusBar.ForeColor = Theme.Fc2;
        progressBar.Visible = false;
        cancelBtn.Visible = false;
    }

    void SetError(string text)
    {
        statusBar.Text = text;
        statusBar.ForeColor = Theme.Red;
        progressBar.Visible = false;
        cancelBtn.Visible = false;
    }
}
