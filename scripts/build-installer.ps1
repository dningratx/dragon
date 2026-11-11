param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$DistRoot = Join-Path $RepoRoot "dist"
$StagingDir = Join-Path $DistRoot "payload-staging"
$PayloadDir = Join-Path $RepoRoot "DraGon.Installer\Payload"
$PayloadZip = Join-Path $PayloadDir "DraGonPayload.zip"
$InstallerOutDir = Join-Path $DistRoot "installer"

Write-Host "=== 1. Menyiapkan Direktori Build Installer DraGon ===" -ForegroundColor Cyan

Get-Process -Name "DraGon-Setup", "DraGon.App", "DraGon.Elevator" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

if (Test-Path $StagingDir) {
    Remove-Item $StagingDir -Recurse -Force
}
if (Test-Path $PayloadZip) {
    Remove-Item $PayloadZip -Force
}
if (Test-Path $InstallerOutDir) {
    Remove-Item $InstallerOutDir -Recurse -Force
}

New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null
New-Item -ItemType Directory -Path $PayloadDir -Force | Out-Null
New-Item -ItemType Directory -Path $InstallerOutDir -Force | Out-Null

Write-Host "=== 2. Mempublikasikan DraGon.Elevator & DraGon.App (Self-Contained Single-File) ===" -ForegroundColor Yellow
$ElevatorStaging = Join-Path $DistRoot "staging-elevator"
$AppStaging = Join-Path $DistRoot "staging-app"
if (Test-Path $ElevatorStaging) { Remove-Item $ElevatorStaging -Recurse -Force }
if (Test-Path $AppStaging) { Remove-Item $AppStaging -Recurse -Force }

