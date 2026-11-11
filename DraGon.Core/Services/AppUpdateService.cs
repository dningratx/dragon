using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed class AppUpdateService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppLogger _logger;

    public const string DefaultManifestFileName = "update-manifest.json";

    public AppUpdateService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<AppUpdateCheckResult> CheckForUpdateAsync(
        string? feedUrlOrPath = null,
        string? currentVersionOverride = null,
        CancellationToken cancellationToken = default)
    {
        var currentVersionStr = currentVersionOverride ?? DraGonPaths.CurrentAppVersionString;
        _logger.Info("Update", $"Memeriksa pembaruan aplikasi... (Versi saat ini: {currentVersionStr})");

        var resolvedSource = ResolveUpdateSource(feedUrlOrPath);
        if (string.IsNullOrWhiteSpace(resolvedSource))
        {
            return new AppUpdateCheckResult
            {
                HasUpdate = false,
                CurrentVersion = currentVersionStr,
                LatestVersion = currentVersionStr,
                Message = "Tidak ada URL atau file manifes pembaruan yang dikonfigurasi."
            };
        }

        try
        {
            AppUpdateManifest? manifest = null;
            if (resolvedSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                resolvedSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var response = await SharedHttpClient.GetAsync(
                    resolvedSource,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warn("Update", $"Gagal mengunduh manifes pembaruan: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    return new AppUpdateCheckResult
                    {
                        HasUpdate = false,
                        CurrentVersion = currentVersionStr,
                        Message = $"Server pembaruan merespons status HTTP {(int)response.StatusCode}."
                    };
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                manifest = await JsonSerializer.DeserializeAsync<AppUpdateManifest>(stream, JsonOptions, cancellationToken);
            }
            else if (File.Exists(resolvedSource))
            {
                await using var stream = File.OpenRead(resolvedSource);
                manifest = await JsonSerializer.DeserializeAsync<AppUpdateManifest>(stream, JsonOptions, cancellationToken);
            }
            else
            {
                return new AppUpdateCheckResult
                {
                    HasUpdate = false,
                    CurrentVersion = currentVersionStr,
                    Message = $"File sumber pembaruan '{resolvedSource}' tidak ditemukan."
                };
            }

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return new AppUpdateCheckResult
                {
                    HasUpdate = false,
                    CurrentVersion = currentVersionStr,
                    Message = "Format data manifes pembaruan tidak valid atau kosong."
                };
            }

            var hasUpdate = IsNewerVersion(currentVersionStr, manifest.Version);
            var isPatchAvailable = false;

            if (hasUpdate)
            {
                var supportsPatch = manifest.UpdateType == UpdatePackageType.Patch &&
                                    !string.IsNullOrWhiteSpace(manifest.PatchPackageUrl);

                var meetsMinVersion = string.IsNullOrWhiteSpace(manifest.MinimumSupportedVersion) ||
                                      !IsNewerVersion(currentVersionStr, manifest.MinimumSupportedVersion);

                isPatchAvailable = supportsPatch && meetsMinVersion;

                var updateTypeLabel = isPatchAvailable
                    ? $"Pembaruan Kecil / Patch ({manifest.FormattedPatchSize})"
                    : $"Pembaruan Penuh / Full ({manifest.FormattedFullSize})";

                _logger.Info("Update", $"Pembaruan ditemukan! Versi baru: {manifest.Version} ({updateTypeLabel})");
            }
            else
            {
                _logger.Info("Update", $"Aplikasi DraGon versi {currentVersionStr} sudah merupakan versi terbaru.");
            }

            return new AppUpdateCheckResult
            {
                HasUpdate = hasUpdate,
                IsPatchAvailable = isPatchAvailable,
                CurrentVersion = currentVersionStr,
                LatestVersion = manifest.Version,
                Manifest = manifest,
                Message = hasUpdate
                    ? $"Versi baru {manifest.Version} tersedia! ({manifest.Title})"
                    : $"DraGon sudah menggunakan versi terbaru (v{currentVersionStr})."
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Update", $"Terjadi kesalahan saat memeriksa pembaruan: {ex.Message}", ex);
            return new AppUpdateCheckResult
            {
                HasUpdate = false,
                CurrentVersion = currentVersionStr,
                Message = $"Gagal memeriksa pembaruan: {ex.Message}"
            };
        }
    }

    public async Task<(bool Success, string Message, string DownloadedFilePath)> DownloadPatchAsync(
        AppUpdateManifest manifest,
        IProgress<(double Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifest.PatchPackageUrl))
        {
            return (false, "URL paket patch tidak ditentukan dalam manifes.", string.Empty);
        }

        DraGonPaths.EnsureDirectoriesExist();
        var destinationDir = DraGonPaths.UpdatesDirectory;
        Directory.CreateDirectory(destinationDir);

        var patchFileName = $"DraGon-Patch-v{manifest.Version}.zip";
        var targetZipPath = Path.Combine(destinationDir, patchFileName);

        try
        {
            var sourceUrl = manifest.PatchPackageUrl.Trim();

            // Jika URL adalah file lokal / direktori
            if (File.Exists(sourceUrl))
            {
                progress?.Report((20, "Menyalin paket patch lokal..."));
                File.Copy(sourceUrl, targetZipPath, overwrite: true);
                progress?.Report((100, "Paket patch lokal siap."));
            }
            else
            {
                progress?.Report((5, $"Menghubungi server untuk mengunduh patch ({manifest.FormattedPatchSize})..."));
                _logger.Info("Update", $"Mengunduh patch dari {sourceUrl} ke {targetZipPath}");

                using var response = await SharedHttpClient.GetAsync(
                    sourceUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? manifest.PatchSizeBytes;
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(targetZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        var pct = Math.Min(98.0, (double)totalRead / totalBytes * 95.0 + 5.0);
                        progress?.Report((pct, $"Mengunduh patch: {pct:0}% ({totalRead / 1024} KB / {totalBytes / 1024} KB)"));
                    }
                }

                progress?.Report((98, "Memverifikasi integritas berkas patch..."));
            }

            // Validasi Checksum jika tersedia
            if (!string.IsNullOrWhiteSpace(manifest.PatchSha256))
            {
                var calculatedHash = await CalculateSha256Async(targetZipPath);
                if (!string.Equals(calculatedHash, manifest.PatchSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Warn("Update", $"Checksum SHA-256 tidak cocok! Diharapkan: {manifest.PatchSha256}, Aktual: {calculatedHash}");
                    return (false, "Integritas berkas patch gagal diverifikasi (Checksum SHA-256 tidak cocok).", targetZipPath);
                }
            }

            progress?.Report((100, $"Paket patch v{manifest.Version} berhasil diunduh."));
            _logger.Info("Update", $"Patch v{manifest.Version} berhasil diunduh ke {targetZipPath}");
            return (true, $"Patch v{manifest.Version} siap dipasang.", targetZipPath);
        }
        catch (Exception ex)
        {
            _logger.Error("Update", $"Gagal mengunduh paket patch: {ex.Message}", ex);
            return (false, $"Gagal mengunduh patch: {ex.Message}", string.Empty);
        }
    }

    public int ExtractPatchDirectly(string patchZipPath, string targetDirectory)
    {
        if (!File.Exists(patchZipPath))
        {
            throw new FileNotFoundException("Berkas patch zip tidak ditemukan.", patchZipPath);
        }

        Directory.CreateDirectory(targetDirectory);
        var updatedCount = 0;

        using var archive = ZipFile.OpenRead(patchZipPath);
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
            if (!destinationPath.StartsWith(Path.GetFullPath(targetDirectory), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var parentDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
            updatedCount++;
        }

        _logger.Info("Update", $"Patch berhasil diekstrak ke '{targetDirectory}'. Total {updatedCount} berkas diperbarui.");
        return updatedCount;
    }

    public (bool Success, string Message) PrepareAndLaunchPatchUpdater(
        string patchZipPath,
        string targetDirectory,
        int currentProcessId,
        bool restartApp = true)
    {
        try
        {
            var updatesDir = DraGonPaths.UpdatesDirectory;
            Directory.CreateDirectory(updatesDir);

            var scriptPath = Path.Combine(updatesDir, "apply-patch.ps1");
            var scriptContent = new StringBuilder();
            scriptContent.AppendLine("param(");
            scriptContent.AppendLine("    [int]$ProcessId,");
            scriptContent.AppendLine("    [string]$ZipPath,");
            scriptContent.AppendLine("    [string]$TargetDir,");
            scriptContent.AppendLine("    [switch]$Restart");
            scriptContent.AppendLine(")");
            scriptContent.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            scriptContent.AppendLine("if ($ProcessId -gt 0) {");
            scriptContent.AppendLine("    try {");
            scriptContent.AppendLine("        $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue");
            scriptContent.AppendLine("        if ($p) { $p.WaitForExit(10000) }");
            scriptContent.AppendLine("    } catch {}");
            scriptContent.AppendLine("}");
            scriptContent.AppendLine("Start-Sleep -Milliseconds 800");
            scriptContent.AppendLine("Expand-Archive -Path $ZipPath -DestinationPath $TargetDir -Force");
            scriptContent.AppendLine("if ($Restart) {");
            scriptContent.AppendLine("    $exe = Join-Path $TargetDir 'DraGon.App.exe'");
            scriptContent.AppendLine("    if (Test-Path $exe) {");
            scriptContent.AppendLine("        Start-Process -FilePath $exe");
            scriptContent.AppendLine("    }");
            scriptContent.AppendLine("}");

            File.WriteAllText(scriptPath, scriptContent.ToString(), Encoding.UTF8);

            var restartFlag = restartApp ? "-Restart" : "";
            var escapedZip = patchZipPath.Replace("'", "''");
            var escapedTarget = targetDirectory.Replace("'", "''");
            var arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ProcessId {currentProcessId} -ZipPath '{escapedZip}' -TargetDir '{escapedTarget}' {restartFlag}".Trim();

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            _logger.Info("Update", $"Updater script diluncurkan. Menyiapkan penggantian berkas patch dan restart DraGon.");
            return (true, "Proses update sedang dipasang. Aplikasi akan menutup dan terbuka kembali secara otomatis.");
        }
        catch (Exception ex)
        {
            _logger.Error("Update", $"Gagal menyiapkan script updater: {ex.Message}", ex);
            return (false, $"Gagal meluncurkan updater: {ex.Message}");
        }
    }

    public static bool IsNewerVersion(string currentVersionStr, string candidateVersionStr)
    {
        if (string.Equals(currentVersionStr.Trim(), candidateVersionStr.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var current = NormalizeVersion(currentVersionStr);
        var candidate = NormalizeVersion(candidateVersionStr);

        return candidate > current;
    }

    public static Version NormalizeVersion(string versionStr)
    {
        var clean = versionStr.Trim().TrimStart('v', 'V');
        var dashIndex = clean.IndexOf('-');
        if (dashIndex > 0)
        {
            clean = clean[..dashIndex];
        }

        var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var major = parts.Length > 0 && int.TryParse(parts[0], out var ma) ? ma : 1;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
        var build = parts.Length > 2 && int.TryParse(parts[2], out var bu) ? bu : 0;
        var revision = parts.Length > 3 && int.TryParse(parts[3], out var re) ? re : 0;

        return new Version(major, minor, build, revision);
    }

    public static string CreateSamplePatchPackage(string destinationZipPath, IDictionary<string, string> sampleFiles)
    {
        var tempDir = Path.Combine(DraGonPaths.TempDirectory, $"patch_create_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var kv in sampleFiles)
            {
                var filePath = Path.Combine(tempDir, kv.Key);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, kv.Value, Encoding.UTF8);
            }

            var destDir = Path.GetDirectoryName(destinationZipPath);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            if (File.Exists(destinationZipPath))
            {
                File.Delete(destinationZipPath);
            }

            ZipFile.CreateFromDirectory(tempDir, destinationZipPath, CompressionLevel.Optimal, false);
            return destinationZipPath;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup error
            }
        }
    }

    private static async Task<string> CalculateSha256Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }

    private string? ResolveUpdateSource(string? customSource)
    {
        if (!string.IsNullOrWhiteSpace(customSource))
        {
            return customSource.Trim();
        }

        var localConfigManifest = Path.Combine(DraGonPaths.ConfigDirectory, DefaultManifestFileName);
        if (File.Exists(localConfigManifest))
        {
            return localConfigManifest;
        }

        var appDirManifest = Path.Combine(DraGonPaths.ResolveEffectiveApplicationDirectory(), DefaultManifestFileName);
        if (File.Exists(appDirManifest))
        {
            return appDirManifest;
        }

        // Default remote manifest GitHub release endpoint
        return "https://raw.githubusercontent.com/username/dragon/main/releases/update-manifest.json";
    }
}
