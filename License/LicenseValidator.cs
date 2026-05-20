using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenClawManager;

public static class LicenseValidator
{
    // 公钥 — 由 CodeGen 工具生成后替换此值
    // 运行 CodeGen 首次会自动输出公钥 XML
    public static readonly string PublicKeyXml = "<RSAKeyValue><Modulus>3R7v5PUVahY4HBlJF6b/1nkccaHo3+VyuaBQbtuQU5GjoEDIFLq0nwky0VytjYFOqV0rgBoFC+xv+L2cjzptPpio/W6WTFDoG37KtUAPYEJUBirhUtkdCKu/C0TPtKEaBySaJIgcPMLWmd7tC7YJoLwpi4Wqey5oL+BbaA6PiI3o2EqRy30sVe5ilTvb7htieN70dxuNOr0JFXVBd8Yv/lFPt50hq+EnY44DytAJG+jDARVrgkl4ntXDYXmqv3QhdTpu09xCmlES1rvF7jTRXQfDolCl9m8i2iFANsZK6vILHW/+7/iWnf+NuWuQsSOfjV82zl0/38+jloqvawkFdQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    /// <summary>生成 RSA 密钥对，返回 (公钥XML, 私钥XML)</summary>
    public static (string publicKey, string privateKey) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ToXmlString(false), rsa.ToXmlString(true));
    }

    /// <summary>获取机器码 — 基于注册表 MachineGuid（绑定电脑）</summary>
    public static string GetMachineCode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrEmpty(guid))
                return Hash(guid);
        }
        catch { }

        // 回退: 机器名 + 处理器数
        var fallback = Environment.MachineName + "_" + Environment.ProcessorCount;
        return Hash(fallback);
    }

    /// <summary>获取U盘设备码 — 基于当前exe所在驱动器的卷序列号</summary>
    public static string GetUsbDeviceId()
    {
        try
        {
            var exePath = AppDomain.CurrentDomain.BaseDirectory;
            var root = Path.GetPathRoot(exePath);
            if (string.IsNullOrEmpty(root)) goto fallback;

            // 用 Win32 GetVolumeInformation 取卷序列号
            var sb = new System.Text.StringBuilder(256);
            if (GetVolumeInformation(root, sb, sb.Capacity, out uint serial,
                out _, out _, null, 0))
            {
                // 卷序列号 + 卷标组合，避免同型号U盘碰撞
                var label = sb.ToString().Trim();
                return Hash($"usb:{serial:X8}:{label}");
            }
        }
        catch { }

        fallback:
        // 回退: 用 DriveInfo 取属性组合
        try
        {
            var exePath = AppDomain.CurrentDomain.BaseDirectory;
            var root = Path.GetPathRoot(exePath);
            var di = new System.IO.DriveInfo(root ?? "C:\\");
            var id = $"{di.DriveType}:{di.TotalSize}:{di.VolumeLabel}:{di.DriveFormat}";
            return Hash(id);
        }
        catch { }

        return Hash("usb-fallback");
    }

    /// <summary>按绑定类型获取设备标识</summary>
    public static string GetDeviceId(string bindingType)
    {
        return bindingType == "usb" ? GetUsbDeviceId() : GetMachineCode();
    }

    /// <summary>用私钥签名 LicenseData 生成注册码</summary>
    public static string GenerateCode(string privateKeyXml, string email, string machineHash,
        string level, DateTime expiry, DateTime issued, string bindingType = "machine")
    {
        var data = new LicenseData
        {
            Email = email,
            MachineHash = machineHash,
            Level = level,
            Expiry = expiry.Date,
            Issued = issued.Date,
            BindingType = bindingType
        };
        var json = JsonSerializer.Serialize(data);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonB64 = Convert.ToBase64String(jsonBytes);

        using var rsa = RSA.Create();
        rsa.FromXmlString(privateKeyXml);
        var signature = rsa.SignData(jsonBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sigB64 = Convert.ToBase64String(signature);

        var raw = jsonB64 + ":" + sigB64;
        var rawBytes = Encoding.UTF8.GetBytes(raw);
        var rawB64 = Convert.ToBase64String(rawBytes);

        return "OCM-" + SplitCode(rawB64);
    }

    /// <summary>验证注册码，返回 (是否有效, LicenseData, 错误信息)</summary>
    public static (bool ok, LicenseData? data, string error) Validate(string code)
    {
        try
        {
            code = code.Replace("-", "").Replace(" ", "").Trim();
            if (!code.StartsWith("OCM"))
                return (false, null, "注册码格式无效");

            var payload = code[3..]; // 去掉 OCM 前缀

            // Base64 解码
            var rawBytes = Convert.FromBase64String(payload);
            var raw = Encoding.UTF8.GetString(rawBytes);
            var parts = raw.Split(':');
            if (parts.Length != 2)
                return (false, null, "注册码数据损坏");

            var jsonBytes = Convert.FromBase64String(parts[0]);
            var json = Encoding.UTF8.GetString(jsonBytes);
            var signature = Convert.FromBase64String(parts[1]);

            var data = JsonSerializer.Deserialize<LicenseData>(json);
            if (data == null)
                return (false, null, "注册码数据无效");

            // RSA 验签
            using var rsa = RSA.Create();
            rsa.FromXmlString(PublicKeyXml);
            bool validSig = rsa.VerifyData(jsonBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!validSig)
                return (false, null, "注册码签名验证失败");

            // 设备绑定校验 — 按 BindingType 选择校验方式
            if (!string.IsNullOrEmpty(data.MachineHash))
            {
                var currentDevice = GetDeviceId(data.BindingType);
                if (!string.Equals(data.MachineHash, currentDevice, StringComparison.OrdinalIgnoreCase))
                {
                    var msg = data.BindingType == "usb"
                        ? "此注册码绑定到另一个U盘设备"
                        : "此注册码绑定到另一台电脑";
                    return (false, null, msg);
                }
            }

            // 有效期检查
            if (data.IsExpired)
                return (false, null, "注册码已过期 (" + data.Expiry.ToString("yyyy-MM-dd") + ")");

            return (true, data, "");
        }
        catch (FormatException)
        {
            return (false, null, "注册码格式错误");
        }
        catch (Exception ex)
        {
            return (false, null, "验证异常: " + ex.Message);
        }
    }

    // Win32: 取卷序列号，用于U盘设备绑定
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool GetVolumeInformation(
        string lpRootPathName,
        System.Text.StringBuilder lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        System.Text.StringBuilder? lpFileSystemNameBuffer,
        int nFileSystemNameSize);

    static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    static string SplitCode(string base64)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < base64.Length; i++)
        {
            if (i > 0 && i % 5 == 0) sb.Append('-');
            sb.Append(base64[i]);
        }
        return sb.ToString();
    }
}
