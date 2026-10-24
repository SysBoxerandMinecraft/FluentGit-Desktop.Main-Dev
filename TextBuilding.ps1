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
$repoRoot = $PSScriptRoot
Set-Location $repoRoot

Write-Host "[FluentGit] Target: $Configuration | $Platform" -ForegroundColor Cyan

# 杀掉可能残留的进程，避免文件占用
Get-Process FluentGit -ErrorAction SilentlyContinue | Stop-Process -Force

# 仅在明确指定 -Clean 时才清理
if ($Clean) {
    Write-Host "[FluentGit] Cleaning..." -ForegroundColor Yellow
    dotnet clean FluentGit.csproj -c $Configuration | Out-Null
}

$rid = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }

if ($Package) {
    Write-Host "[FluentGit] Publishing MSIX..." -ForegroundColor Cyan
    dotnet publish FluentGit.csproj -c Release -p:Platform=$Platform `
      -p:RuntimeIdentifier=$rid `
      -p:WindowsPackageType=MSIX `
      -p:GenerateAppxPackageOnBuild=true `
      -p:AppxPackageSigningEnabled=false `
      -p:AppxPackageAllowUnsigned=true
} else {
    Write-Host "[FluentGit] Building..." -ForegroundColor Cyan
    dotnet build FluentGit.csproj -c $Configuration -p:Platform=$Platform
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FluentGit] Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

# ==========================================
# 多语言检查
# ==========================================
$targetDir = "$repoRoot\bin\$Platform\$Configuration"
$priFile = Get-ChildItem -Path $targetDir -Filter "resources.pri" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($priFile) {
    Write-Host "[FluentGit] Localization resource found: $($priFile.FullName)" -ForegroundColor Green
} else {
    Write-Host "[FluentGit] WARNING: resources.pri not found. Localization may fail!" -ForegroundColor Yellow
}

# ==========================================
# 运行模式 (使用 dotnet run)
# ==========================================
if ($Run) {
    if ($Package) {
        Write-Host "[FluentGit] Cannot use -Run with -Package. Please install the MSIX manually." -ForegroundColor Red
    } else {
        Write-Host "[FluentGit] Launching via 'dotnet run'..." -ForegroundColor Green
        # --no-build 防止重复编译，因为上面已经 build 过了
        dotnet run --project FluentGit.csproj -c $Configuration -p:Platform=$Platform --no-build
    }
} else {
    Write-Host "[FluentGit] Build succeeded." -ForegroundColor Green
}