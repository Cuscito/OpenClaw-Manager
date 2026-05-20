using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

[assembly: SupportedOSPlatform("windows")]

namespace OpenClawManager;

static class Program
{
    // 在 Main JIT 之前注册，确保托管 DLL 可从 lib/ 或程序根目录加载。
    [ModuleInitializer]
    public static void Init()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            try
            {
                var name = new AssemblyName(args.Name).Name;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var libPath = Path.Combine(baseDir, "lib", name + ".dll");
                if (File.Exists(libPath)) return Assembly.LoadFrom(libPath);
                var rootPath = Path.Combine(baseDir, name + ".dll");
                if (File.Exists(rootPath)) return Assembly.LoadFrom(rootPath);
            }
            catch { }
            return null;
        };
    }

    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}


