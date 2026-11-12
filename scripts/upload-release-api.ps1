$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$DistRoot = Join-Path $RepoRoot "dist"
$NotesPath = Join-Path $RepoRoot "RELEASE.md"

Write-Host "=== Mempublikasikan GitHub Release v1.2.0 via GitHub API ===" -ForegroundColor Cyan

# 1. Dapatkan token dningratx dari Git Credential Manager
$lines = @('protocol=https', 'host=github.com', 'username=dningratx') | git credential fill
$token = ""
foreach ($l in $lines) {
    if ($l.StartsWith("password=")) {
        $token = $l.Substring(9).Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Error "Token autentikasi GitHub tidak ditemukan di Credential Manager."
}

$headers = @{
    "Authorization" = "Bearer $token"
    "User-Agent" = "DraGon-Release-Bot"
    "Accept" = "application/vnd.github.v3+json"
}

# 2. Siapkan Release Body dari RELEASE.md
$bodyContent = if (Test-Path $NotesPath) {
    [System.IO.File]::ReadAllText($NotesPath, [System.Text.Encoding]::UTF8)
} else {
    "Rilis DraGon v1.2.0 dengan Net Installer modular dan unduhan komponen on-demand."
}

$releasePayload = @{
    tag_name = "v1.2.0"
    target_commitish = "main"
    name = "DraGon v1.2.0 — Net Installer Modular & Unduhan Komponen On-Demand"
    body = $bodyContent
    draft = $false
    prerelease = $false
} | ConvertTo-Json -Depth 5

Write-Host "1. Membuat GitHub Release v1.2.0..." -ForegroundColor Yellow
$createUrl = "https://api.github.com/repos/dningratx/dragon/releases"

try {
    $release = Invoke-RestMethod -Uri $createUrl -Method Post -Headers $headers -Body $releasePayload -ContentType "application/json; charset=utf-8"
} catch {
    # Jika release sudah ada, ambil datanya
    Write-Host "Release mungkin sudah ada, mencoba mengambil data rilis..." -ForegroundColor Yellow
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/dningratx/dragon/releases/tags/v1.2.0" -Method Get -Headers $headers
}

$releaseId = $release.id
Write-Host "Release berhasil dibuat / ditemukan (ID: $releaseId)!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Cyan

# 3. Unggah file aset (DraGon-Setup.exe, DraGon-win-x64.zip, DraGon-Patch-v1.2.0.zip)
$filesToUpload = @(
    (Join-Path $DistRoot "DraGon-Setup.exe"),
    (Join-Path $DistRoot "DraGon-win-x64.zip"),
    (Join-Path $DistRoot "patch\DraGon-Patch-v1.2.0.zip")
)

Add-Type -AssemblyName System.Net.Http
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromMinutes(15)
$httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer $token")
$httpClient.DefaultRequestHeaders.Add("User-Agent", "DraGon-Release-Bot")
$httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json")

foreach ($filePath in $filesToUpload) {
    if (-not (Test-Path $filePath)) {
        Write-Warning "File tidak ditemukan: $filePath"
        continue
    }

    $fileName = Split-Path -Leaf $filePath
    $fileItem = Get-Item $filePath
    $fileBytes = $fileItem.Length
    $mbSize = [math]::Round($fileBytes / 1MB, 1)

    Write-Host "2. Mengunggah aset: $fileName ($mbSize MB)..." -ForegroundColor Yellow

    # Cek apakah asset dengan nama yang sama sudah ada, jika ada hapus dulu
    if ($release.assets) {
        $existing = $release.assets | Where-Object { $_.name -eq $fileName }
        if ($existing) {
            Write-Host "   -> Menghapus aset lama yang sudah ada di rilis..." -ForegroundColor Gray
            $delUrl = "https://api.github.com/repos/dningratx/dragon/releases/assets/$($existing.id)"
            try {
                $null = Invoke-RestMethod -Uri $delUrl -Method Delete -Headers $headers
            } catch {}
        }
    }

    $uploadUrl = "https://uploads.github.com/repos/dningratx/dragon/releases/$releaseId/assets?name=$fileName"
    
    $fileStream = [System.IO.File]::OpenRead($filePath)
    $streamContent = [System.Net.Http.StreamContent]::new($fileStream)
    $streamContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/octet-stream")

    try {
        $response = $httpClient.PostAsync($uploadUrl, $streamContent).GetAwaiter().GetResult()
        if ($response.IsSuccessStatusCode) {
            Write-Host "   -> Berhasil diunggah: $fileName" -ForegroundColor Green
        } else {
            $respBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            Write-Warning "   -> Gagal mengunggah $fileName : $($response.StatusCode) - $respBody"
        }
    } finally {
        $fileStream.Dispose()
        $streamContent.Dispose()
    }
}

$httpClient.Dispose()
Write-Host "=== Selesai! Release v1.2.0 telah sepenuhnya dipublikasikan ===" -ForegroundColor Green
Write-Host "Kunjungi: $($release.html_url)" -ForegroundColor Cyan
