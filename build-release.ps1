# LocalShare Automated Release & Installer Build Script
# Usage: powershell -File .\build-release.ps1 [-Version 1.4.0]

param(
    [string]$Version,
    [string]$GitHubToken = $env:GITHUB_TOKEN
)

$ErrorActionPreference = "Stop"

# Automatically parse .env file if GITHUB_TOKEN is not passed directly
if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    Write-Host "====================ENV======================================"
    $EnvFile = Join-Path (Get-Location) ".env"
    if (Test-Path $EnvFile) {
        Get-Content $EnvFile | ForEach-Object {
            if ($_ -match '^\s*GITHUB_TOKEN\s*=\s*(.*)\s*$') {
                $GitHubToken = $matches[1].Trim('"', "'", ' ')
            }
        }
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " 🚀 LocalShare - Stable Release & Installer Builder" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ProjectDir = Get-Location
$PublishDir = Join-Path $ProjectDir "dist\publish"
$InstallerOutputDir = Join-Path $ProjectDir "dist\installer"
$IssScript = Join-Path $ProjectDir "installer\installer.iss"
$PropsPath = Join-Path $ProjectDir "Directory.Build.props"

# 1. Update Directory.Build.props if -Version parameter is passed
if ($Version) {
    Write-Host "`n[Version Update] Setting application version to v$Version..." -ForegroundColor Yellow
    $CleanVer = $Version.TrimStart('v', 'V')
    $PropsXml = @"
<Project>
  <PropertyGroup>
    <Version>$CleanVer</Version>
    <AssemblyVersion>$CleanVer.0</AssemblyVersion>
    <FileVersion>$CleanVer.0</FileVersion>
    <InformationalVersion>$CleanVer</InformationalVersion>
  </PropertyGroup>
</Project>
"@
    $PropsXml | Out-File -FilePath $PropsPath -Encoding utf8
    Write-Host "✅ Directory.Build.props updated with version v$CleanVer" -ForegroundColor Green
}

# 2. Extract current dynamic version
$CurrentVersion = "1.0.0"
if (Test-Path $PropsPath) {
    [xml]$xml = Get-Content $PropsPath
    if ($xml.Project.PropertyGroup.Version) {
        $CurrentVersion = $xml.Project.PropertyGroup.Version
    }
}

Write-Host "`n[Building Target Version]: v$CurrentVersion" -ForegroundColor Green

# 3. Clean previous build artifacts
Write-Host "`n[1/5] Cleaning previous build output folders..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $InstallerOutputDir) { Remove-Item $InstallerOutputDir -Recurse -Force }

New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $InstallerOutputDir -Force | Out-Null

# 4. Restore dependencies for target runtime win-x64
Write-Host "`n[2/5] Restoring project dependencies for win-x64..." -ForegroundColor Yellow
dotnet restore src/LocalShare.App/LocalShare.App.csproj -r win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "Dotnet restore failed!" -ForegroundColor Red
    exit 1
}

# 5. Run dotnet publish for self-contained x64 Single File
Write-Host "`n[3/5] Publishing self-contained single-file x64 release..." -ForegroundColor Yellow
dotnet publish src/LocalShare.App/LocalShare.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Dotnet publish failed!" -ForegroundColor Red
    exit 1
}

# Copy Assets folder into publish output directory for Windows Shortcut icon resolution
$PublishAssetsDir = Join-Path $PublishDir "Assets"
if (-not (Test-Path $PublishAssetsDir)) { New-Item -ItemType Directory -Path $PublishAssetsDir -Force | Out-Null }
Copy-Item "src\LocalShare.App\Assets\*" $PublishAssetsDir -Force

Write-Host "Self-contained release v$CurrentVersion published to: $PublishDir" -ForegroundColor Green

# 6. Create sample latest_version.json manifest for update server
Write-Host "`n[4/5] Generating update manifest (latest_version.json)..." -ForegroundColor Yellow
$ManifestObj = [PSCustomObject]@{
    version = $CurrentVersion
    releaseDate = (Get-Date -Format "yyyy-MM-dd")
    downloadUrl = "https://github.com/razorisuru/LocalShare/releases/download/v$CurrentVersion/LocalShare_Setup_v$CurrentVersion.exe"
    changelog = "Release version v$CurrentVersion with Obsidian Glass UI, dark high-visibility controls, Public Space browser, multi-peer streaming, and dynamic version management."
    sha256 = ""
    isMandatory = $false
}

$ManifestPath = Join-Path $ProjectDir "dist\latest_version.json"
$ManifestObj | ConvertTo-Json -Depth 4 | Out-File -FilePath $ManifestPath -Encoding utf8
Write-Host "Update manifest v$CurrentVersion generated: $ManifestPath" -ForegroundColor Green

# 7. Search for Inno Setup Compiler (ISCC.exe) to generate installer .exe
Write-Host "`n[5/5] Searching for Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Yellow
$IsccPaths = @(
    "ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$IsccCmd = $null
foreach ($p in $IsccPaths) {
    if (Get-Command $p -ErrorAction SilentlyContinue) {
        $IsccCmd = $p
        break
    }
    if (Test-Path $p) {
        $IsccCmd = $p
        break
    }
}

if ($IsccCmd) {
    Write-Host "Compiling setup installer using Inno Setup ($IsccCmd)..." -ForegroundColor Cyan
    & $IsccCmd "/DMyAppVersion=$CurrentVersion" $IssScript
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer generated at: $InstallerOutputDir\LocalShare_Setup_v$CurrentVersion.exe" -ForegroundColor Green
    } else {
        Write-Host "Inno Setup script compilation failed!" -ForegroundColor Red
    }
} else {
    Write-Host "Inno Setup (ISCC.exe) is not installed on this machine." -ForegroundColor Yellow
    Write-Host "Your standalone single-file release v$CurrentVersion is ready in: $PublishDir" -ForegroundColor White
    Write-Host "To build the setup installer, install Inno Setup 6 from https://jrsoftware.org/isdl.php and rerun this script." -ForegroundColor White
}

# 8. Auto-publish to GitHub Releases using .env token
if (-not [string]::IsNullOrWhiteSpace($GitHubToken) -or (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "`n[GitHub Release Upload] Uploading release v$CurrentVersion to GitHub Releases..." -ForegroundColor Yellow
    & powershell -File .\publish-github-release.ps1 -Version $CurrentVersion -GitHubToken $GitHubToken
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Release build process complete for v$CurrentVersion!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
