# FluentGit

> 一个基于 **WinUI 3 / Windows App SDK** 的轻量级 Git 图形客户端  
> 追求 Fluent Design 的现代界面与流畅体验，专注日常仓库管理。

---

[English](README.EN.md) | 中文

## ✨ 功能特性

| 模块 | 功能 |
|------|------|
| **仓库管理** | 初始化仓库（`git init`）、打开本地仓库 |
| **远程同步** | 拉取更新（`git pull`）、获取远程信息（`git fetch`） |
| **提交变更** | 全部暂存（`git add -A`）、提交（`git commit -m`） |
| **目录浏览** | 目录树展示、双击打开文件、右键菜单（打开 / 终端 / 资源管理器 / 复制路径） |
| **设置中心** | Git 路径自动查找 + 签名验证、编译产物过滤开关、设置项搜索 |
| **主题** | 跟随系统 / 手动切换，设置持久化（Windows 11 启用 Mica，Windows 10 回退纯色） |
| **提示反馈** | 所有操作结果通过 InfoBar 滑入提示，自动消失 |


---

## 🖼️ 界面预览

![初始界面](docs\img\界面截图.png)

---

## 🧱 技术栈

- **UI**：WinUI 3 / Windows App SDK 2.4
- **语言**：C# / .NET 10
- **打包**：MSIX（未签名，需要开发者模式或手动签名）
- **平台**：Windows 10 22H2（19045）及以上、Windows 11

---

## 📁 项目结构

```
FluentGit/
├── App.xaml / App.xaml.cs           # 应用入口，全局异常处理
├── MainWindow.xaml / .cs            # 主窗口（Mica + 自定义 TitleBar + NavigationView）
├── FluentGit.csproj                 # 项目文件
├── app.manifest                     # 应用清单（DPI、支持的系统版本）
├── Package.appxmanifest             # MSIX 清单
├── Building.ps1                     # 构建脚本（见上方「使用 Building.ps1」）
│
├── Views/                           # 页面层
│   ├── RepoPage.xaml / .cs          # 页面骨架，字段 + 加载 + Git 检测。
│   ├── RepoPage.Status.cs           # 变更状态刷新与界面显隐
│   ├── RepoPage.InfoBar.cs          # InfoBar 滑入滑出动画
│   ├── RepoPage.Actions.cs          # Git 操作事件，统一收敛进ExecuteGitOperationAsync
│   ├── AdminPage.xaml / .cs         # 目录树 + 右键菜单
│   ├── SettingsPage.xaml / .cs      # 设置中心（搜索、分组）
│   └── PlaceholderPage.xaml / .cs   # 主题设置（临时页，后续并入 SettingsPage）
│
├── Services/                        # 服务层
│   ├── AppState.cs                  # 全局状态（当前仓库路径、Git 路径）
│   ├── AppLogger.cs                 # 统一日志（OK / Warning / Error / Info）
│   ├── CrashLogger.cs               # 崩溃日志落盘（%LocalAppData%\FluentGit\logs）
│   ├── SettingsService.cs           # JSON 配置持久化（原子写）
│   ├── GitPathHelper.cs             # Git 路径自动查找 + 签名验证（带缓存）
│   │
│   ├── GitService/                  # Git 命令模块化封装
│   │   ├── GitService.cs            # 主入口 + RunGit（ArgumentList）
│   │   ├── GitService.Repository.cs # init / clone
│   │   ├── GitService.Remote.cs     # pull / fetch
│   │   ├── GitService.Commit.cs     # add / commit
│   │   ├── GitService.Status.cs     # status / 变更摘要
│   │   ├── GitService.Branches.cs   # branch 列表
│   │   └── GitService.Models.cs     # GitStatusEntry
│   │
│   └── Search/                      # 通用搜索模块
│       ├── SearchEntry.cs           # 搜索项（标题 / 关键字 / 关联 UI）
│       └── SearchService.cs         # 匹配、过滤、更新可见性
│
├── Assets/                          # 图标资源
│   ├── AppIcon.ico
│   ├── *.png                        # MSIX 徽标、启动画面
│   └── File_Icon/                   # 目录树图标（文件 / 空文件夹 / 满文件夹）
│
├── Easteregg/                      
|-- 你应该自己去看看。
│
└── .vscode/                         # VS Code 配置
    ├── launch.json                  # 调试配置
    └── tasks.json                   # build 任务
```

