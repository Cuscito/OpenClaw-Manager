using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodeGenGui;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    TextBox emailBox, machineBox, codeBox, keyBox;
    DateTimePicker expiryPicker;
    ComboBox bindingTypeCb;
    Button genBtn, copyBtn, saveKeyBtn, detectDeviceBtn;
    Label codeLabel, infoLabel;
    string? _lastCode;

    public MainForm()
    {
        Text = "注册码生成器";
        Size = new Size(620, 560);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 10f);

        int y = 16, pad = 16;

        // 标题
        var title = new Label { Text = "🔑 OpenClaw 管理器 — 注册码生成", Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(18, 183, 245), Location = new Point(pad, y), AutoSize = true };
        Controls.Add(title);
        y += 40;

        // 邮箱
        Controls.Add(Lbl("用户邮箱:", pad, y + 6));
        emailBox = new TextBox { Location = new Point(130, y + 2), Size = new Size(300, 28), PlaceholderText = "user@example.com", BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(emailBox);
        y += 40;

        // 到期日
        Controls.Add(Lbl("到期日期:", pad, y + 4));
        expiryPicker = new DateTimePicker { Location = new Point(130, y + 2), Size = new Size(200, 28), Value = DateTime.Now.AddYears(1), Format = DateTimePickerFormat.Short, MinDate = DateTime.Now };
        Controls.Add(expiryPicker);
        y += 40;

        // 绑定类型 + 设备码
        Controls.Add(Lbl("绑定类型:", pad, y + 6));
        bindingTypeCb = new ComboBox { Location = new Point(130, y + 2), Size = new Size(120, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 9f) };
        bindingTypeCb.Items.AddRange(["电脑绑定", "U盘绑定"]);
        bindingTypeCb.SelectedIndex = 0;
        bindingTypeCb.SelectedIndexChanged += (_, _) => UpdateDevicePlaceholder();
        Controls.Add(bindingTypeCb);

        detectDeviceBtn = new Button { Text = "🔍 检测", Location = new Point(256, y + 2), Size = new Size(70, 28), BackColor = Color.FromArgb(100, 100, 100), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Microsoft YaHei UI", 9f) };
        detectDeviceBtn.FlatAppearance.BorderSize = 0;
        detectDeviceBtn.Click += DetectDeviceBtn_Click;
        Controls.Add(detectDeviceBtn);
        y += 34;

        machineBox = new TextBox { Location = new Point(pad, y), Size = new Size(584, 28), PlaceholderText = "留空不绑定，填入后注册码仅当前设备可用", BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(machineBox);
        y += 40;

        // 生成按钮
        genBtn = new Button { Text = "▶ 生成注册码", Location = new Point(130, y), Size = new Size(140, 36), BackColor = Color.FromArgb(18, 183, 245), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold) };
        genBtn.FlatAppearance.BorderSize = 0;
        genBtn.Click += GenBtn_Click;
        Controls.Add(genBtn);
        y += 52;

        // 注册码输出
        codeLabel = new Label { Text = "注册码:", Location = new Point(pad, y), AutoSize = true, ForeColor = Color.FromArgb(100, 100, 100) };
        Controls.Add(codeLabel);
        y += 24;
        codeBox = new TextBox { Location = new Point(pad, y), Size = new Size(584, 28), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(245, 245, 245), Font = new Font("Consolas", 9f), PlaceholderText = "点击「生成注册码」后显示在此处" };
        Controls.Add(codeBox);
        y += 36;

        // 复制按钮
        copyBtn = new Button { Text = "📋 复制注册码", Location = new Point(pad, y), Size = new Size(140, 30), BackColor = Color.FromArgb(76, 175, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Enabled = false };
        copyBtn.FlatAppearance.BorderSize = 0;
        copyBtn.Click += (_, _) => { if (!string.IsNullOrEmpty(_lastCode)) { Clipboard.SetText(_lastCode); copyBtn.Text = "✓ 已复制"; Task.Run(async () => { await Task.Delay(2000); copyBtn.Invoke(() => copyBtn.Text = "📋 复制注册码"); }); } };
        Controls.Add(copyBtn);

        infoLabel = new Label { Location = new Point(160, y + 4), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 9f) };
        Controls.Add(infoLabel);
        y += 44;

        // 密钥管理
        var keyLabel = new Label { Text = "密钥状态:", Location = new Point(pad, y), AutoSize = true, ForeColor = Color.FromArgb(100, 100, 100), Font = new Font("Microsoft YaHei UI", 9f) };
        Controls.Add(keyLabel);
        y += 24;
        keyBox = new TextBox { Location = new Point(pad, y), Size = new Size(584, 28), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(245, 245, 245), Font = new Font("Microsoft YaHei UI", 9f), Text = "✅ 密钥已内置 — 与 App 公钥匹配", ForeColor = Color.FromArgb(76, 175, 80) };
        Controls.Add(keyBox);
        y += 36;

        saveKeyBtn = new Button { Text = "💾 导出私钥（备份）", Location = new Point(pad, y), Size = new Size(150, 30), BackColor = Color.FromArgb(244, 67, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        saveKeyBtn.FlatAppearance.BorderSize = 0;
        saveKeyBtn.Click += (_, _) => {
            using var dlg = new SaveFileDialog { FileName = "private_key.xml", Filter = "XML 文件|*.xml", Title = "导出私钥备份" };
            if (dlg.ShowDialog() == DialogResult.OK) {
                File.WriteAllText(dlg.FileName, HardcodedPrivateKey, Encoding.UTF8);
                MessageBox.Show("私钥已导出至:\n" + dlg.FileName + "\n\n⚠ 请妥善保管，勿泄露！", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        Controls.Add(saveKeyBtn);
    }

    static readonly string HardcodedPrivateKey = "<RSAKeyValue><Modulus>3R7v5PUVahY4HBlJF6b/1nkccaHo3+VyuaBQbtuQU5GjoEDIFLq0nwky0VytjYFOqV0rgBoFC+xv+L2cjzptPpio/W6WTFDoG37KtUAPYEJUBirhUtkdCKu/C0TPtKEaBySaJIgcPMLWmd7tC7YJoLwpi4Wqey5oL+BbaA6PiI3o2EqRy30sVe5ilTvb7htieN70dxuNOr0JFXVBd8Yv/lFPt50hq+EnY44DytAJG+jDARVrgkl4ntXDYXmqv3QhdTpu09xCmlES1rvF7jTRXQfDolCl9m8i2iFANsZK6vILHW/+7/iWnf+NuWuQsSOfjV82zl0/38+jloqvawkFdQ==</Modulus><Exponent>AQAB</Exponent><P>99eZqp9KNLoSjSSuYdTOMZPl786K/L5/RcYLW8ASaliM9yJxDZt+KegsotxYyfPiGZwhGIYByfZ1Ad5m+4KzFJuXUQ41JlAjK2qhEXs2wP1FkGQS1V1SCBu+3IanbymqqFHiYX8hqm3j9/4vCfJjf5BZRbSFce4nSts1SLZ8AT8=</P><Q>5GYsjTbSHNpVQ7ZbL5LrChP6UcSMcFaYio/vqy6P7n+G3DRxD6U1d6/8XuMHpM2oDtRG1Cr9lM7GgobzEfKaQzzTdNtMhL23y68DoeLuT7e46RTRNUp+/vU9h6YRoeYykw8NCoWNi/UWZ+GZiqWR7RFJfnQOUTsP1TBTcGL5WEs=</Q><DP>ZWNDr7L+Lle4YxkQZWEjANEaDWIXJZHgivCbkOsgHXUgJbFnQkPL9uTN7cnqYKNuaT+fomKftLkn4J3UzysGi3WjDzuarpO173rtbTNUkNqLbKgjtDk656pCCADl2enXsIfe7jeN1CSkT70iDRWlcnfgrU1OEe91D4Bhad+NFw0=</DP><DQ>ZA//ZL6nvsxInqm9uVH8dyXZfQlHHdBSdCIrNivoEuz8AG1ZOyl+Czmmr0t3hdQz1ItbnIhZIGCWx4in5S0MOHjli0SoeV9Ero+2X640CejLshHUtiw1By4aDtvKzcs0TQlDBWENakutzUUhJ4TyMZJpSGVAAIaHoTbS12IFK5E=</DQ><InverseQ>JOY87tL6+uHmEHJHwj0S/+VviqDiYSq55l/Z9C4EVMFmJp1CHUE+G3zRhCjWL/lRpaFbXNdzKg40GJ9PmiQQkfprqdLbPB6Uz7sTlDmgdxa6TkIo56yQZC/DDMgv2zS+uiYQOWt9FzR83CLQ424SFyKSaVNObZT6NJ5ZfBuaIns=</InverseQ><D>YXKK4mon/9K+85MpJSVRxmElaeaizFlAEBJYoCJfHpUmeS9Tfd8yTowtOxsO2TfNRNJso03UP4b5abOWVYC4OEfq4ZVk40kcoVFlYC9VF2TjxrwLBQntw658ySiQKNh+sfste0AIwbZUAQe/i9OztoR2AqjrktHG1KKZZehU1SZzP31ob705auQ1QMt9EHAdDynQCLDQwgZoVeo19c43uQCL2c1RizJKrnJbTlLmwUDD+VKcevufHMjfTRlZph4l59eUZ4N5GkvKubB7W8W1b9h4OH1GjIUdVa3K0xs1DhnD2x0Gom553EjSBgHP/ADiLAWmuO1s6sOX5SD+PsFHmQ==</D></RSAKeyValue>";

    void GenBtn_Click(object? sender, EventArgs e)
    {
        var email = emailBox.Text.Trim();
        if (string.IsNullOrEmpty(email))
        {
            MessageBox.Show("请输入用户邮箱", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        genBtn.Enabled = false;
        genBtn.Text = "生成中...";

        try
        {
            // 使用硬编码密钥对（与 App 内嵌公钥匹配）
            using var rsa = RSA.Create();
            rsa.FromXmlString(HardcodedPrivateKey);

            var machineHash = machineBox.Text.Trim();
            var expiry = expiryPicker.Value.Date;

            var bindingType = bindingTypeCb.SelectedIndex == 1 ? "usb" : "machine";

            var license = new LicenseData
            {
                Email = email,
                MachineHash = machineHash,
                Level = "pro",
                Expiry = expiry,
                Issued = DateTime.Now.Date,
                BindingType = bindingType
            };

            var json = JsonSerializer.Serialize(license);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var jsonB64 = Convert.ToBase64String(jsonBytes);

            var signature = rsa.SignData(jsonBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var sigB64 = Convert.ToBase64String(signature);

            var raw = jsonB64 + ":" + sigB64;
            var rawBytes = Encoding.UTF8.GetBytes(raw);
            var rawB64 = Convert.ToBase64String(rawBytes);

            var sb = new StringBuilder("OCM-");
            for (int i = 0; i < rawB64.Length; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append('-');
                sb.Append(rawB64[i]);
            }
            _lastCode = sb.ToString();

            codeBox.Text = _lastCode;
            copyBtn.Enabled = true;
            var bindingLabel = bindingType == "usb" ? "U盘" : "电脑";
            infoLabel.Text = $"邮箱: {email}  |  到期: {expiry:yyyy-MM-dd}  |  {(string.IsNullOrEmpty(machineHash) ? "未绑定" : $"已绑定{bindingLabel}")}";
            infoLabel.ForeColor = Color.FromArgb(76, 175, 80);
        }
        catch (Exception ex)
        {
            MessageBox.Show("生成失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            genBtn.Enabled = true;
            genBtn.Text = "▶ 生成注册码";
        }
    }

    Label Lbl(string text, int x, int y) => new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };

    void UpdateDevicePlaceholder()
    {
        machineBox.PlaceholderText = bindingTypeCb.SelectedIndex == 1
            ? "留空不绑定，填入U盘设备码后注册码仅该U盘可用（点「检测」自动获取）"
            : "留空不绑定，填入电脑设备码后注册码仅该电脑可用（点「检测」自动获取）";
    }

    void DetectDeviceBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            if (bindingTypeCb.SelectedIndex == 1)
            {
                // U盘: 弹出驱动器选择
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType != DriveType.CDRom)
                    .Select(d => $"{d.Name.TrimEnd('\\')} [{d.VolumeLabel}] ({d.DriveType})")
                    .ToArray();

                if (drives.Length == 0)
                {
                    MessageBox.Show("未检测到可用驱动器", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 构建选择列表
                var driveList = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType != DriveType.CDRom)
                    .ToArray();

                using var picker = new Form
                {
                    Text = "选择U盘驱动器",
                    Size = new Size(400, 280),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    Font = new Font("Microsoft YaHei UI", 10f)
                };
                var listBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Microsoft YaHei UI", 10f)
                };
                foreach (var d in driveList)
                    listBox.Items.Add($"{d.Name.TrimEnd('\\')}  [{d.VolumeLabel}]  ({FormatSize(d.TotalSize)})  {(d.DriveType == DriveType.Removable ? "🔌可移动" : "💾固定")}");
                listBox.SelectedIndex = 0;
                picker.Controls.Add(listBox);

                var okBtn = new Button
                {
                    Text = "选择此驱动器",
                    Dock = DockStyle.Bottom,
                    Height = 36,
                    BackColor = Color.FromArgb(18, 183, 245),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                okBtn.FlatAppearance.BorderSize = 0;
                okBtn.Click += (_, _) =>
                {
                    if (listBox.SelectedIndex >= 0 && listBox.SelectedIndex < driveList.Length)
                    {
                        var di = driveList[listBox.SelectedIndex];
                        machineBox.Text = ComputeUsbHash(di);
                    }
                    picker.Close();
                };
                picker.Controls.Add(okBtn);
                picker.ShowDialog(this);
            }
            else
            {
                // 电脑: 读 MachineGuid
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                    var guid = key?.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrEmpty(guid))
                        machineBox.Text = HashStr(guid);
                    else
                        machineBox.Text = HashStr(Environment.MachineName + "_" + Environment.ProcessorCount);
                }
                catch
                {
                    machineBox.Text = HashStr(Environment.MachineName + "_" + Environment.ProcessorCount);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("检测失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // 计算U盘设备码 — 与 LicenseValidator.GetUsbDeviceId 逻辑一致
    static string ComputeUsbHash(DriveInfo di)
    {
        try
        {
            var root = di.RootDirectory.FullName;
            var sb = new StringBuilder(256);
            if (GetVolumeInformation(root, sb, sb.Capacity, out uint serial,
                out _, out _, null, 0))
            {
                var label = sb.ToString().Trim();
                return HashStr($"usb:{serial:X8}:{label}");
            }
        }
        catch { }
        // 回退
        var id = $"{di.DriveType}:{di.TotalSize}:{di.VolumeLabel}:{di.DriveFormat}";
        return HashStr(id);
    }

    static string HashStr(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {sizes[order]}";
    }

    // Win32: 取卷序列号
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern bool GetVolumeInformation(
        string lpRootPathName,
        StringBuilder lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder? lpFileSystemNameBuffer,
        int nFileSystemNameSize);
}

class LicenseData
{
    public string Email { get; set; } = "";
    public string MachineHash { get; set; } = "";
    public string Level { get; set; } = "pro";
    public DateTime Expiry { get; set; }
    public DateTime Issued { get; set; }
    // 绑定类型: "machine"=绑电脑, "usb"=绑U盘
    public string BindingType { get; set; } = "machine";
}
