using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenClawManager;

class SearchDialog : Form
{
    readonly string query;
    readonly List<SearchResult> results = new();
    DataGridView grid = null!;
    Label statusBar = null!;
    ProgressBar progressBar = null!;
    Button cancelBtn = null!;
    CancellationTokenSource? cts;

    class SearchResult
    {
        public string Slug = "";
        public string Name = "";
        public string Desc = "";
        public string Status = "未安装";
        public string ActionText = "安装";
        public bool Installed;
    }

    public SearchDialog(string query)
    {
        this.query = query;
        Text = string.IsNullOrWhiteSpace(query) ? "ClawHub 技能市场" : $"“{query}” - ClawHub 技能搜索";
        Size = new Size(760, 500);
        MinimumSize = new Size(620, 420);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Theme.ApplyDialog(this);

        BuildUi();
        Load += async (_, _) => await SearchAsync();
        Resize += (_, _) => LayoutUi();
    }

    void BuildUi()
    {
        var pad = Theme.S(14);
        var titleBar = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientSize.Width, Theme.S(48)),
            BackColor = Theme.BgElevated
        };
        titleBar.Controls.Add(new Label
        {
            Text = Text,
            ForeColor = Theme.Fc,
            Font = Theme.Font(11f, FontStyle.Bold),
            Location = new Point(pad, Theme.S(14)),
            AutoSize = true,
            BackColor = Color.Transparent
        });
        Controls.Add(titleBar);

        grid = Theme.Grid();
        grid.ReadOnly = false;
        grid.Location = new Point(pad, Theme.S(58));
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "技能", FillWeight = 24, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "desc", HeaderText = "说明", FillWeight = 42, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "状态", FillWeight = 14, ReadOnly = true });
        grid.Columns.Add(new DataGridViewButtonColumn { Name = "action", HeaderText = "操作", FillWeight = 12, UseColumnTextForButtonValue = false, FlatStyle = FlatStyle.Flat });
        grid.Columns.Add(new DataGridViewButtonColumn { Name = "remove", HeaderText = "", FillWeight = 8, UseColumnTextForButtonValue = true, Text = "移除", FlatStyle = FlatStyle.Flat });
        grid.CellFormatting += GridCellFormatting;
        grid.CellContentClick += GridCellContentClick;
        Controls.Add(grid);

        statusBar = new Label
        {
            Text = "搜索中...",
            ForeColor = Theme.Fc2,
            Font = Theme.Font(9f),
            AutoSize = false,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(statusBar);

        progressBar = new ProgressBar { Style = ProgressBarStyle.Marquee, Visible = true };
        Controls.Add(progressBar);

        cancelBtn = Theme.BtnDanger("取消");
        cancelBtn.AutoSize = false;
        cancelBtn.Visible = true;
        cancelBtn.Click += (_, _) =>
        {
            cts?.Cancel();
            SetDone("已取消");
        };
        Controls.Add(cancelBtn);

        LayoutUi();
    }

    void LayoutUi()
    {
        var pad = Theme.S(14);
        if (Controls.Count > 0 && Controls[0] is Panel titleBar)
            titleBar.Width = ClientSize.Width;

        var footerH = Theme.S(42);
        grid.Location = new Point(pad, Theme.S(58));
        grid.Size = new Size(ClientSize.Width - pad * 2, ClientSize.Height - Theme.S(58) - footerH);
        statusBar.Location = new Point(pad, ClientSize.Height - Theme.S(31));
        statusBar.Size = new Size(ClientSize.Width - Theme.S(190), Theme.S(22));
        progressBar.Size = new Size(Theme.S(96), Theme.S(14));
        progressBar.Location = new Point(ClientSize.Width - Theme.S(164), ClientSize.Height - Theme.S(27));
        cancelBtn.Size = new Size(Theme.S(58), Theme.S(24));
        cancelBtn.Location = new Point(ClientSize.Width - Theme.S(66), ClientSize.Height - Theme.S(32));
    }

    void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (grid.Columns[e.ColumnIndex].Name == "status")
        {
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var value = cell.Value?.ToString() ?? "";
            cell.Style.ForeColor = value.Contains("已安装") ? Theme.Grn : value.Contains("失败") ? Theme.Red : Theme.Fc2;
        }

        if (grid.Columns[e.ColumnIndex].Name == "action" && grid.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewButtonCell btn)
        {
            var action = btn.Value?.ToString() ?? "";
            btn.FlatStyle = FlatStyle.Flat;
            btn.Style.BackColor = action switch
            {
                "安装" => Theme.Acc,
                "安装中..." => Theme.BgElevated,
                "已安装" => Theme.Grn,
                _ => Theme.BgElevated
            };
            btn.Style.ForeColor = action == "安装中..." ? Theme.Fc2 : Theme.FcWhite;
        }
    }

    async void GridCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= results.Count) return;
        var result = results[e.RowIndex];
        var columnName = grid.Columns[e.ColumnIndex].Name;
        if (columnName == "action" && result.ActionText == "安装")
            await InstallSkillAsync(result, grid.Rows[e.RowIndex]);
        else if (columnName == "remove")
        {
            results.RemoveAt(e.RowIndex);
            grid.Rows.RemoveAt(e.RowIndex);
            statusBar.Text = $"剩余 {results.Count} 项";
        }
    }

    async Task SearchAsync()
    {
        cts = new CancellationTokenSource();
        var output = await OpenClawRuntime.RunOpenClawAsync($"skills search \"{query}\" --json", 15000, cts.Token);
        if (cts.IsCancellationRequested) return;

        if (output.code == 0 && !string.IsNullOrWhiteSpace(output.stdout))
        {
            try
            {
                var root = JsonNode.Parse(output.stdout);
                var list = root?["results"]?.AsArray();
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        var slug = item?["slug"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(slug)) continue;
                        var name = item?["displayName"]?.ToString() ?? slug;
                        var desc = OpenClawRuntime.Trim(item?["summary"]?.ToString() ?? "", 120);
                        var result = new SearchResult { Slug = slug, Name = name, Desc = desc };
                        results.Add(result);
                        grid.Rows.Add(result.Name, result.Desc, result.Status, result.ActionText, "移除");
                    }
                }
                SetDone(results.Count > 0 ? $"找到 {results.Count} 个技能" : "没有找到结果");
            }
            catch (Exception ex) { SetDone("解析失败: " + ex.Message); }
        }
        else
        {
            SetDone("搜索失败: " + OpenClawRuntime.LastLine(output.stderr + output.stdout));
        }
    }

    async Task InstallSkillAsync(SearchResult result, DataGridViewRow row)
    {
        result.ActionText = "安装中...";
        row.Cells["action"].Value = result.ActionText;
        row.Cells["status"].Value = "...";
        grid.InvalidateRow(row.Index);
        SetBusy($"正在安装 {result.Name}...");

        cts = new CancellationTokenSource();
        var output = await OpenClawRuntime.RunOpenClawAsync($"skills install {result.Slug}", 120000, cts.Token);
        if (cts.IsCancellationRequested)
        {
            result.ActionText = "安装";
            row.Cells["action"].Value = result.ActionText;
            row.Cells["status"].Value = "已取消";
            SetDone("已取消");
            return;
        }

        if (output.code == 0)
        {
            result.Installed = true;
            result.Status = "已安装";
            result.ActionText = "已安装";
            row.Cells["status"].Value = result.Status;
            row.Cells["action"].Value = result.ActionText;
            SetDone($"{result.Name} 安装完成");
        }
        else
        {
            result.Status = "安装失败";
            result.ActionText = "安装";
            row.Cells["status"].Value = result.Status;
            row.Cells["action"].Value = result.ActionText;
            SetDone("安装失败: " + OpenClawRuntime.LastLine(output.stderr + output.stdout));
        }
        grid.InvalidateRow(row.Index);
    }

    void SetBusy(string text)
    {
        statusBar.Text = text;
        statusBar.ForeColor = Theme.Acc;
        progressBar.Visible = true;
        cancelBtn.Visible = true;
    }

    void SetDone(string text)
    {
        statusBar.Text = text;
        statusBar.ForeColor = Theme.Fc2;
        progressBar.Visible = false;
        cancelBtn.Visible = false;
    }

}
