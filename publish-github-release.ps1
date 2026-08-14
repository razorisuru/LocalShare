# LocalShare - Automated GitHub Release & Asset Uploader
# Usage: powershell -File .\publish-github-release.ps1 [-Version 1.4.0] [-GitHubToken "your_pat_token"]

param(
    [string]$Version = "1.4.0",
    [string]$GitHubToken = $env:GITHUB_TOKEN
)

$ErrorActionPreference = "Stop"
$RepoOwner = "razorisuru"
$RepoName = "LocalShare"

# Automatically parse .env file if GITHUB_TOKEN is not passed directly
if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    $EnvFile = Join-Path (Get-Location) ".env"
    if (Test-Path $EnvFile) {
        Get-Content $EnvFile | ForEach-Object {
            if ($_ -match '^\s*GITHUB_TOKEN\s*=\s*(.*)\s*$') {
                $GitHubToken = $matches[1].Trim('"', "'", ' ')
            }
        }
    }
}

$Tag = "v$($Version.TrimStart('v', 'V'))"
$InstallerFile = Join-Path (Get-Location) "dist\installer\LocalShare_Setup_$Tag.exe"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " 🚀 GitHub Release Uploader for $RepoOwner/$RepoName ($Tag)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not (Test-Path $InstallerFile)) {
    Write-Host "❌ Installer file not found at: $InstallerFile" -ForegroundColor Red
    Write-Host "   Please run 'powershell -File .\build-release.ps1 -Version $Version' first." -ForegroundColor Yellow
    exit 1
}

# Method A: Try GitHub CLI (gh) if installed
if (Get-Command gh -ErrorAction SilentlyContinue) {
    Write-Host "`n[Method A] Publishing release using GitHub CLI (gh)..." -ForegroundColor Yellow
    gh release create $Tag $InstallerFile --title "LocalShare $Tag" --notes "Release $Tag with automated installer, dark glass UI, and live update support."
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n🎉 SUCCESS! Release published on GitHub:" -ForegroundColor Green
        Write-Host "   https://github.com/$RepoOwner/$RepoName/releases/tag/$Tag" -ForegroundColor White
        exit 0
    }
}

# Method B: Use GitHub REST API with Personal Access Token (PAT)
if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    Write-Host "`n⚠️ GitHub Token (PAT) not found in environment or .env file." -ForegroundColor Yellow
    Write-Host "Please add GITHUB_TOKEN=your_token in .env or run:" -ForegroundColor White
    Write-Host "   powershell -File .\publish-github-release.ps1 -Version $Version -GitHubToken `"YOUR_TOKEN`"" -ForegroundColor Cyan
    exit 1
}

Write-Host "`n[Method B] Publishing release via GitHub REST API..." -ForegroundColor Yellow

$Headers = @{
    "Authorization" = "token $GitHubToken"
    "Accept" = "application/vnd.github.v3+json"
    "User-Agent" = "LocalShare-ReleaseBuilder"
}

# 1. Create or fetch GitHub release
$ReleaseUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases"
$ReleaseBody = @{
    tag_name = $Tag
    target_commitish = "main"
    name = "LocalShare $Tag"
    body = "Release $Tag with self-contained installer, dark glass UI, multi-peer streaming, and live software updater."
    draft = $false
    prerelease = $false
} | ConvertTo-Json

try {
    Write-Host "Creating GitHub release tag $Tag..." -ForegroundColor Cyan
    $ReleaseResp = Invoke-RestMethod -Uri $ReleaseUrl -Method Post -Headers $Headers -Body $ReleaseBody -ContentType "application/json"
} catch {
    Write-Host "Release tag $Tag already exists, fetching existing release info..." -ForegroundColor Yellow
    $ReleaseResp = Invoke-RestMethod -Uri "https://api.github.com/repos/$RepoOwner/$RepoName/releases/tags/$Tag" -Headers $Headers
}

$UploadUrl = $ReleaseResp.upload_url -replace '\{\?name,label\}', "?name=LocalShare_Setup_$Tag.exe"

# Delete existing asset if present to allow overwrite
if ($ReleaseResp.assets) {
    foreach ($asset in $ReleaseResp.assets) {
        if ($asset.name -eq "LocalShare_Setup_$Tag.exe") {
            Write-Host "Overwriting existing asset LocalShare_Setup_$Tag.exe on GitHub..." -ForegroundColor Yellow
            try {
                Invoke-RestMethod -Uri $asset.url -Method Delete -Headers $Headers
            } catch {}
        }
    }
}

# 2. Upload asset binary
Write-Host "Uploading installer asset LocalShare_Setup_$Tag.exe to GitHub Releases..." -ForegroundColor Cyan
$UploadHeaders = @{
    "Authorization" = "token $GitHubToken"
    "Accept" = "application/vnd.github.v3+json"
    "User-Agent" = "LocalShare-ReleaseBuilder"
}

$FileBytes = [System.IO.File]::ReadAllBytes($InstallerFile)
$AssetResp = Invoke-RestMethod -Uri $UploadUrl -Method Post -Headers $UploadHeaders -Body $FileBytes -ContentType "application/octet-stream"

Write-Host "`n🎉 SUCCESS! Installer binary uploaded to GitHub Releases:" -ForegroundColor Green
Write-Host "   https://github.com/$RepoOwner/$RepoName/releases/tag/$Tag" -ForegroundColor White
