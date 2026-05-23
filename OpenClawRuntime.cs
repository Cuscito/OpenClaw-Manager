using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClawManager;

/// <summary>
/// 共享运行时工具类 — 集中管理 Node.exe 路径、OpenClaw 入口、进程启动、文本处理、
/// 配置读写和网关进程 PID 追踪。消除 SkillsPage、SearchDialog、DashboardPage、
/// SettingsPage 之间的重复代码。
/// </summary>
public static class OpenClawRuntime
{
    // ── 路径解析 ──

    /// <summary>runtime/node.exe 的完整路径；不存在时返回空字符串。</summary>
    public static string NodeExe
    {
        get
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node.exe");
            return File.Exists(path) ? path : "";
        }
    }

    /// <summary>
    /// OpenClaw 入口脚本（dist/index.js 或 openclaw.mjs）的完整路径；
    /// 都不存在时返回空字符串。
    /// </summary>
    public static string OpenClawEntry
    {
        get
        {
            var dist = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "index.js");
            if (File.Exists(dist)) return dist;
            var mjs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "openclaw.mjs");
            return File.Exists(mjs) ? mjs : "";
        }
    }

    /// <summary>npm.cmd 的完整路径；不存在时返回空字符串。</summary>
    public static string NpmCmdPath
    {
        get
        {
            var dir = Path.GetDirectoryName(NodeExe);
            if (string.IsNullOrEmpty(dir)) return "";
            var npmCmd = Path.Combine(dir, "npm.cmd");
            if (File.Exists(npmCmd) && Directory.Exists(Path.Combine(dir, "node_modules", "npm")))
                return npmCmd;
            return "";
        }
    }

    // ── 配置读写 ──

    /// <summary>读取 openclaw.json 配置；失败返回 null。</summary>
    public static JsonObject? ReadConfig()
    {
        try
        {
            var path = MainForm.CfgFullPath;
            if (File.Exists(path))
                return JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))?.AsObject();
        }
        catch (Exception ex)
        {
            LogError("OpenClawRuntime.ReadConfig", ex);
        }
        return null;
    }

    /// <summary>保存 JSON 配置到 openclaw.json；失败记录日志。</summary>
    public static void SaveConfig(JsonObject cfg)
    {
        try
        {
            var path = MainForm.CfgFullPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, cfg.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            LogError("OpenClawRuntime.SaveConfig", ex);
        }
    }

    // ── JSON 辅助 ──

    public static List<string> ReadArray(JsonObject? obj, string key)
        => obj?[key]?.AsArray()?.Select(x => x?.ToString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new();

    // ── 进程启动（异步） ──

    /// <summary>
    /// 异步运行 openclaw CLI 命令。
    /// 返回 (exitCode, stdout, stderr)。超时或被取消时尽力 kill 子进程。
    /// </summary>
    public static async Task<(int code, string stdout, string stderr)> RunOpenClawAsync(
        string args, int timeoutMs, CancellationToken token)
    {
        var node = NodeExe;
        var entry = OpenClawEntry;
        if (string.IsNullOrEmpty(node) || string.IsNullOrEmpty(entry))
            return (-1, "", "未找到 runtime/node.exe 或 OpenClaw 入口");

        var psi = new ProcessStartInfo
        {
            FileName = node,
            Arguments = "\"" + entry + "\" " + args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.EnvironmentVariables["OPENCLAW_HOME"] = AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            using var p = Process.Start(psi);
            if (p == null) return (-1, "", "无法启动 OpenClaw");
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            try { await p.WaitForExitAsync(linked.Token); }
            catch (OperationCanceledException)
            {
                try { if (!p.HasExited) p.Kill(); } catch { }
                return (-1, await stdout, await stderr + "\n操作超时或已取消");
            }
            return (p.ExitCode, await stdout, await stderr);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    // ── 进程启动（同步） ──

    /// <summary>
    /// 同步运行 openclaw CLI 命令。
    /// 返回 (exitCode, stdout, stderr)。超时时尽力 kill 子进程。
    /// </summary>
    public static (int code, string stdout, string stderr) RunOpenClawSync(string args, int timeoutMs)
    {
        var node = NodeExe;
        var entry = OpenClawEntry;
        if (string.IsNullOrEmpty(node) || string.IsNullOrEmpty(entry))
            return (-1, "", "未找到 runtime/node.exe 或 OpenClaw 入口");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = node,
                Arguments = "\"" + entry + "\" " + args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["OPENCLAW_HOME"] = AppDomain.CurrentDomain.BaseDirectory;
            using var p = Process.Start(psi);
            if (p == null) return (-1, "", "无法启动 OpenClaw");
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                return (-1, stdoutTask.Result, stderrTask.Result + "\n操作超时");
            }
            return (p.ExitCode, stdoutTask.Result, stderrTask.Result);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    // ── 通用命令执行 ──

    /// <summary>
    /// 解析并构建通用命令的 ProcessStartInfo。
    /// 支持 "openclaw ..."、"npm ..."/"npm.cmd ..." 和普通 cmd 命令。
    /// </summary>
    public static ProcessStartInfo BuildCommandPsi(string cmd, bool admin = false)
    {
        string fileName, args;
        if (cmd.StartsWith("openclaw "))
        {
            fileName = NodeExe;
            args = "\"" + OpenClawEntry + "\" " + cmd["openclaw ".Length..];
        }
        else if (cmd.StartsWith("npm ") || cmd.StartsWith("npm.cmd "))
        {
            var npm = NpmCmdPath;
            if (string.IsNullOrEmpty(npm) || !File.Exists(npm))
            { fileName = "cmd.exe"; args = "/c " + cmd; }
            else
            { fileName = npm; args = cmd.StartsWith("npm.cmd ") ? cmd["npm.cmd ".Length..] : cmd["npm ".Length..]; }
        }
        else
        {
            fileName = "cmd.exe";
            args = "/c " + cmd;
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = admin,
            CreateNoWindow = !admin,
            WindowStyle = admin ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
        };
        psi.EnvironmentVariables["OPENCLAW_HOME"] = AppDomain.CurrentDomain.BaseDirectory;

        if (!admin)
        {
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
        }
        else
        {
            psi.Verb = "runas";
        }
        return psi;
    }

    /// <summary>
    /// 执行通用命令并等待完成（最多 30 秒），超时时 kill 进程。
    /// </summary>
    public static async Task RunCmdAsync(string cmd, bool admin = false, int timeoutMs = 30000)
    {
        try
        {
            var psi = BuildCommandPsi(cmd, admin);
            using var p = Process.Start(psi);
            if (p != null)
            {
                try { await p.WaitForExitAsync(new CancellationTokenSource(timeoutMs).Token); }
                catch (OperationCanceledException) { /* timeout */ }
                if (p != null && !p.HasExited) { try { p.Kill(); } catch { } }
            }
        }
        catch (Exception ex)
        {
            LogError("OpenClawRuntime.RunCmdAsync", ex);
        }
    }

    // ── 文本辅助 ──

    /// <summary>截断字符串到最大长度，超出部分用 "..." 替换。</summary>
    public static string Trim(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "...";

    /// <summary>获取多行文本的最后一非空行（去除前后空格）。</summary>
    public static string LastLine(string value)
        => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "未知错误";

    // ── 简单日志 ──

    static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".openclaw", "manager.log");

    /// <summary>记录错误到 manager.log，静默忽略日志写入失败。</summary>
    public static void LogError(string source, Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";
            File.AppendAllText(LogPath, entry, Encoding.UTF8);
        }
        catch { /* 日志写入失败不应影响主流程 */ }
    }

    // ── 网关进程 PID 追踪（解决无差别杀进程问题） ──

    static readonly HashSet<int> _ownedProcessIds = new();
    static readonly object _pidLock = new();

    /// <summary>记录由本管理器启动的进程 PID。</summary>
    public static void TrackProcess(Process p)
    {
        if (p == null) return;
        lock (_pidLock) { _ownedProcessIds.Add(p.Id); }
    }

    /// <summary>记录已知 PID（用于事后追踪）。</summary>
    public static void TrackPid(int pid)
    {
        lock (_pidLock) { _ownedProcessIds.Add(pid); }
    }

    /// <summary>仅杀死本管理器启动的进程（按 PID），不波及系统其他进程。</summary>
    public static void KillOwnedProcesses()
    {
        List<int> pids;
        lock (_pidLock)
        {
            pids = new List<int>(_ownedProcessIds);
            _ownedProcessIds.Clear();
        }
        foreach (var pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (!p.HasExited) p.Kill();
            }
            catch (ArgumentException) { /* 进程已退出 */ }
            catch (InvalidOperationException) { /* 进程已退出 */ }
            catch (Exception ex)
            {
                LogError("OpenClawRuntime.KillOwnedProcesses", ex);
            }
        }
    }

    /// <summary>检查指定 PID 的进程是否仍在运行。</summary>
    public static bool IsProcessAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch { return false; }
    }
}
