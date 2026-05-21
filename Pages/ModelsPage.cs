using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawManager;

public class ModelsPage
{
    Panel body = null!;
    TableLayoutPanel mainTable = null!;
    DataGridView provGrid = null!;
    DataGridView modelGrid = null!;
    TextBox providerBox = null!;
    TextBox modelBox = null!;
    TextBox aliasBox = null!;

    public void Build(Panel p)
    {
        body = p;
        body.Controls.Clear();
        body.BackColor = Theme.Bg;
        body.AutoScroll = false;

        int pad = Theme.S(12);
        int w = body.ClientSize.Width - pad * 2 - SystemInformation.VerticalScrollBarWidth;

        int y = pad;
        AddTitle("模型配置", "管理 API 供应商、模型列表和默认模型。", pad, ref y, w);

        // ── 供应商卡片 ──
        y = BuildProviderCard(pad, y, w);
        // ── 模型卡片 ──
        y = BuildModelCard(pad, y, w);
        // ── 手动添加卡片 ──
        BuildManualCard(pad, y, w);

        RefreshProviders();
        RefreshModels();
        Theme.ApplyTo(body);
    }

    void AddTitle(string title, string subtitle, int x, ref int y, int w)
    {
        var tl = new Label { Text = title, ForeColor = Theme.Fc, Font = Theme.Font(13f, FontStyle.Bold), AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, y) };
        body.Controls.Add(tl);
        y += Theme.S(24);
        var sl = new Label { Text = subtitle, ForeColor = Theme.Fc2, Font = Theme.Font(9f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, y) };
        body.Controls.Add(sl);
        y += Theme.S(26);
    }

    // ═══ 供应商卡片 ═══
    int BuildProviderCard(int x, int y, int w)
    {
        int cardH = Theme.S(178);
        var card = Theme.Card(x, y, w, cardH, "API 供应商");
        body.Controls.Add(card);

        provGrid = Theme.Grid();
        provGrid.ReadOnly = false;
        provGrid.Location = new Point(Theme.S(14), Theme.S(40));
        provGrid.Size = new Size(card.Width - Theme.S(28), Theme.S(88));
        provGrid.Columns.Add("id", "Provider ID");
        provGrid.Columns.Add("url", "Base URL");
        provGrid.Columns.Add("key", "API Key");
        provGrid.Columns["id"].Width = (provGrid.Width - 40) / 3;
        provGrid.Columns["url"].Width = (provGrid.Width - 40) / 3;
        provGrid.Columns["key"].Width = (provGrid.Width - 40) / 3;
        provGrid.Columns["key"].ReadOnly = true; // key 列不可编辑（显示掩码）
        // 编辑后自动保存
        provGrid.CellEndEdit += (_, e) =>
        {
            var pid = provGrid.Rows[e.RowIndex].Cells[0].Value?.ToString() ?? "";
            var url = provGrid.Rows[e.RowIndex].Cells[1].Value?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(pid) && !string.IsNullOrWhiteSpace(url))
            {
                var cfg = OpenClawRuntime.ReadConfig();
                if (cfg == null) return;
                var providers = cfg["models"]?["providers"]?.AsObject();
                if (providers == null || !providers.ContainsKey(pid)) return;
                providers[pid]!["baseUrl"] = url;
                OpenClawRuntime.SaveConfig(cfg);
            }
        };
        provGrid.SelectionChanged += (_, _) =>
        {
            // 选中供应商后自动填入手动添加的 provider 默认值
            if (provGrid.SelectedRows.Count > 0)
            {
                var pid = provGrid.SelectedRows[0].Cells[0].Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(pid) && providerBox != null)
                    providerBox.Text = pid;
            }
        };
        card.Controls.Add(provGrid);

        var btns = new FlowLayoutPanel
        {
            Location = new Point(Theme.S(14), Theme.S(136)),
            Size = new Size(card.Width - Theme.S(28), Theme.S(32)),
            BackColor = Color.Transparent, WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        btns.Controls.Add(MakeBtn("+ 添加", Theme.Acc, (_, _) => ShowAddProviderDialog()));
        btns.Controls.Add(MakeBtn("删除", Theme.Red, (_, _) =>
        {
            if (provGrid.SelectedRows.Count == 0) { MessageBox.Show("请先选择供应商", "提示"); return; }
            var pid = provGrid.SelectedRows[0].Cells[0].Value?.ToString() ?? "";
            if (MessageBox.Show("同时删除该供应商下所有模型？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                DelProvAndModels(pid);
                RefreshProviders();
                RefreshModels();
            }
        }));
        btns.Controls.Add(MakeBtn("拉取模型", Theme.Acc, (_, _) =>
        {
            if (provGrid.SelectedRows.Count == 0) { MessageBox.Show("请先选择供应商", "提示"); return; }
            FetchModels(provGrid.SelectedRows[0].Cells[0].Value?.ToString() ?? "");
        }));
        card.Controls.Add(btns);

        return y + card.Height + Theme.S(10);
    }

    // ═══ 模型卡片 ═══
    int BuildModelCard(int x, int y, int w)
    {
        int cardH = Theme.S(190);
        var card = Theme.Card(x, y, w, cardH, "已配置模型");
        body.Controls.Add(card);

        modelGrid = Theme.Grid();
        modelGrid.Location = new Point(Theme.S(14), Theme.S(40));
        modelGrid.Size = new Size(card.Width - Theme.S(28), Theme.S(102));
        modelGrid.Columns.Add("id", "Model ID");
        modelGrid.Columns.Add("alias", "Alias");
        modelGrid.Columns.Add("prov", "Provider");
        modelGrid.Columns["id"].Width = (modelGrid.Width - 40) * 3 / 7;
        modelGrid.Columns["alias"].Width = (modelGrid.Width - 40) * 2 / 7;
        modelGrid.Columns["prov"].Width = (modelGrid.Width - 40) * 2 / 7;
        // 默认模型加粗标记
        modelGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            var mid = modelGrid.Rows[e.RowIndex].Cells[0].Value?.ToString() ?? "";
            if (mid == _defaultModel)
                e.CellStyle.Font = Theme.Font(9f, FontStyle.Bold);
        };
        modelGrid.CellDoubleClick += (_, _) =>
        {
            if (modelGrid.SelectedRows.Count == 0) return;
            var mid = modelGrid.SelectedRows[0].Cells[0].Value?.ToString() ?? "";
            var alias = modelGrid.SelectedRows[0].Cells[1].Value?.ToString() ?? "";
            if (MessageBox.Show("设为默认模型？\n" + mid + "\n（双击切换到其他面板不会触发）", "设置默认", MessageBoxButtons.YesNo) == DialogResult.Yes)
                SetDefault(mid);
        };
        card.Controls.Add(modelGrid);

        var btns = new FlowLayoutPanel
        {
            Location = new Point(Theme.S(14), Theme.S(152)),
            Size = new Size(card.Width - Theme.S(28), Theme.S(32)),
            BackColor = Color.Transparent, WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        btns.Controls.Add(MakeBtn("设为默认", Theme.Acc, (_, _) =>
        {
            if (modelGrid.SelectedRows.Count == 0) { MessageBox.Show("请先选择模型", "提示"); return; }
            var mid = modelGrid.SelectedRows[0].Cells[0].Value?.ToString() ?? "";
            if (MessageBox.Show("设为默认模型？\n" + mid, "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
                SetDefault(mid);
        }));
        btns.Controls.Add(MakeBtn("删除", Theme.Red, (_, _) =>
        {
            if (modelGrid.SelectedRows.Count == 0) { MessageBox.Show("请先选择模型", "提示"); return; }
            DelModel(modelGrid.SelectedRows[0].Cells[0].Value?.ToString() ?? "");
            RefreshModels();
        }));
        card.Controls.Add(btns);

        return y + card.Height + Theme.S(10);
    }

    // ═══ 手动添加卡片 ═══
    void BuildManualCard(int x, int y, int w)
    {
        int narrow = body.ClientSize.Width < 760 ? 1 : 0;
        int cardH = narrow == 1 ? Theme.S(176) : Theme.S(96);
        var card = Theme.Card(x, y, w, cardH, "手动添加模型");
        body.Controls.Add(card);

        var form = new FlowLayoutPanel
        {
            Location = new Point(Theme.S(14), Theme.S(40)),
            Size = new Size(card.Width - Theme.S(28), card.Height - Theme.S(48)),
            BackColor = Color.Transparent, WrapContents = true
        };

        providerBox = AddInput(form, "Provider ID（点击上方供应商自动填入）", narrow == 0 ? Theme.S(150) : card.Width - Theme.S(48));
        modelBox = AddInput(form, "Model ID", narrow == 0 ? Theme.S(200) : card.Width - Theme.S(48));
        aliasBox = AddInput(form, "Alias（可选）", narrow == 0 ? Theme.S(150) : card.Width - Theme.S(48));

        var addBtn = Theme.Btn("添加模型");
        addBtn.Margin = new Padding(0, Theme.S(18), Theme.S(8), 0);
        addBtn.Click += (_, _) =>
        {
            var prov = providerBox.Text.Trim();
            var mod = modelBox.Text.Trim();
            var alias = aliasBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(prov) || string.IsNullOrWhiteSpace(mod)) return;
            // 检查供应商是否存在
            var providers = OpenClawRuntime.ReadConfig()?["models"]?["providers"]?.AsObject();
            if (providers == null || !providers.ContainsKey(prov))
            {
                MessageBox.Show("供应商 \"" + prov + "\" 不存在，请先在供应商表中添加。", "提示");
                return;
            }
            var fullId = prov + "/" + mod;
            AddModel(fullId, string.IsNullOrWhiteSpace(alias) ? mod : alias);
            providerBox.Clear(); modelBox.Clear(); aliasBox.Clear();
            RefreshModels();
        };
        form.Controls.Add(addBtn);
        card.Controls.Add(form);
    }

    // ═══ 辅助方法 ═══
    Button MakeBtn(string text, Color bg, EventHandler click)
    {
        var btn = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat, BackColor = bg,
            ForeColor = Color.White, Font = Theme.Font(10f),
            Cursor = Cursors.Hand, Size = new Size(Theme.S(88), Theme.S(30)),
            FlatAppearance = { BorderSize = 0 }, UseVisualStyleBackColor = false,
            Margin = new Padding(0, 0, Theme.S(6), 0)
        };
        btn.Click += click;
        return btn;
    }

    TextBox AddInput(FlowLayoutPanel parent, string label, int width)
    {
        var group = new Panel { Size = new Size(width, Theme.S(56)), BackColor = Color.Transparent, Margin = new Padding(0, 0, Theme.S(10), Theme.S(6)) };
        group.Controls.Add(new Label { Text = label, ForeColor = Theme.Fc2, Font = Theme.Font(8.5f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 0) });
        var tb = Theme.TextBox();
        tb.Location = new Point(0, Theme.S(22));
        tb.Size = new Size(width, Theme.S(28));
        group.Controls.Add(tb);
        parent.Controls.Add(group);
        return tb;
    }

    // ═══ 添加供应商对话框 ═══
    void ShowAddProviderDialog()
    {
        var dlg = new Form { Text = "添加供应商", Size = new Size(470, 350), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        Theme.ApplyDialog(dlg);

        int yy = Theme.S(12);
        var quick = Theme.ComboBox();
        quick.Location = new Point(Theme.S(16), yy);
        quick.Size = new Size(Theme.S(420), Theme.S(28));
        quick.Items.AddRange(["-- 快速填入 --", "deepseek (DeepSeek)", "openai (OpenAI)", "anthropic (Anthropic)", "google (Google)", "xai (xAI Grok)", "mistral (Mistral)", "groq (Groq)", "ollama (本地Ollama)", "azure-openai (Azure OpenAI)", "together (Together AI)", "replicate (Replicate)", "huggingface (Hugging Face)"]);
        quick.SelectedIndex = 0;
        dlg.Controls.Add(quick);
        yy += Theme.S(38);

        var idBox = DialogText(dlg, "Provider ID", yy, Theme.S(80)); yy += Theme.S(40);
        var urlBox = DialogText(dlg, "Base URL", yy, Theme.S(80), "https://api.xxx.com/v1"); yy += Theme.S(40);
        var keyBox = DialogText(dlg, "API Key", yy, Theme.S(80), "", true); yy += Theme.S(40);

        var apiLabel = Theme.Lbl("API 类型", Theme.Fc2);
        apiLabel.Location = new Point(Theme.S(16), yy + Theme.S(4));
        dlg.Controls.Add(apiLabel);
        var apiType = Theme.ComboBox();
        apiType.Location = new Point(Theme.S(120), yy);
        apiType.Size = new Size(Theme.S(316), Theme.S(28));
        apiType.Items.AddRange(["openai-completions", "anthropic-messages", "google-generative", "ollama", "azure-openai-completions", "together-completions", "replicate-predictions"]);
        apiType.SelectedIndex = 0;
        dlg.Controls.Add(apiType);
        yy += Theme.S(46);

        quick.SelectedIndexChanged += (_, _) =>
        {
            if (quick.SelectedIndex <= 0) return;
            var p = quick.SelectedItem?.ToString()?.Split('(')[0].Trim() ?? "";
            idBox.Text = p;
            apiType.SelectedIndex = p switch { "anthropic" => 1, "google" => 2, "ollama" => 3, "azure-openai" => 4, "together" => 5, "replicate" => 6, _ => 0 };
            urlBox.Text = p switch
            {
                "deepseek" => "https://api.deepseek.com", "openai" => "https://api.openai.com/v1",
                "anthropic" => "https://api.anthropic.com/v1", "google" => "https://generativelanguage.googleapis.com/v1beta",
                "xai" => "https://api.x.ai/v1", "mistral" => "https://api.mistral.ai/v1",
                "groq" => "https://api.groq.com/openai/v1", "ollama" => "http://localhost:11434/v1",
                "azure-openai" => "https://YOUR_RESOURCE_NAME.openai.azure.com",
                "huggingface" => "https://api-inference.huggingface.co",
                "replicate" => "https://api.replicate.com/v1",
                "together" => "https://api.together.xyz/v1", _ => ""
            };
        };

        var ok = Theme.Btn("保存");
        ok.Location = new Point(Theme.S(340), yy - Theme.S(4));
        ok.Click += (_, _) =>
        {
            var idT = idBox.Text.Trim(); var urlT = urlBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(idT) || string.IsNullOrWhiteSpace(urlT)) return;
            SaveProv(idT, urlT, keyBox.Text.Trim(), apiType.SelectedItem?.ToString() ?? "openai-completions");
            dlg.Close(); RefreshProviders();
        };
        dlg.Controls.Add(ok);
        dlg.ShowDialog(body.FindForm());
    }

    TextBox DialogText(Form dlg, string label, int y, int lblW, string text = "", bool password = false)
    {
        var lb = Theme.Lbl(label, Theme.Fc2);
        lb.Location = new Point(Theme.S(16), y + Theme.S(4));
        dlg.Controls.Add(lb);
        var tb = Theme.TextBox(text, password: password);
        tb.Location = new Point(lblW + Theme.S(24), y);
        tb.Size = new Size(dlg.ClientSize.Width - lblW - Theme.S(50), Theme.S(28));
        dlg.Controls.Add(tb);
        return tb;
    }

    // ═══ 拉取模型 ═══
    void FetchModels(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return;
        var provider = OpenClawRuntime.ReadConfig()?["models"]?["providers"]?[providerId];
        if (provider == null) return;
        var url = provider["baseUrl"]?.ToString() ?? "";
        var key = provider["apiKey"]?.ToString() ?? "";
        var api = provider["api"]?.ToString() ?? "";
        // Ollama 不需要 key
        if (api != "ollama" && string.IsNullOrEmpty(key))
        {
            MessageBox.Show("请先设置 API Key", "提示"); return;
        }

        Task.Run(() =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var ids = new List<string>();

                if (api == "ollama")
                {
                    var json = http.GetStringAsync(url.TrimEnd('/').Replace("/v1", "") + "/api/tags").Result;
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var arr))
                        foreach (var m in arr.EnumerateArray())
                            if (m.TryGetProperty("name", out var nm))
                                ids.Add(nm.GetString() ?? "");
                }
                else
                {
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                    var json = http.GetStringAsync(url.TrimEnd('/') + "/models").Result;
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var arr))
                        foreach (var m in arr.EnumerateArray())
                            if (m.TryGetProperty("id", out var id))
                                ids.Add(id.GetString() ?? "");
                }

                body.Invoke(() => ShowFetchDialog(providerId, ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList()));
            }
            catch (Exception ex) { body.Invoke(() => MessageBox.Show("拉取失败: " + ex.Message, "错误")); }
        });
    }

    void ShowFetchDialog(string providerId, List<string> models)
    {
        var dlg = new Form { Text = "选择导入 - " + providerId, Size = new Size(540, 480), MinimizeBox = false, MaximizeBox = false };
        Theme.ApplyDialog(dlg);

        var clb = new CheckedListBox { Location = new Point(Theme.S(10), Theme.S(10)), Size = new Size(Theme.S(500), Theme.S(370)), CheckOnClick = true };
        foreach (var m in models) clb.Items.Add(m, true);
        dlg.Controls.Add(clb);

        var selAll = Theme.Btn("全选");
        selAll.Location = new Point(Theme.S(10), Theme.S(390));
        selAll.Click += (_, _) => { for (int i = 0; i < clb.Items.Count; i++) clb.SetItemChecked(i, true); };
        dlg.Controls.Add(selAll);

        var selNone = Theme.Btn("全不选");
        selNone.Location = new Point(Theme.S(94), Theme.S(390));
        selNone.Click += (_, _) => { for (int i = 0; i < clb.Items.Count; i++) clb.SetItemChecked(i, false); };
        dlg.Controls.Add(selNone);

        var importBtn = Theme.Btn("导入选中模型");
        importBtn.Location = new Point(Theme.S(350), Theme.S(390));
        importBtn.Click += (_, _) =>
        {
            int count = 0;
            for (int i = 0; i < clb.Items.Count; i++)
            {
                if (!clb.GetItemChecked(i)) continue;
                var mid = clb.Items[i]?.ToString() ?? "";
                var alias = mid.Contains('/') ? mid.Split('/').Last() : mid;
                AddModel(providerId + "/" + mid, alias);
                count++;
            }
            dlg.Close(); RefreshModels();
            MessageBox.Show("已导入 " + count + " 个模型", "完成");
        };
        dlg.Controls.Add(importBtn);
        dlg.ShowDialog(body.FindForm());
    }

    // ═══ 配置读写 ═══
    string? _defaultModel;

    void RefreshProviders()
    {
        provGrid.Rows.Clear();
        try
        {
            var providers = OpenClawRuntime.ReadConfig()?["models"]?["providers"]?.AsObject();
            if (providers == null) return;
            foreach (var p in providers)
                provGrid.Rows.Add(p.Key, p.Value?["baseUrl"]?.ToString() ?? "-", Mask(p.Value?["apiKey"]?.ToString()));
        }
        catch { }
    }

    void RefreshModels()
    {
        modelGrid.Rows.Clear();
        try
        {
            var cfg = OpenClawRuntime.ReadConfig();
            var models = cfg?["agents"]?["defaults"]?["models"]?.AsObject();
            _defaultModel = cfg?["agents"]?["defaults"]?["model"]?["primary"]?.ToString();
            if (models == null) return;
            foreach (var m in models)
            {
                var alias = m.Value?["alias"]?.ToString() ?? "-";
                var prov = m.Key.Contains('/') ? m.Key.Split('/')[0] : "-";
                modelGrid.Rows.Add(m.Key, alias, prov);
            }
        }
        catch { }
    }

    static string Mask(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 6) return "****";
        return key[..3] + "****" + key[^3..];
    }

    void SaveProv(string id, string url, string key, string api)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url)) return;
        var cfg = OpenClawRuntime.ReadConfig() ?? CreateDefaultConfig();
        cfg["models"] ??= new JsonObject();
        var models = cfg["models"]!.AsObject();
        models["providers"] ??= new JsonObject();
        var prov = models["providers"]!.AsObject();
        // 修复：添加 models 字段以确保与 OpenClaw 网关兼容
        prov[id] = new JsonObject { 
            ["baseUrl"] = url, 
            ["api"] = api,
            ["models"] = new JsonArray()  // 添加空的 models 数组
        };
        if (!string.IsNullOrWhiteSpace(key)) prov[id]!["apiKey"] = key;
        OpenClawRuntime.SaveConfig(cfg);
    }

    void DelProvAndModels(string pid)
    {
        if (string.IsNullOrWhiteSpace(pid)) return;
        var cfg = OpenClawRuntime.ReadConfig();
        if (cfg == null) return;
        // 删除供应商本身
        cfg["models"]?["providers"]?.AsObject()?.Remove(pid);
        // 级联删除该供应商下的所有模型
        var models = cfg["agents"]?["defaults"]?["models"]?.AsObject();
        if (models != null)
        {
            var toRemove = models.Where(kv => kv.Key.StartsWith(pid + "/")).Select(kv => kv.Key).ToList();
            foreach (var k in toRemove) models.Remove(k);
        }
        OpenClawRuntime.SaveConfig(cfg);
    }

    void AddModel(string fullId, string alias)
    {
        var cfg = OpenClawRuntime.ReadConfig() ?? CreateDefaultConfig();
        cfg["agents"] ??= new JsonObject();
        var agents = cfg["agents"]!.AsObject();
        agents["defaults"] ??= new JsonObject();
        var defs = agents["defaults"]!.AsObject();
        defs["models"] ??= new JsonObject();
        defs["models"]!.AsObject()[fullId] = new JsonObject { ["alias"] = alias };
        OpenClawRuntime.SaveConfig(cfg);
    }

    void DelModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return;
        var cfg = OpenClawRuntime.ReadConfig();
        if (cfg == null) return;
        cfg["agents"]?["defaults"]?["models"]?.AsObject()?.Remove(modelId);
        OpenClawRuntime.SaveConfig(cfg);
    }

    void SetDefault(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return;
        var cfg = OpenClawRuntime.ReadConfig() ?? CreateDefaultConfig();
        cfg["agents"] ??= new JsonObject();
        var agents = cfg["agents"]!.AsObject();
        agents["defaults"] ??= new JsonObject();
        agents["defaults"]!.AsObject()["model"] = new JsonObject { ["primary"] = modelId };
        OpenClawRuntime.SaveConfig(cfg);
        _defaultModel = modelId;
        RefreshModels();
    }

    static JsonObject CreateDefaultConfig() => new()
    {
        ["models"] = new JsonObject { ["providers"] = new JsonObject() },
        ["agents"] = new JsonObject { ["defaults"] = new JsonObject { ["model"] = new JsonObject { ["primary"] = "" }, ["models"] = new JsonObject() } },
        ["gateway"] = new JsonObject()
    };
}
