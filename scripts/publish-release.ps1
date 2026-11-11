param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$DistRoot = Join-Path $RepoRoot "dist"
$PublishDir = Join-Path $DistRoot "DraGon-$Runtime"
$ZipPath = Join-Path $DistRoot "DraGon-$Runtime.zip"

Write-Host "=== Membangun Paket Distribusi DraGon ($Configuration | $Runtime) ===" -ForegroundColor Cyan

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

$scFlag = if ($SelfContained) { "true" } else { "false" }

$ElevatorStaging = Join-Path $DistRoot "staging-elevator-rel"
$AppStaging = Join-Path $DistRoot "staging-app-rel"
if (Test-Path $ElevatorStaging) { Remove-Item $ElevatorStaging -Recurse -Force }
if (Test-Path $AppStaging) { Remove-Item $AppStaging -Recurse -Force }

Write-Host "1. Mempublikasikan DraGon.Elevator (Single-File)..." -ForegroundColor Yellow
dotnet publish (Join-Path $RepoRoot "DraGon.Elevator\DraGon.Elevator.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $ElevatorStaging

Write-Host "2. Mempublikasikan DraGon.App (Single-File)..." -ForegroundColor Yellow
dotnet publish (Join-Path $RepoRoot "DraGon.App\DraGon.App.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $AppStaging

Copy-Item (Join-Path $ElevatorStaging "DraGon.Elevator.exe") -Destination (Join-Path $PublishDir "DraGon.Elevator.exe") -Force
Copy-Item (Join-Path $AppStaging "DraGon.App.exe") -Destination (Join-Path $PublishDir "DraGon.App.exe") -Force
Copy-Item (Join-Path $RepoRoot "README.md") -Destination (Join-Path $PublishDir "README.md") -Force

Remove-Item $ElevatorStaging -Recurse -Force
Remove-Item $AppStaging -Recurse -Force

Write-Host "3. Mengemas arsip distribusi ZIP..." -ForegroundColor Yellow
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -Force

Write-Host "Selesai! Paket distribusi tersedia di: $ZipPath" -ForegroundColor Green