dotnet publish (Join-Path $RepoRoot "DraGon.Elevator\DraGon.Elevator.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $ElevatorStaging

dotnet publish (Join-Path $RepoRoot "DraGon.App\DraGon.App.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $AppStaging

Copy-Item (Join-Path $ElevatorStaging "DraGon.Elevator.exe") -Destination (Join-Path $StagingDir "DraGon.Elevator.exe") -Force
Copy-Item (Join-Path $AppStaging "DraGon.App.exe") -Destination (Join-Path $StagingDir "DraGon.App.exe") -Force
Copy-Item (Join-Path $RepoRoot "README.md") -Destination (Join-Path $StagingDir "README.md") -Force

Remove-Item $ElevatorStaging -Recurse -Force
Remove-Item $AppStaging -Recurse -Force

Write-Host "=== 3. Menyertakan Paket Runtime Esensial (Ramping & Optimal) ===" -ForegroundColor Yellow
$LocalRuntimes = Join-Path $env:LOCALAPPDATA "DraGon\runtimes"
$StagingRuntimes = Join-Path $StagingDir "runtimes"

if (Test-Path $LocalRuntimes) {
    New-Item -ItemType Directory -Path $StagingRuntimes -Force | Out-Null

    # 1. Apache HTTP Server (Web Server Utama)
    $srcApache = Join-Path $LocalRuntimes "apache"
    if (Test-Path $srcApache) {
        Write-Host "  -> Menyertakan Apache HTTP Server..." -ForegroundColor Cyan
        Copy-Item $srcApache -Destination (Join-Path $StagingRuntimes "apache") -Recurse -Force
    }

    # 2. PHP Runtime (Satu versi utama: PHP 8.3 LTS)
    $srcPhp = Join-Path $LocalRuntimes "php"
    if (Test-Path $srcPhp) {
        Write-Host "  -> Menyertakan PHP 8.3 (Satu versi utama stabil)..." -ForegroundColor Cyan
        $dstPhp = Join-Path $StagingRuntimes "php"
        New-Item -ItemType Directory -Path $dstPhp -Force | Out-Null
        $phpCandidate = Join-Path $srcPhp "php-8.3.36"
        if (-not (Test-Path $phpCandidate)) {
            $phpCandidate = (Get-ChildItem $srcPhp -Directory | Select-Object -First 1).FullName
        }
        if ($phpCandidate -and (Test-Path $phpCandidate)) {
            Copy-Item $phpCandidate -Destination (Join-Path $dstPhp (Split-Path -Leaf $phpCandidate)) -Recurse -Force
        }
    }

    # 3. MariaDB Database Server (Dibersihkan dari data lokal & debug pdb)
    $srcMaria = Join-Path $LocalRuntimes "mariadb"
    if (Test-Path $srcMaria) {
        Write-Host "  -> Menyertakan MariaDB Database Server (bersih)..." -ForegroundColor Cyan
        $dstMaria = Join-Path $StagingRuntimes "mariadb"
        New-Item -ItemType Directory -Path $dstMaria -Force | Out-Null
        $mariaCandidate = (Get-ChildItem $srcMaria -Directory | Select-Object -First 1).FullName
        if ($mariaCandidate -and (Test-Path $mariaCandidate)) {
            $mariaTarget = Join-Path $dstMaria (Split-Path -Leaf $mariaCandidate)
            New-Item -ItemType Directory -Path $mariaTarget -Force | Out-Null
            Get-ChildItem $mariaCandidate | Where-Object { $_.Name -ne "data" } | ForEach-Object {
                Copy-Item $_.FullName -Destination $mariaTarget -Recurse -Force
            }
            Get-ChildItem $mariaTarget -Recurse -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force
        }
    }

    # 4. Composer Dependency Manager
    $srcComp = Join-Path $LocalRuntimes "composer"
    if (Test-Path $srcComp) {
        Write-Host "  -> Menyertakan Composer..." -ForegroundColor Cyan
        Copy-Item $srcComp -Destination (Join-Path $StagingRuntimes "composer") -Recurse -Force
    }

    # 5. Node.js Runtime
    $srcNode = Join-Path $LocalRuntimes "nodejs"
    if (Test-Path $srcNode) {
        Write-Host "  -> Menyertakan Node.js & npm..." -ForegroundColor Cyan
        Copy-Item $srcNode -Destination (Join-Path $StagingRuntimes "nodejs") -Recurse -Force
    }

    # 6. phpMyAdmin Web GUI
    $srcPma = Join-Path $LocalRuntimes "phpmyadmin"
    if (Test-Path $srcPma) {
        Write-Host "  -> Menyertakan phpMyAdmin..." -ForegroundColor Cyan
        Copy-Item $srcPma -Destination (Join-Path $StagingRuntimes "phpmyadmin") -Recurse -Force
    }

    # 7. Nginx Web Server
    $srcNginx = Join-Path $LocalRuntimes "nginx"
    if (-not (Test-Path $srcNginx)) { $srcNginx = "C:\DraGon\runtimes\nginx" }
    if (Test-Path $srcNginx) {
        Write-Host "  -> Menyertakan Nginx Web Server..." -ForegroundColor Cyan
        Copy-Item $srcNginx -Destination (Join-Path $StagingRuntimes "nginx") -Recurse -Force
    }
}

Write-Host "=== 4. Mengemas Payload Instalasi Ramping (DraGonPayload.zip) ===" -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($StagingDir, $PayloadZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host "=== 5. Membangun Single-File GUI Installer Mandiri (Self-Contained DraGon-Setup.exe) ===" -ForegroundColor Yellow
dotnet publish (Join-Path $RepoRoot "DraGon.Installer\DraGon.Installer.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $InstallerOutDir

$FinalSetupExe = Join-Path $DistRoot "DraGon-Setup.exe"
Copy-Item (Join-Path $InstallerOutDir "DraGon-Setup.exe") -Destination $FinalSetupExe -Force

Remove-Item $StagingDir -Recurse -Force

Write-Host "=== 6. Menyiapkan Folder Runtimes di Direktori Distribusi ===" -ForegroundColor Yellow
$LocalRuntimes = Join-Path $env:LOCALAPPDATA "DraGon\runtimes"
$DistRuntimes = Join-Path $DistRoot "runtimes"
if (Test-Path $LocalRuntimes) {
    if (-not (Test-Path $DistRuntimes)) {
        try {
            $null = cmd /c mklink /J "$DistRuntimes" "$LocalRuntimes"
            Write-Host "Tautan junction folder runtimes berhasil dibuat di: $DistRuntimes" -ForegroundColor Cyan
        } catch {
            Write-Host "Menyalin folder runtimes ke direktori distribusi..." -ForegroundColor Cyan
            Copy-Item $LocalRuntimes -Destination $DistRuntimes -Recurse -Force
        }
    } else {
        Write-Host "Folder runtimes di direktori distribusi sudah siap." -ForegroundColor Cyan
    }
}

Write-Host "=== Selesai! Installer tersedia di: $FinalSetupExe ===" -ForegroundColor Green
Get-Item $FinalSetupExe | Select-Object FullName, Length, LastWriteTime
