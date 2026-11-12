param(
    [string]$Tag = "v1.2.0",
    [string]$Title = "DraGon v1.2.0 — Net Installer Modular & Unduhan Komponen On-Demand",
    [string]$NotesFile = "RELEASE.md"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$DistRoot = Join-Path $RepoRoot "dist"
$SetupExe = Join-Path $DistRoot "DraGon-Setup.exe"
$PortableZip = Join-Path $DistRoot "DraGon-win-x64.zip"
$PatchZip = Join-Path $DistRoot "patch\DraGon-Patch-$Tag.zip"
$ManifestJson = Join-Path $DistRoot "patch\update-manifest.json"
$NotesPath = Join-Path $RepoRoot $NotesFile

Write-Host "=== Mempublikasikan GitHub Release DraGon ($Tag) ===" -ForegroundColor Cyan

if (-not (Test-Path $SetupExe)) {
    Write-Error "Berkas installer '$SetupExe' tidak ditemukan. Jalankan .\scripts\build-installer.ps1 terlebih dahulu."
}

# Cek apakah GitHub CLI sudah terautentikasi
$ghInstalled = (Get-Command gh -ErrorAction SilentlyContinue) -ne $null
$ghLoggedIn = $false

if ($ghInstalled) {
    try {
        $null = gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            $ghLoggedIn = $true
        }
    } catch {}
}

if ($ghLoggedIn) {
    Write-Host "-> Menggunakan GitHub CLI (gh) untuk membuat Release dan mengunggah aset..." -ForegroundColor Yellow
    $assets = @($SetupExe, $PortableZip)
    if (Test-Path $PatchZip) { $assets += $PatchZip }
    if (Test-Path $ManifestJson) { $assets += $ManifestJson }

    gh release create $Tag $assets --title $Title --notes-file $NotesPath
    Write-Host "=== Berhasil! Release $Tag telah dipublikasikan di GitHub ===" -ForegroundColor Green
    Write-Host "URL: https://github.com/dningratx/dragon/releases/tag/$Tag" -ForegroundColor Cyan
} else {
    Write-Host "-> GitHub CLI belum terautentikasi (gh auth login belum dijalankan)." -ForegroundColor Yellow
    Write-Host "-> Membuka halaman GitHub New Release di browser..." -ForegroundColor Cyan

    $newReleaseUrl = "https://github.com/dningratx/dragon/releases/new?tag=$Tag"
    Start-Process $newReleaseUrl

    Write-Host "-> Membuka folder berkas rilis (dist) di File Explorer..." -ForegroundColor Cyan
    Start-Process "explorer.exe" -ArgumentList "/select,`"$SetupExe`""

    Write-Host ""
    Write-Host "Langkah Mudah di Browser GitHub:" -ForegroundColor Green
    Write-Host "1. Judul Rilis: $Title" -ForegroundColor White
    Write-Host "2. Catatan Rilis: Salin isi berkas RELEASE.md" -ForegroundColor White
    Write-Host "3. Drag & Drop berkas berikut ke kotak Attach binaries:" -ForegroundColor White
    Write-Host "   - $SetupExe" -ForegroundColor Cyan
    Write-Host "   - $PortableZip" -ForegroundColor Cyan
    if (Test-Path $PatchZip) { Write-Host "   - $PatchZip" -ForegroundColor Cyan }
    Write-Host "4. Klik tombol 'Publish release'." -ForegroundColor Green
}
