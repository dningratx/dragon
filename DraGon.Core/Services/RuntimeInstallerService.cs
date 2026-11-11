using System.Diagnostics;
using System.IO.Compression;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed class RuntimeInstallerService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private readonly AppLogger _logger;

    public RuntimeInstallerService(AppLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DownloadableVersionPreset> GetPhpVersionPresets() =>
    [
        new("php", "php-8.5.12", "PHP 8.5.12 (VS17 x64 - Terbaru)", "https://windows.php.net/downloads/releases/php-8.5.12-1-Win32-vs17-x64.zip"),
        new("php", "php-8.4.27", "PHP 8.4.27 (VS17 x64 - Stabil)", "https://windows.php.net/downloads/releases/php-8.4.27-1-Win32-vs17-x64.zip"),
        new("php", "php-8.3.36", "PHP 8.3.36 (VS16 x64 - Populer Laravel)", "https://windows.php.net/downloads/releases/php-8.3.36-1-Win32-vs16-x64.zip"),
        new("php", "php-8.2.35", "PHP 8.2.35 (VS16 x64 - Kompatibel)", "https://windows.php.net/downloads/releases/php-8.2.35-1-Win32-vs16-x64.zip"),
        new("php", "php-8.1.34", "PHP 8.1.34 (VS16 x64 - Legacy)", "https://windows.php.net/downloads/releases/php-8.1.34-Win32-vs16-x64.zip"),
        new("php", "php-8.0.30", "PHP 8.0.30 (VS16 x64 - Legacy)", "https://windows.php.net/downloads/releases/php-8.0.30-Win32-vs16-x64.zip")
    ];

    public IReadOnlyList<DownloadableVersionPreset> GetDatabaseVersionPresets() =>
    [
        new("mariadb", "mysql-8.4.11", "MySQL 8.4.11 LTS (Oracle Resmi x64)", "https://cdn.mysql.com/Downloads/MySQL-8.4/mysql-8.4.11-winx64.zip"),
        new("mariadb", "mysql-8.0.45", "MySQL 8.0.45 (Oracle Resmi x64)", "https://cdn.mysql.com/Downloads/MySQL-8.0/mysql-8.0.45-winx64.zip"),
        new("mariadb", "mysql-9.1.0", "MySQL 9.1.0 Innovation (Oracle Resmi x64)", "https://cdn.mysql.com/Downloads/MySQL-9.1/mysql-9.1.0-winx64.zip"),
        new("mariadb", "mariadb-11.8.9", "MariaDB 11.8.9 LTS (Resmi x64)", "https://downloads.mariadb.org/rest-api/mariadb/11.8.9/mariadb-11.8.9-winx64.zip"),
        new("mariadb", "mariadb-11.4.13", "MariaDB 11.4.13 LTS (Resmi x64)", "https://downloads.mariadb.org/rest-api/mariadb/11.4.13/mariadb-11.4.13-winx64.zip"),
        new("mariadb", "mariadb-10.11.19", "MariaDB 10.11.19 LTS (Resmi x64)", "https://downloads.mariadb.org/rest-api/mariadb/10.11.19/mariadb-10.11.19-winx64.zip"),
        new("mariadb", "mariadb-10.6.28", "MariaDB 10.6.28 LTS (Resmi x64)", "https://downloads.mariadb.org/rest-api/mariadb/10.6.28/mariadb-10.6.28-winx64.zip")
    ];

    public async Task<(bool Success, string Message, string InstalledDirectory)> DownloadAndInstallVersionPresetAsync(
        DownloadableVersionPreset preset,
        string runtimeBasePath,
        IProgress<(double Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        DraGonPaths.EnsureDirectoriesExist(baseDir);
        var zipFileName = $"{preset.FolderName}.zip";
        var tempZipPath = Path.Combine(DraGonPaths.DownloadsCacheDirectory, zipFileName);
        var targetFolder = Path.Combine(baseDir, preset.Category, preset.FolderName);
        var stagingFolder = Path.Combine(DraGonPaths.TempDirectory, $"extract_{preset.FolderName}_{Guid.NewGuid():N}");

        try
        {
            progress?.Report((2, $"Menghubungi server resmi untuk mengunduh {preset.DisplayName}..."));
            _logger.Info("Installer", $"Mengunduh versi {preset.DisplayName} dari {preset.DownloadUrl}");

            using var response = await SharedHttpClient.GetAsync(
                preset.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using (var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await networkStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var pct = Math.Clamp((double)totalRead / totalBytes * 75.0, 2.0, 75.0);
                        var mbRead = totalRead / (1024.0 * 1024.0);
                        var mbTotal = totalBytes / (1024.0 * 1024.0);
                        progress?.Report((pct, $"Mengunduh {preset.DisplayName}: {mbRead:F1} MB / {mbTotal:F1} MB..."));
                    }
                    else
                    {
                        var mbRead = totalRead / (1024.0 * 1024.0);
                        progress?.Report((40, $"Mengunduh {preset.DisplayName}: {mbRead:F1} MB..."));
                    }
                }
            }

            progress?.Report((80, $"Mengekstrak {preset.DisplayName} ke {targetFolder}..."));
            await Task.Run(() =>
            {
                Directory.CreateDirectory(stagingFolder);
                ZipFile.ExtractToDirectory(tempZipPath, stagingFolder, overwriteFiles: true);
                cancellationToken.ThrowIfCancellationRequested();

                var subDirs = Directory.GetDirectories(stagingFolder);
                var rootFiles = Directory.GetFiles(stagingFolder);
                var sourceCopyRoot = (subDirs.Length == 1 && rootFiles.Length == 0)
                    ? subDirs[0]
                    : stagingFolder;

                Directory.CreateDirectory(targetFolder);
                CopyDirectoryContents(sourceCopyRoot, targetFolder);
            }, cancellationToken);

            if (string.Equals(preset.Category, "mariadb", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report((92, $"Menginisialisasi database awal untuk {preset.DisplayName}..."));
                await TryInitializeMariaDbDataDirectoryAsync(targetFolder);
            }

            progress?.Report((100, $"{preset.DisplayName} berhasil dipasang dan siap digunakan!"));
            _logger.Info("Installer", $"Versi '{preset.DisplayName}' berhasil dipasang ke: {targetFolder}");
            return (true, $"{preset.DisplayName} berhasil diunduh dan dipasang ke '{targetFolder}'.", targetFolder);
        }
        catch (Exception ex)
        {
            _logger.Error("Installer", $"Gagal mengunduh/memasang {preset.DisplayName}.", ex);
            return (false, $"Gagal mengunduh/memasang {preset.DisplayName}: {ex.Message}", string.Empty);
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingFolder))
                {
                    Directory.Delete(stagingFolder, recursive: true);
                }
            }
            catch
            {
                // Ignore staging cleanup error
            }
        }
    }

    public async Task<(bool Success, string Message)> DownloadAndInstallOfficialArchiveAsync(
        RuntimePackageInfo package,
        string runtimeBasePath,
        IProgress<(double Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadUrl = package.EffectiveDownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return (false, $"Paket '{package.Name}' memerlukan pemilihan versi manual di halaman unduhan resmi, lalu gunakan tombol 'Impor ZIP Resmi'.");
        }

        DraGonPaths.EnsureDirectoriesExist(runtimeBasePath);
        var isPhar = downloadUrl.EndsWith(".phar", StringComparison.OrdinalIgnoreCase);
        var ext = isPhar ? ".phar" : ".zip";
        var zipFileName = $"{package.Id}-official-{DateTime.Now:yyyyMMddHHmmss}{ext}";
        var tempZipPath = Path.Combine(DraGonPaths.DownloadsCacheDirectory, zipFileName);

        try
        {
            var targetVerName = package.SelectedDownloadVersion?.DisplayName ?? package.RecommendedVersion;
            progress?.Report((2, $"Menghubungi server resmi untuk mengunduh {package.Name} ({targetVerName})..."));
            _logger.Info("Installer", $"Mengunduh paket resmi {package.Name} ({targetVerName}) dari {downloadUrl}");

            using var response = await SharedHttpClient.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using (var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await networkStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var pct = Math.Clamp((double)totalRead / totalBytes * 75.0, 2.0, 75.0);
                        var mbRead = totalRead / (1024.0 * 1024.0);
                        var mbTotal = totalBytes / (1024.0 * 1024.0);
                        progress?.Report((pct, $"Mengunduh {mbRead:F1} MB / {mbTotal:F1} MB..."));
                    }
                    else
                    {
                        var mbRead = totalRead / (1024.0 * 1024.0);
                        progress?.Report((40, $"Mengunduh {mbRead:F1} MB..."));
                    }
                }
            }

            if (isPhar)
            {
                var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
                    ? DraGonPaths.DefaultRuntimesDirectory
                    : runtimeBasePath;

                var targetSub = package.SelectedDownloadVersion?.TargetSubFolder;
                var targetFolder = !string.IsNullOrWhiteSpace(targetSub)
                    ? Path.Combine(baseDir, package.ExpectedRelativeDir, targetSub)
                    : Path.Combine(baseDir, package.ExpectedRelativeDir);

                Directory.CreateDirectory(targetFolder);
                var destPhar = Path.Combine(targetFolder, "composer.phar");
                File.Copy(tempZipPath, destPhar, overwrite: true);
                var batPath = Path.Combine(targetFolder, "composer.bat");
                var batContent = "@echo off\r\nif exist \"%~dp0..\\php\\php.exe\" (\r\n  \"%~dp0..\\php\\php.exe\" \"%~dp0composer.phar\" %*\r\n) else (\r\n  php \"%~dp0composer.phar\" %*\r\n)\r\n";
                await File.WriteAllTextAsync(batPath, batContent, cancellationToken);
                progress?.Report((100, "Composer selesai dipasang."));
                _logger.Info("Installer", $"Composer berhasil dipasang di {targetFolder}");
                return (true, $"Paket '{package.Name}' ({targetVerName}) berhasil dipasang ke '{targetFolder}'.");
            }

            return await ImportOfficialZipArchiveAsync(package, tempZipPath, runtimeBasePath, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.Warn("Installer", $"Pengunduhan paket {package.Name} dibatalkan oleh pengguna.");
            return (false, "Pengunduhan dibatalkan.");
        }
        catch (Exception ex)
        {
            _logger.Error("Installer", $"Gagal mengunduh paket {package.Name} dari sumber resmi.", ex);
            return (false, $"Gagal mengunduh dari server resmi ({ex.Message}). Anda juga dapat mengunduh melalui browser lalu klik 'Impor File ZIP Resmi'.");
        }
        finally
        {
            try
            {
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
            catch
            {
                // Ignore temp zip cleanup error
            }
        }
    }

    public async Task<(bool Success, string Message)> ImportOfficialZipArchiveAsync(
        RuntimePackageInfo package,
        string zipFilePath,
        string runtimeBasePath,
        IProgress<(double Percent, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zipFilePath) || !File.Exists(zipFilePath))
        {
            return (false, "File arsip ZIP tidak ditemukan.");
        }

        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var targetSub = package.SelectedDownloadVersion?.TargetSubFolder;
        var targetFolder = !string.IsNullOrWhiteSpace(targetSub)
            ? Path.Combine(baseDir, package.ExpectedRelativeDir, targetSub)
            : Path.Combine(baseDir, package.ExpectedRelativeDir);

        // If target folder already has binaries and no subfolder was explicitly set, generate versioned slot name
        if (string.IsNullOrWhiteSpace(targetSub) && Directory.Exists(targetFolder) && Directory.EnumerateFileSystemEntries(targetFolder).Any())
        {
            var archiveSlotName = Path.GetFileNameWithoutExtension(zipFilePath);
            targetFolder = Path.Combine(targetFolder, archiveSlotName);
        }

        var stagingFolder = Path.Combine(DraGonPaths.TempDirectory, $"extract_{package.Id}_{Guid.NewGuid():N}");

        try
        {
            progress?.Report((80, "Mengekstrak arsip ZIP ke folder staging..."));

            await Task.Run(() =>
            {
                Directory.CreateDirectory(stagingFolder);
                ZipFile.ExtractToDirectory(zipFilePath, stagingFolder, overwriteFiles: true);

                cancellationToken.ThrowIfCancellationRequested();

                // Check if the ZIP contains a single root wrapper directory (e.g. nginx-1.26.2\*)
                var subDirs = Directory.GetDirectories(stagingFolder);
                var rootFiles = Directory.GetFiles(stagingFolder);
                var sourceCopyRoot = (subDirs.Length == 1 && rootFiles.Length == 0)
                    ? subDirs[0]
                    : stagingFolder;

                Directory.CreateDirectory(targetFolder);
                CopyDirectoryContents(sourceCopyRoot, targetFolder);
            }, cancellationToken);

            // Post-install configurations
            if (string.Equals(package.Id, "mariadb", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report((92, "Memeriksa inisialisasi folder data MariaDB..."));
                await TryInitializeMariaDbDataDirectoryAsync(targetFolder);
            }
            else if (string.Equals(package.Id, "phpmyadmin", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report((94, "Menyiapkan konfigurasi otomatis phpMyAdmin (config.inc.php)..."));
                ServerConfigGenerator.EnsurePhpMyAdminConfigured(targetFolder);
            }
            else if (string.Equals(package.Id, "python", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report((94, "Menyiapkan konfigurasi integrasi Python..."));
                EnsurePythonConfigured(targetFolder);
            }
            else if (string.Equals(package.Id, "heidisql", StringComparison.OrdinalIgnoreCase))
            {
                EnsureHeidiSqlConfigured(targetFolder);
            }

            progress?.Report((100, "Selesai dipasang."));
            _logger.Info("Installer", $"Paket runtime '{package.Name}' berhasil diekstrak ke: {targetFolder}");
            return (true, $"Paket '{package.Name}' berhasil dipasang ke '{targetFolder}'.");
        }
        catch (Exception ex)
        {
            _logger.Error("Installer", $"Gagal mengekstrak arsip ZIP untuk {package.Name}.", ex);
            return (false, $"Gagal mengekstrak arsip ZIP: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingFolder))
                {
                    Directory.Delete(stagingFolder, recursive: true);
                }
            }
            catch
            {
                // Ignore staging cleanup error
            }
        }
    }

    public async Task<(bool Success, string Message)> TryInitializeMariaDbDataDirectoryAsync(string mariaDbFolder)
    {
        if (!Directory.Exists(mariaDbFolder))
        {
            return (false, "Folder runtime MariaDB belum tersedia.");
        }

        var dataDir = Path.Combine(mariaDbFolder, "data");
        if (Directory.Exists(dataDir) && Directory.EnumerateFileSystemEntries(dataDir).Any())
        {
            return (true, "Folder data MariaDB sudah terinisialisasi.");
        }

        var installDbExe = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
            mariaDbFolder,
            @"bin\mysql_install_db.exe");

        var mysqldExe = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
            mariaDbFolder,
            @"bin\mysqld.exe");

        if ((string.IsNullOrWhiteSpace(installDbExe) || !File.Exists(installDbExe)) &&
            (string.IsNullOrWhiteSpace(mysqldExe) || !File.Exists(mysqldExe)))
        {
            return (false, "Executable 'bin\\mysql_install_db.exe' (MariaDB) atau 'bin\\mysqld.exe' (MySQL) belum ditemukan.");
        }

        var primaryExe = !string.IsNullOrWhiteSpace(installDbExe) && File.Exists(installDbExe) ? installDbExe : mysqldExe!;
        var isMariaDbInstaller = string.Equals(Path.GetFileName(primaryExe), "mysql_install_db.exe", StringComparison.OrdinalIgnoreCase);
        var binDir = Path.GetDirectoryName(primaryExe)!;
        var rootDir = Directory.GetParent(binDir)?.FullName ?? mariaDbFolder;
        var targetDataDir = Path.Combine(rootDir, "data");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = primaryExe,
                WorkingDirectory = rootDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (isMariaDbInstaller)
            {
                psi.ArgumentList.Add($"--datadir={targetDataDir}");
            }
            else
            {
                // MySQL 8.x / 8.4 initialization without root password
                psi.ArgumentList.Add("--initialize-insecure");
                psi.ArgumentList.Add($"-- basedir={rootDir}".Replace("-- basedir=", "--basedir="));
                psi.ArgumentList.Add($"--datadir={targetDataDir}");
            }

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return (false, "Gagal memulai proses mysql_install_db.exe.");
            }

            await proc.WaitForExitAsync();
            if (proc.ExitCode == 0)
            {
                _logger.Info("MariaDB", $"Folder data MariaDB berhasil diinisialisasi di {targetDataDir}.");
                return (true, $"Database awal MariaDB berhasil diinisialisasi di '{targetDataDir}'.");
            }

            var err = await proc.StandardError.ReadToEndAsync();
            return (false, $"mysql_install_db selesai dengan kode {proc.ExitCode}: {err}");
        }
        catch (Exception ex)
        {
            _logger.Error("MariaDB", "Gagal menginisialisasi folder data MariaDB.", ex);
            return (false, $"Gagal menginisialisasi data MariaDB: {ex.Message}");
        }
    }

    private static void CopyDirectoryContents(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectoryContents(dir, destSubDir);
        }
    }

    private static void EnsurePythonConfigured(string pythonFolder)
    {
        try
        {
            // Enable 'import site' in python*._pth if present so pip and external modules can be loaded
            foreach (var pthFile in Directory.GetFiles(pythonFolder, "*._pth"))
            {
                var content = File.ReadAllText(pthFile);
                if (content.Contains("#import site"))
                {
                    content = content.Replace("#import site", "import site");
                    File.WriteAllText(pthFile, content);
                }
            }
        }
        catch
        {
            // Ignore minor pth configuration error
        }
    }

    private static void EnsureHeidiSqlConfigured(string heidiFolder)
    {
        try
        {
            // Ensure portable_settings.txt exists so HeidiSQL operates portably within its runtime folder
            var settingsFile = Path.Combine(heidiFolder, "portable_settings.txt");
            if (!File.Exists(settingsFile))
            {
                File.WriteAllText(settingsFile, string.Empty);
            }
        }
        catch
        {
            // Ignore portable marker creation error
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DraGon-LocalDev-Windows/1.0");
        return client;
    }
}
