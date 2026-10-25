# FluentGit

> A lightweight Git GUI client built on **WinUI 3 / Windows App SDK**  
> Pursuing a modern Fluent Design interface and smooth experience, focusing on daily repository management.

---

## ✨ Features

| Module | Feature |
|--------|---------|
| **Repository Management** | Initialize repository (`git init`), open local repository |
| **Remote Sync** | Pull updates (`git pull`), fetch remote info (`git fetch`) |
| **Commit Changes** | Stage all (`git add -A`), commit (`git commit -m`) |
| **Directory Browsing** | Directory tree view, double-click to open files, right-click menu (Open / Terminal / Explorer / Copy Path) |
| **Settings Center** | Auto-find Git path + signature verification, build artifact filter toggle, settings search |
| **Theme** | Follows system or manual toggle, persisted in settings (Mica on Windows 11, solid color fallback on Windows 10) |
| **Notifications** | All operation results shown via InfoBar sliding notification, auto-dismiss |


---

## 🖼️ Screenshots

![Home Screen](docs\img\界面截图.png)

---

## 🧱 Tech Stack

- **UI**: WinUI 3 / Windows App SDK 2.4
- **Language**: C# / .NET 10
- **Packaging**: MSIX (unsigned; requires Developer Mode or manual signing)
- **Platform**: Windows 10 22H2 (19045) and above, Windows 11

---

## 📁 Project Structure

```
FluentGit/
├── App.xaml / App.xaml.cs           # Application entry, global exception handling
├── MainWindow.xaml / .cs            # Main window (Mica + custom TitleBar + NavigationView)
├── FluentGit.csproj                 # Project file
├── app.manifest                     # App manifest (DPI, supported OS)
├── Package.appxmanifest             # MSIX manifest
├── Building.ps1                     # Build script (see "Using Building.ps1" above)
│
├── Views/                           # Pages
│   ├── RepoPage.xaml / .cs          # Page skeleton, fields + loading + Git detection.
│   ├── RepoPage.Status.cs           # Change status refresh and UI visibility (show/hide).
│   ├── RepoPage.InfoBar.cs          # InfoBar slide-in/slide-out animations.
│   ├── RepoPage.Actions.cs          # Git operation events, uniformly centralized into ExecuteGitOperationAsync.
│   ├── AdminPage.xaml / .cs         # Directory tree + context menu
│   ├── SettingsPage.xaml / .cs      # Settings center (search, grouping)
│   └── PlaceholderPage.xaml / .cs   # Theme settings (temporary page, to be merged into SettingsPage)
│
├── Services/                        # Services
│   ├── AppState.cs                  # Global state (current repo path, Git path)
│   ├── AppLogger.cs                 # Unified logging (OK / Warning / Error / Info)
│   ├── CrashLogger.cs               # Crash log to disk (%LocalAppData%\FluentGit\logs)
│   ├── SettingsService.cs           # JSON settings persistence (atomic write)
│   ├── GitPathHelper.cs             # Git path auto-discovery + signature verification (cached)
│   │
│   ├── GitService/                  # Modular Git command wrapper
│   │   ├── GitService.cs            # Main entry + RunGit (ArgumentList)
│   │   ├── GitService.Repository.cs # init / clone
│   │   ├── GitService.Remote.cs     # pull / fetch
│   │   ├── GitService.Commit.cs     # add / commit
│   │   ├── GitService.Status.cs     # status / change summary
│   │   ├── GitService.Branches.cs   # branch list
│   │   └── GitService.Models.cs     # GitStatusEntry
│   │
│   └── Search/                      # Generic search module
│       ├── SearchEntry.cs           # Search entry (title / keywords / linked UI)
│       └── SearchService.cs         # Matching, filtering, visibility updates
│
├── Assets/                          # Icon resources
│   ├── AppIcon.ico
│   ├── *.png                        # MSIX logos, splash screen
│   └── File_Icon/                   # Directory tree icons (file / empty folder / full folder)
│
├── Easteregg/   
|-- You should go see for yourself.                    
└── .vscode/                         # VS Code configuration
    ├── launch.json                  # Debug configuration
    └── tasks.json                   # Build task
```

---

## 🚀 Build & Run

### Requirements

- Windows 10 22H2 (10.0.19045) or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Git for Windows](https://git-scm.com/install/windows) (official signed version)

### Command Line Build

```powershell
# Debug
dotnet build FluentGit.csproj -c Debug -p:Platform=x64

# Release
dotnet build FluentGit.csproj -c Release -p:Platform=x64

# Run
.\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\FluentGit.exe
```

### Using Building.ps1

The repo ships with `Building.ps1`, which wraps the common flow: kill running process → clean → build.

```powershell
# Debug build (default)
./Building.ps1

# Release build
./Building.ps1 -Configuration Release

# Produce an MSIX package (same as dotnet publish, see next section)
./Building.ps1 -Package
```

**Parameters:**

| Parameter | Values | Default | Description |
|---|---|---|---|
| `-Configuration` | `Debug` / `Release` | `Debug` | Build configuration |
| `-Platform` | `x64` / `ARM64` | `x64` | Target architecture |
| `-Package` | switch | off | Build MSIX package (forces Release internally) |

**What the script does:**

1. Changes to the script's own directory (uses `$PSScriptRoot`, independent of your current working directory)
2. Kills any lingering `FluentGit.exe` process
3. Runs `dotnet clean`
4. Runs `dotnet build` or `dotnet publish`
5. Returns a non-zero exit code if the build fails

> **If you see "cannot be loaded because running scripts is disabled on this system"**, run once in PowerShell:
>
> ```powershell
> Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
> ```
>
> Or bypass for a single invocation:
>
> ```powershell
> powershell -ExecutionPolicy Bypass -File .\Building.ps1
> ```
---

## 🔐 Security

- **Git path signature verification**: Uses WinVerifyTrust to validate the Authenticode signature of `git.exe`, `bash.exe`, `sh.exe`, etc. The signer must be **Johannes Schindelin** (official maintainer of Git for Windows).
- **Smart path resolution**: Supports deriving `git.exe` from `bash.exe` / `git-bash.exe` / `git-cmd.exe`.
- **Offline verification**: Uses `WTD_CACHE_ONLY_URL_RETRIEVAL`, no network dependency.

---

## 🧘 Developer Easter Egg

A **digital Buddha** is enshrined at the top of the `MainWindow.xaml.cs` constructor to bless the code with no BUGs.

> **Do NOT remove.** Tested: removing it increases BUG rate by 100%. (Just kidding... probably.)

---

## 📦 Install MSIX

### Requirements

- Windows 10 22H2 (10.0.19045) or later (x64)
- Windows 11 (ARM64/x64)
- No separate .NET runtime required (self-contained)

### Steps

1. Download the `.msix` file
2. Trust the signing certificate before installing:
   - Double-click `FluentGit_TemporaryKey.pfx`
   - Choose **Local Machine** → **Place all certificates in the following store** → **Trusted Root Certification Authorities**
3. Double-click the `.msix` to install
4. Launch from the Start menu
---

## 📄 License

MIT License

---

## 🙏 Credits

- [Git for Windows](https://gitforwindows.org/)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- All contributors and users

---
See [`Easteregg/README.en.md`](./Easteregg/README.en.md) for more Easter eggs.
---

> **FluentGit** — Making Git more elegant.