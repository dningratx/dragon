param(
    [string]$Version = "1.0.1",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Changelog = "Pembaruan inkremental perbaikan bug dan peningkatan performa."
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$DistRoot = Join-Path $RepoRoot "dist"
$PatchOutDir = Join-Path $DistRoot "patch"
$PatchStaging = Join-Path $DistRoot "staging-patch"
$PatchZip = Join-Path $PatchOutDir "DraGon-Patch-v$Version.zip"
$ManifestJson = Join-Path $PatchOutDir "update-manifest.json"

Write-Host "=== Membangun Paket Patch Inkremental DraGon (v$Version) ===" -ForegroundColor Cyan

if (Test-Path $PatchStaging) { Remove-Item $PatchStaging -Recurse -Force }
if (-not (Test-Path $PatchOutDir)) { New-Item -ItemType Directory -Path $PatchOutDir -Force | Out-Null }
if (Test-Path $PatchZip) { Remove-Item $PatchZip -Force }

New-Item -ItemType Directory -Path $PatchStaging -Force | Out-Null

$AppStaging = Join-Path $DistRoot "staging-app-patch"
if (Test-Path $AppStaging) { Remove-Item $AppStaging -Recurse -Force }

Write-Host "1. Mempublikasikan binary aplikasi terbaru..." -ForegroundColor Yellow
dotnet publish (Join-Path $RepoRoot "DraGon.App\DraGon.App.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $AppStaging

Copy-Item (Join-Path $AppStaging "DraGon.App.exe") -Destination (Join-Path $PatchStaging "DraGon.App.exe") -Force
Remove-Item $AppStaging -Recurse -Force

Write-Host "2. Mengemas patch ringan menjadi ZIP..." -ForegroundColor Yellow
Compress-Archive -Path "$PatchStaging\*" -DestinationPath $PatchZip -Force
Remove-Item $PatchStaging -Recurse -Force

$zipItem = Get-Item $PatchZip
$zipBytes = $zipItem.Length
$sha256 = (Get-FileHash -Path $PatchZip -Algorithm SHA256).Hash.ToLowerInvariant()

Write-Host "3. Menghasilkan update-manifest.json..." -ForegroundColor Yellow
$manifestObject = [PSCustomObject]@{
    version = $Version
    minimumSupportedVersion = "1.0.0"
    releaseDate = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    title = "DraGon v$Version (Patch Update)"
    changelog = $Changelog
    updateType = "Patch"
    patchPackageUrl = "DraGon-Patch-v$Version.zip"
    patchSizeBytes = $zipBytes
    patchSha256 = $sha256
    fullPackageUrl = "DraGon-Setup.exe"
    fullSizeBytes = 90000000
    changedFiles = @("DraGon.App.exe")
    isMandatory = $false
}

$manifestContent = $manifestObject | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($ManifestJson, $manifestContent, [System.Text.Encoding]::UTF8)

Write-Host "=== Selesai! Paket Patch Berhasil Dibuat ===" -ForegroundColor Green
Write-Host "File Patch: $PatchZip ($([math]::Round($zipBytes / 1KB, 1)) KB)" -ForegroundColor Green
Write-Host "Manifest:   $ManifestJson" -ForegroundColor Green
Write-Host "SHA-256:    $sha256" -ForegroundColor Green
