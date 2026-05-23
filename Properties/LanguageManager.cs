using System;
using System.Globalization;
using System.Threading;
using System.Resources;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.ComponentModel;

namespace OpenClawManager.Properties
{
    public static class LanguageManager
    {
        private static ResourceManager _resourceManager;
        private static CultureInfo _currentCulture;
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language.dat");
        
        public static CultureInfo CurrentCulture => _currentCulture;
        
        // 语言更改事件
        public static event Action? LanguageChanged;
        
        public static void Initialize()
        {
            // Initialize resource manager
            _resourceManager = new ResourceManager("OpenClawManager.Properties.Resources", Assembly.GetExecutingAssembly());
            
            // 读取保存的语言设置
            string savedLanguage = "zh-CN";
            if (File.Exists(ConfigPath))
            {
                try
                {
                    savedLanguage = File.ReadAllText(ConfigPath).Trim();
                }
                catch
                {
                    savedLanguage = "zh-CN";
                }
            }
            
            SetLanguage(savedLanguage);
        }
        
        public static void SetLanguage(string cultureName)
        {
            try
            {
                _currentCulture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentUICulture = _currentCulture;
                
                // 重建ResourceManager以清除缓存
                _resourceManager = new ResourceManager("OpenClawManager.Properties.Resources", Assembly.GetExecutingAssembly());
                
                // 保存语言设置
                try
                {
                    File.WriteAllText(ConfigPath, cultureName);
                }
                catch { }
                
                // 触发语言更改事件
                System.Diagnostics.Debug.WriteLine($"Language changed to: {cultureName}, NavDashboard={GetString("NavDashboard")}");
                LanguageChanged?.Invoke();
            }
            catch
            {
                _currentCulture = CultureInfo.CurrentCulture;
            }
        }
        
        public static string GetString(string key)
        {
            try
            {
                return _resourceManager.GetString(key, _currentCulture) ?? key;
            }
            catch
            {
                return key;
            }
        }
        
        public static readonly Dictionary<string, string> SupportedLanguages = new Dictionary<string, string>
        {
            { "zh-CN", "中文" },
            { "en-US", "English" }
        };
    }
}