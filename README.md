# OpenClaw Manager

OpenClaw 网关的 Windows 桌面管理工具，基于 WinForms + .NET 10 + WebView2。

## 功能

- **仪表盘** — 网关状态监控、启停控制、版本切换、实时控制台、健康守护
- **AI 对话** — WebView2 内嵌 Chat UI，流式推理，会话持久化
- **模型配置** — 供应商/模型 CRUD，Ollama 拉取，默认模型设置
- **技能管理** — ClawHub 技能搜索/安装/启用
- **频道设置** — 20+ 频道配置（Telegram/Discord/微信/飞书等）
- **系统设置** — 端口/认证/注册码管理
- **注册码系统** — RSA-2048 签名，支持电脑绑定和 U 盘绑定
- **中英文切换** — 全界面国际化

## 快速开始

### 下载

从 [Releases](https://github.com/Cuscito/OpenClaw-Manager-20260521/releases) 页面下载最新版本：

| 文件 | 说明 |
|------|------|
| `OpenClaw-Manager.zip` | 主程序编译产物 |
| `runtime.part01` / `runtime.part02` / `runtime.part03` | Node.js 运行时分卷 |

### 安装

```powershell
# 1. 解压主程序
Expand-Archive -Path OpenClaw-Manager.zip -DestinationPath OpenClaw-Manager

# 2. 合并运行时（Windows CMD / PowerShell）
copy /b runtime.part01 + runtime.part02 + runtime.part03 runtime.zip

# 3. 解压运行时到程序目录
Expand-Archive -Path runtime.zip -DestinationPath OpenClaw-Manager\runtime

# 4. 启动
Start-Process OpenClaw-Manager\OpenClaw-Manager.exe
```

合并后目录结构：

```
OpenClaw-Manager/
├── OpenClaw-Manager.exe
├── marked.min.js
├── runtime/
│   ├── node.exe
│   └── node_modules/openclaw/
└── workspace/          # 首次启动自动创建
```

## 技术栈

| 技术 | 说明 |
|------|------|
| .NET 10 | WinForms + WebView2 |
| C# | 核心语言 |
| marked.js | 内嵌 Markdown 渲染 |
| Node.js | OpenClaw 网关运行时 |

## 项目结构

```
├── OpenClawManager.csproj
├── Program.cs / Theme.cs / MainForm.cs / OpenClawRuntime.cs
├── Pages/
│   ├── DashboardPage.cs    # 仪表盘
│   ├── ChatPage.cs         # AI 对话
│   ├── ModelsPage.cs       # 模型配置
│   ├── SkillsPage.cs       # 技能管理
│   ├── ChannelsPage.cs     # 频道设置
│   ├── LogsPage.cs         # 运行日志
│   ├── SettingsPage.cs     # 系统设置
│   ├── SetupPage.cs        # 初始化向导
│   └── GuardianPage.cs     # 守护程序
├── License/                # 注册码系统
├── CodeGenGui/             # 注册码生成器
├── Properties/             # 语言资源 (zh-CN / en-US)
├── .github/workflows/      # CI/CD 自动编译
└── runtime.zip             # Node.js + OpenClaw 运行时 (168MB)
```

## 开发

```powershell
# 编译发布
dotnet publish OpenClawManager.csproj -c Release

# 发布到指定目录
dotnet publish OpenClawManager.csproj -c Release -o D:\output --self-contained -r win-x64
```

## CI/CD

推送到 `v*` 标签自动触发 GitHub Actions 编译并创建 Release。

## 绑定说明

- **电脑绑定**：基于 Windows MachineGuid，更换电脑需重新激活
- **U 盘绑定**：基于卷序列号 + 卷标，适合便携场景

## License

MIT License. See [LICENSE.txt](License/LICENSE.txt) for details.

## ❤️ 赞赏

如果这个项目对你有帮助，欢迎请我喝杯咖啡~

![微信赞赏](wechat-qr.png)