---

## 🚀 构建与运行

### 环境要求

- Windows 10 22H2 (10.0.19045) 或更高
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Git for Windows](https://git-scm.com/install/windows)（官方签名版）

### 命令行构建

```powershell
# Debug
dotnet build FluentGit.csproj -c Debug -p:Platform=x64

# Release
dotnet build FluentGit.csproj -c Release -p:Platform=x64

# 运行
.\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\FluentGit.exe
```

### 使用 Building.ps1

仓库附带 `Building.ps1`，封装了「杀进程 → clean → build」的常用流程。

```powershell
# Debug 构建（默认）
./Building.ps1

# Release 构建
./Building.ps1 -Configuration Release

# 打 MSIX 包（等价于 dotnet publish，见下一节）
./Building.ps1 -Package
```

**参数说明：**

| 参数 | 可选值 | 默认 | 说明 |
|---|---|---|---|
| `-Configuration` | `Debug` / `Release` | `Debug` | 构建配置 |
| `-Platform` | `x64` / `ARM64` | `x64` | 目标架构 |
| `-Package` | 开关 | 关 | 打 MSIX 包（内部固定 Release） |

**脚本会做什么：**

1. 切换到脚本所在目录（用 `$PSScriptRoot`，不依赖你当前的工作目录）
2. 结束残留的 `FluentGit.exe` 进程
3. 执行 `dotnet clean`
4. 执行 `dotnet build` 或 `dotnet publish`
5. 构建失败时返回非 0 退出码

> **首次运行若报「无法加载文件 Building.ps1，因为在此系统上禁止运行脚本」**，在 PowerShell 里执行一次：
>
> ```powershell
> Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
> ```
>
> 或者临时绕过：
>
> ```powershell
> powershell -ExecutionPolicy Bypass -File .\Building.ps1
> ```
---

## 🔐 安全说明

- **Git 路径签名验证**：通过 WinVerifyTrust 验证 `git.exe`、`bash.exe`、`sh.exe` 等文件的 Authenticode 签名，签名者需为 **Johannes Schindelin**（Git for Windows 官方维护者）。
- **智能路径推导**：支持从 `bash.exe` / `git-bash.exe` / `git-cmd.exe` 推导出 `git.exe`。
- **不联网校验**：签名验证使用 `WTD_CACHE_ONLY_URL_RETRIEVAL`，不依赖网络。

---

## 🧘 开发者彩蛋

`MainWindow.xaml.cs` 的构造函数顶部供奉了一尊 **电子佛祖**，用于保佑代码无 BUG。

> **请勿移除**。实测移除后 BUG 率上升 100%。（开玩笑的……大概）

---
## 📦 安装 MSIX

### 系统要求

- Windows 10 22H2 (10.0.19045) 及以上（x64）
- Windows 11（ARM64/x64）
- 无需单独安装 .NET 运行时（已自包含打包）

### 安装步骤

1. 下载 `.msix` 文件
2. 双击安装前，需要先信任签名证书：
   - 双击 `FluentGit_TemporaryKey.pfx`
   - 选择 **本地计算机** → **将所有的证书都放入下列存储** → **受信任的根证书颁发机构**
3. 双击 `.msix` 安装
4. 从开始菜单启动
---

## 📄 开源协议

MIT License

---

## 🙏 鸣谢

- [Git for Windows](https://gitforwindows.org/)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- 所有贡献者与用户
---
更多彩蛋见 [`Easteregg/README.md`](./Easteregg/README.md)。
---

> **FluentGit** — 让 Git 更优雅。