using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace OpenClawManager;

public static class LicenseManager
{
    static string DataDir => AppDomain.CurrentDomain.BaseDirectory;
    static string LicenseFile => Path.Combine(DataDir, "license.dat");
    static string TrialFile => Path.Combine(DataDir, "trial.dat");
    const int TrialDays = 30;

    static LicenseData? _cachedLicense;
    static bool _loaded;

    /// <summary>是否专业版已激活</summary>
    public static bool IsPro { get { var lic = LoadLicense(); return lic != null && !lic.IsExpired; } }

    /// <summary>试用剩余天数，负数表示已过期</summary>
    public static int TrialDaysRemaining => TrialDays - (int)(DateTime.Now - GetInstallDate()).TotalDays;

    /// <summary>试用是否已过期</summary>
    public static bool IsTrialExpired => TrialDaysRemaining <= 0;

    /// <summary>许可邮箱</summary>
    public static string LicenseEmail => LoadLicense()?.Email ?? "";

    /// <summary>许可到期日</summary>
    public static DateTime? LicenseExpiry => LoadLicense()?.Expiry;

    /// <summary>许可绑定类型文本（国际化）</summary>
    public static string LicenseBindingType
    {
        get
        {
            var L = Properties.LanguageManager.GetString;
            var lic = LoadLicense();
            if (lic != null && lic.BindingType == "usb") return L("LicenseBindingUSB");
            return L("LicenseBindingPC");
        }
    }

    /// <summary>当前生效的设备码（按绑定类型返回）</summary>
    public static string LicenseDeviceCode
    {
        get
        {
            var lic = LoadLicense();
            var bindingType = lic?.BindingType ?? "machine";
            return LicenseValidator.GetDeviceId(bindingType);
        }
    }

    /// <summary>UI 状态文本（国际化）</summary>
    public static string StatusText
    {
        get
        {
            var L = Properties.LanguageManager.GetString;
            var lic = LoadLicense();
            if (lic != null)
            {
                var bindLabel = lic.BindingType == "usb" ? L("LicenseUSB") : L("LicenseBindingPC");
                if (lic.IsExpired)
                    return L("LicenseProExpired") + " (" + lic.Expiry.ToString("yyyy-MM-dd") + ")";
                return L("LicensePro") + " · " + lic.Email + " · " + L("LicenseExpiry") + " " + lic.Expiry.ToString("yyyy-MM-dd") + " · " + bindLabel;
            }
            var days = TrialDaysRemaining;
            if (days > 0)
                return L("LicenseTrial") + " · " + L("LicenseRemaining") + " " + days + " " + L("LicenseDays");
            return L("LicenseTrialExpired");
        }
    }

    /// <summary>激活注册码</summary>
    public static (bool ok, string message) Activate(string code)
    {
        var L = Properties.LanguageManager.GetString;
        var (ok, data, error) = LicenseValidator.Validate(code);
        if (!ok || data == null)
            return (false, error);

        try
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            var save = new { Code = code, ActivatedAt = DateTime.Now, Email = data.Email, Expiry = data.Expiry };
            File.WriteAllText(LicenseFile, JsonSerializer.Serialize(save), Encoding.UTF8);
            _cachedLicense = data;
            _loaded = true;
            var msg = string.Format(L("LicenseActivateSuccess"), data.Email, data.Expiry.ToString("yyyy-MM-dd"));
            return (true, msg);
        }
        catch (Exception ex)
        {
            return (false, L("LicenseSaveFailed") + ex.Message);
        }
    }

    /// <summary>清除许可（测试用）</summary>
    public static void Deactivate()
    {
        try { File.Delete(LicenseFile); } catch { }
        _cachedLicense = null;
        _loaded = false;
    }

    static LicenseData? LoadLicense()
    {
        if (_loaded) return _cachedLicense;
        _loaded = true;

        try
        {
            if (!File.Exists(LicenseFile)) return null;
            var json = File.ReadAllText(LicenseFile, Encoding.UTF8);
            var saved = JsonSerializer.Deserialize<SavedLicense>(json);
            if (saved == null || string.IsNullOrEmpty(saved.Code)) return null;

            var (ok, data, _) = LicenseValidator.Validate(saved.Code);
            if (ok && data != null)
            {
                _cachedLicense = data;
                return data;
            }
        }
        catch { }

        return null;
    }

    static DateTime GetInstallDate()
    {
        try
        {
            if (File.Exists(TrialFile))
            {
                var json = File.ReadAllText(TrialFile, Encoding.UTF8);
                var td = JsonSerializer.Deserialize<TrialData>(json);
                if (td != null && td.InstallDate > DateTime.MinValue)
                    return td.InstallDate;
            }
        }
        catch { }

        // 首次运行，记录安装日期
        var now = DateTime.Now;
        try
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            File.WriteAllText(TrialFile,
                JsonSerializer.Serialize(new TrialData { InstallDate = now }), Encoding.UTF8);
        }
        catch { }
        return now;
    }

    /// <summary>检查是否可以使用高级功能，不是则弹窗提示</summary>
    public static bool CheckPro()
    {
        var L = Properties.LanguageManager.GetString;
        if (IsPro) return true;
        var days = TrialDaysRemaining;
        var msg = days <= 0
            ? L("LicenseProFeature")
            : string.Format(L("LicenseProFeatureTrial"), days.ToString());
        MessageBox.Show(msg, L("LicenseProDialogTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    class SavedLicense
    {
        public string Code { get; set; } = "";
        public DateTime ActivatedAt { get; set; }
        public string Email { get; set; } = "";
        public DateTime Expiry { get; set; }
    }

    class TrialData
    {
        public DateTime InstallDate { get; set; }
    }
}