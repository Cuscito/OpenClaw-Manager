using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OpenClawManager;

public class LauncherPage
{
    Panel body = null!;

    public void Build(Panel p)
    {
        body = p;
        body.Controls.Clear();
        body.BackColor = Theme.Bg;

        var w = body.ClientSize.Width;
        var h = body.ClientSize.Height;
        var cardW = Math.Min(360, Math.Max(280, (w - 80) / 2));
        var cardH = 330;
        var totalW = cardW * 2 + 28;
        var stacked = w < 760;
        if (stacked)
        {
            cardW = Math.Min(420, w - 48);
            cardH = 250;
            totalW = cardW;
        }

        var startX = Math.Max(24, (w - totalW) / 2);
        var startY = Math.Max(28, (h - (stacked ? cardH * 2 + 24 : cardH)) / 2 - 32);

        body.Controls.Add(new Label
        {
            Text = "OpenClaw 管理器",
            Font = Theme.Font(24f, FontStyle.Bold),
            ForeColor = Theme.Fc,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(startX, startY - 74)
        });

        body.Controls.Add(new Label
        {
            Text = "选择启动方式，随后完成网关、模型和工作区配置。",
            Font = Theme.Font(10.5f),
            ForeColor = Theme.Fc2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(startX, startY - 36)
        });

        bool hasRuntime = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node.exe"));
        var localCard = BuildCard(
            "本地运行",
            "使用随程序携带的运行环境",
            "无需下载 Node.js，数据留在当前目录，适合 U 盘或离线部署。",
            hasRuntime ? "开始配置" : "仍可配置",
            startX, startY, cardW, cardH,
            Theme.Acc, new[] { "启动更快", "数据本地保存", "环境隔离" },
            !hasRuntime ? "未检测到 runtime/node.exe，启动前请补齐运行环境。" : null);
        WireCard(localCard, () => new SetupPage().Build(body, local: true));
        body.Controls.Add(localCard);

        var onlineX = stacked ? startX : startX + cardW + 28;
        var onlineY = stacked ? startY + cardH + 24 : startY;
        var onlineCard = BuildCard(
            "在线安装",
            "联网下载 Node.js 与 OpenClaw",
            "适合首次部署，会自动准备依赖并写入默认配置。",
            "在线安装",
            onlineX, onlineY, cardW, cardH,
            Theme.Acc2, new[] { "自动部署", "版本更新", "依赖完整" });
        WireCard(onlineCard, () => new SetupPage().Build(body, local: false));
        body.Controls.Add(onlineCard);

    }

    Panel BuildCard(string title, string tagline, string desc, string action, int x, int y, int w, int h, Color accent, string[] features, string? warning = null)
    {
        var card = Theme.Card(x, y, w, h);
        card.Cursor = Cursors.Hand;
        card.Padding = new Padding(Theme.S(18));

        card.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, card.Width, Theme.S(4));
        };

        card.Controls.Add(new Label
        {
            Text = title,
            ForeColor = Theme.Fc,
            Font = Theme.Font(16f, FontStyle.Bold),
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(Theme.S(22), Theme.S(26))
        });

        card.Controls.Add(new Label
        {
            Text = tagline,
            ForeColor = accent,
            Font = Theme.Font(10f, FontStyle.Bold),
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(Theme.S(22), Theme.S(64))
        });

        card.Controls.Add(new Label
        {
            Text = desc,
            ForeColor = Theme.Fc2,
            Font = Theme.Font(9.5f),
            AutoSize = false,
            Size = new Size(w - Theme.S(44), Theme.S(54)),
            BackColor = Color.Transparent,
            Location = new Point(Theme.S(22), Theme.S(94))
        });

        var actionBtn = Theme.StyledButton(action, accent, Theme.FcWhite, 0);
        actionBtn.Location = new Point(Theme.S(22), Theme.S(160));
        card.Controls.Add(actionBtn);

        var fy = Theme.S(214);
        foreach (var feature in features)
        {
            card.Controls.Add(new Label
            {
                Text = "✓ " + feature,
                ForeColor = Theme.Fc,
                Font = Theme.Font(9.5f),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(Theme.S(24), fy)
            });
            fy += Theme.S(25);
        }

        if (!string.IsNullOrWhiteSpace(warning))
        {
            card.Controls.Add(new Label
            {
                Text = warning,
                ForeColor = Theme.Warn,
                Font = Theme.Font(8.5f),
                AutoSize = false,
                Size = new Size(w - Theme.S(44), Theme.S(36)),
                BackColor = Color.Transparent,
                Location = new Point(Theme.S(22), h - Theme.S(48))
            });
        }

        return card;
    }

    void WireCard(Panel card, Action action)
    {
        card.Click += (_, _) => action();
        card.MouseEnter += (_, _) => card.BackColor = Theme.BgElevated;
        card.MouseLeave += (_, _) => card.BackColor = Theme.BgCard;
        foreach (Control control in card.Controls)
        {
            control.Cursor = Cursors.Hand;
            control.Click += (_, _) => action();
        }
    }
}
