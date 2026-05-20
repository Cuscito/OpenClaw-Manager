using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenClawManager;

public static class Theme
{
    public static float ScaleFactor { get; set; } = 1.0f;

    public static void InitDpi(Control c)
    {
        using var g = c.CreateGraphics();
        ScaleFactor = Math.Max(1.0f, g.DpiX / 96f);
    }

    public static bool IsDark { get; set; }

    public static void Toggle()
    {
        IsDark = !IsDark;
        try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.dat"), IsDark ? "dark" : "light"); } catch { }
    }

    public static void LoadTheme()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.dat");
            if (File.Exists(path)) IsDark = File.ReadAllText(path).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
    }

    public static readonly Color QqBlue = Color.FromArgb(18, 183, 245);
    public static readonly Color QqGreen = Color.FromArgb(78, 201, 102);
    public static readonly Color QqRed = Color.FromArgb(255, 89, 89);
    public static readonly Color QqOrange = Color.FromArgb(255, 165, 0);

    public static Color Bg => IsDark ? Color.FromArgb(18, 22, 33) : Color.FromArgb(246, 248, 251);
    public static Color BgWhite => IsDark ? Color.FromArgb(30, 36, 52) : Color.FromArgb(255, 255, 255);
    public static Color BgHover => IsDark ? Color.FromArgb(42, 51, 72) : Color.FromArgb(235, 242, 252);
    public static Color BgSelected => IsDark ? Color.FromArgb(38, 63, 98) : Color.FromArgb(224, 239, 255);
    public static Color BgSidebar => IsDark ? Color.FromArgb(22, 27, 41) : Color.FromArgb(250, 252, 255);
    public static Color BgCard => IsDark ? Color.FromArgb(30, 36, 52) : Color.FromArgb(255, 255, 255);
    public static Color BgElevated => IsDark ? Color.FromArgb(36, 44, 63) : Color.FromArgb(250, 252, 255);

    public static Color Bdr => IsDark ? Color.FromArgb(58, 69, 94) : Color.FromArgb(218, 226, 237);
    public static Color BdrLight => IsDark ? Color.FromArgb(43, 53, 74) : Color.FromArgb(232, 238, 247);

    public static Color Fc => IsDark ? Color.FromArgb(232, 237, 247) : Color.FromArgb(30, 41, 59);
    public static Color Fc2 => IsDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
    public static Color FcMuted => IsDark ? Color.FromArgb(112, 126, 148) : Color.FromArgb(148, 163, 184);
    public static Color FcWhite => Color.White;

    public static Color Acc => IsDark ? Color.FromArgb(96, 165, 250) : Color.FromArgb(37, 99, 235);
    public static Color Acc2 => IsDark ? Color.FromArgb(45, 212, 191) : Color.FromArgb(13, 148, 136);
    public static Color Grn => IsDark ? Color.FromArgb(74, 222, 128) : Color.FromArgb(22, 163, 74);
    public static Color Red => IsDark ? Color.FromArgb(248, 113, 113) : Color.FromArgb(220, 38, 38);
    public static Color Warn => IsDark ? Color.FromArgb(251, 191, 36) : Color.FromArgb(217, 119, 6);

    public const string FontFamily = "Microsoft YaHei UI";
    public const string MonoFont = "Consolas";
    public const int SidebarW = 216;
    public const int TitleH = 56;

    public static Font Font(float size = 9f, FontStyle style = FontStyle.Regular)
        => new(FontFamily, size * ScaleFactor, style);

    public static Font Mono(float size = 9f) => new(MonoFont, size * ScaleFactor);

    public static int S(int value) => Math.Max(1, (int)Math.Round(value * ScaleFactor));

    public static Label Lbl(string text, Color? c = null, float size = 9f, FontStyle style = FontStyle.Regular)
        => new()
        {
            Text = text,
            ForeColor = c ?? Fc,
            Font = Font(size, style),
            AutoSize = true,
            BackColor = Color.Transparent
        };

    public static Button Btn(string text) => StyledButton(text, Acc, FcWhite, 0);

    public static Button BtnWhite(string text) => StyledButton(text, BgWhite, Fc, 1);

    public static Button BtnDanger(string text) => StyledButton(text, Red, FcWhite, 0);

    public static Button BtnSuccess(string text) => StyledButton(text, Grn, FcWhite, 0);

    public static Button StyledButton(string text, Color bg, Color fg, int border)
        => new()
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = fg,
            Font = Font(9f, FontStyle.Bold),
            FlatAppearance = { BorderSize = border, BorderColor = Bdr },
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Height = S(32),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(S(14), 0, S(14), 0)
        };

    public static TextBox TextBox(string text = "", string placeholder = "", bool password = false)
        => new()
        {
            Text = text,
            PlaceholderText = placeholder,
            UseSystemPasswordChar = password,
            BackColor = BgWhite,
            ForeColor = Fc,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Font(9.5f),
            Height = S(28)
        };

    public static ComboBox ComboBox()
        => new()
        {
            BackColor = BgWhite,
            ForeColor = Fc,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = Font(9.5f),
            Height = S(28)
        };

    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            BackgroundColor = BgWhite,
            ForeColor = Fc,
            GridColor = BdrLight,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowTemplate = { Height = S(34), MinimumHeight = S(34) },
            ColumnHeadersHeight = S(34),
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BgElevated,
                ForeColor = Fc2,
                Font = Font(9f, FontStyle.Bold),
                SelectionBackColor = BgElevated,
                SelectionForeColor = Fc,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(S(8), 0, 0, 0)
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BgWhite,
                ForeColor = Fc,
                Font = Font(9.5f),
                SelectionBackColor = BgSelected,
                SelectionForeColor = Fc
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = IsDark ? Color.FromArgb(27, 33, 48) : Color.FromArgb(249, 251, 254),
                ForeColor = Fc
            }
        };
        return grid;
    }

    public static Panel Card(int x, int y, int w, int h, string? title = null)
    {
        var panel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = BgCard
        };
        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BdrLight);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        if (!string.IsNullOrWhiteSpace(title))
        {
            panel.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Fc,
                Font = Font(10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(S(14), S(10)),
                BackColor = Color.Transparent
            });
        }
        return panel;
    }

    public static void ApplyDialog(Form form)
    {
        form.BackColor = BgWhite;
        form.ForeColor = Fc;
        form.Font = Font(9f);
        form.StartPosition = FormStartPosition.CenterParent;
        ApplyTo(form);
    }

    public static void ApplyTo(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case DataGridView grid:
                    ApplyGrid(grid);
                    break;
                case TextBox tb:
                    tb.BackColor = BgWhite;
                    tb.ForeColor = Fc;
                    tb.Font = Font(9.5f);
                    break;
                case ComboBox cb:
                    cb.BackColor = BgWhite;
                    cb.ForeColor = Fc;
                    cb.Font = Font(9.5f);
                    break;
                case CheckedListBox clb:
                    clb.BackColor = BgWhite;
                    clb.ForeColor = Fc;
                    clb.Font = Font(9.5f);
                    break;
                case RichTextBox rtb:
                    if (rtb.BackColor.ToArgb() != Color.FromArgb(30, 30, 30).ToArgb())
                    {
                        rtb.BackColor = BgWhite;
                        rtb.ForeColor = Fc;
                    }
                    break;
                case Button btn:
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.UseVisualStyleBackColor = false;
                    btn.Font = btn.Font?.FontFamily.Name == FontFamily ? btn.Font : Font(9f, FontStyle.Bold);
                    if (btn.BackColor == Color.Gray || btn.BackColor == SystemColors.Control)
                    {
                        btn.BackColor = BgElevated;
                        btn.ForeColor = Fc;
                        btn.FlatAppearance.BorderSize = 1;
                        btn.FlatAppearance.BorderColor = Bdr;
                    }
                    else if (btn.BackColor == Color.Transparent)
                    {
                        btn.ForeColor = Fc;
                    }
                    break;
                case Label label:
                    if (label.ForeColor == Color.Black || label.ForeColor == SystemColors.ControlText || IsThemeTextColor(label.ForeColor))
                        label.ForeColor = Fc;
                    break;
                case FlowLayoutPanel flow:
                    if (flow.BackColor == Color.White || flow.BackColor == SystemColors.Control)
                        flow.BackColor = BgCard;
                    break;
                case Panel panel:
                    if (panel.BackColor == Color.White || panel.BackColor == SystemColors.Control)
                        panel.BackColor = BgCard;
                    break;
            }

            if (control.HasChildren)
                ApplyTo(control);
        }
    }

    static bool IsThemeTextColor(Color color)
    {
        var candidates = new[]
        {
            Color.FromArgb(232, 237, 247),
            Color.FromArgb(30, 41, 59),
            Color.FromArgb(148, 163, 184),
            Color.FromArgb(100, 116, 139),
            Color.FromArgb(112, 126, 148)
        };
        return candidates.Any(c => c.ToArgb() == color.ToArgb());
    }

    public static void ApplyGrid(DataGridView grid)
    {
        grid.BackgroundColor = BgWhite;
        grid.ForeColor = Fc;
        grid.GridColor = BdrLight;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = BgElevated;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Fc2;
        grid.ColumnHeadersDefaultCellStyle.Font = Font(9f, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = BgWhite;
        grid.DefaultCellStyle.ForeColor = Fc;
        grid.DefaultCellStyle.Font = Font(9.5f);
        grid.DefaultCellStyle.SelectionBackColor = BgSelected;
        grid.DefaultCellStyle.SelectionForeColor = Fc;
        grid.AlternatingRowsDefaultCellStyle.BackColor = IsDark ? Color.FromArgb(27, 33, 48) : Color.FromArgb(249, 251, 254);
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Fc;
    }
}
