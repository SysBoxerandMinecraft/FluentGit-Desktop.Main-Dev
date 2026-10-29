param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",

    [switch]$Package,
    [switch]$Run,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

# 【修改点 1】因为脚本移到了 tools 目录，所以需要获取上一级目录作为项目根目录
$repoRoot = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $repoRoot "FluentGit.csproj"

# 切换工作目录到项目根目录，确保后续相对路径（如 bin 文件夹）正确
Set-Location $repoRoot

Write-Host "[FluentGit] Target: $Configuration | $Platform" -ForegroundColor Cyan

# 杀掉可能残留的进程，避免文件占用
Get-Process FluentGit -ErrorAction SilentlyContinue | Stop-Process -Force

# 仅在明确指定 -Clean 时才清理
if ($Clean) {
    Write-Host "[FluentGit] Cleaning..." -ForegroundColor Yellow
    # 【修改点 2】使用绝对路径指向 csproj
    dotnet clean $csprojPath -c $Configuration | Out-Null
}

$rid = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }

if ($Package) {
    Write-Host "[FluentGit] Publishing MSIX..." -ForegroundColor Cyan
    # 【修改点 3】使用绝对路径指向 csproj
    dotnet publish $csprojPath -c Release -p:Platform=$Platform `
      -p:RuntimeIdentifier=$rid `
      -p:WindowsPackageType=MSIX `
      -p:GenerateAppxPackageOnBuild=true `
      -p:AppxPackageSigningEnabled=false `
      -p:AppxPackageAllowUnsigned=true
} else {
    Write-Host "[FluentGit] Building..." -ForegroundColor Cyan
    # 【修改点 4】使用绝对路径指向 csproj
    dotnet build $csprojPath -c $Configuration -p:Platform=$Platform
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FluentGit] Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

# ==========================================
# 多语言检查
# ==========================================
# $repoRoot 已经指向上级目录，这里的 bin 路径依然正确
$targetDir = "$repoRoot\bin\$Platform\$Configuration"
$priFile = Get-ChildItem -Path $targetDir -Filter "resources.pri" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($priFile) {
    Write-Host "[FluentGit] Localization resource found: $($priFile.FullName)" -ForegroundColor Green
} else {
    Write-Host "[FluentGit] WARNING: resources.pri not found. Localization may fail!" -ForegroundColor Yellow
}

# ==========================================
# 运行模式
# ==========================================
if ($Run) {
    if ($Package) {
        Write-Host "[FluentGit] Cannot use -Run with -Package. Please install the MSIX manually." -ForegroundColor Red
    } else {
        Write-Host "[FluentGit] Launching via 'dotnet run'..." -ForegroundColor Green
        # 【修改点 5】使用绝对路径指向 csproj
        dotnet run --project $csprojPath -c $Configuration -p:Platform=$Platform --no-build
    }
} else {
    Write-Host "[FluentGit] Build succeeded." -ForegroundColor Green
}