using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DraGon.Installer;

public sealed record InstallOptions(
    string TargetDirectory,
    string WorkspaceDirectory,
    bool CreateDesktopShortcut,
    bool CreateStartMenuShortcut,
    bool InitializeWorkspaceAndDataDirs,
    bool LaunchAfterInstall);

[SupportedOSPlatform("windows")]
public static class InstallerEngine
{
    private const string UninstallRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DraGon";

    public static string DefaultInstallDirectory => @"C:\DraGon";

    public static string DesktopShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DraGon.lnk");

    public static string StartMenuShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "DraGon.lnk");

    public static async Task<(bool Success, string Message, string InstalledExePath)> RunInstallAsync(
        InstallOptions options,
        IProgress<(int Percent, string Status)>? progress = null)
    {
        try
        {
            var targetDir = Path.GetFullPath(options.TargetDirectory.Trim());
            var workspaceDir = string.IsNullOrWhiteSpace(options.WorkspaceDirectory)
                ? Path.Combine(targetDir, "www")
                : Path.GetFullPath(options.WorkspaceDirectory.Trim());

            progress?.Report((5, "Menghentikan proses DraGon dan layanan aktif (Apache, PHP, DB, Nginx)..."));
            StopRunningDraGonInstances(targetDir);

            progress?.Report((12, $"Menyiapkan direktori aplikasi custom: {targetDir}..."));
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(workspaceDir);

            progress?.Report((25, "Mengekstrak berkas aplikasi DraGon & DraGon.Elevator..."));
            await ExtractPayloadAsync(targetDir, progress);

            var installedMainExe = Path.Combine(targetDir, "DraGon.App.exe");
            if (!File.Exists(installedMainExe))
            {
                return (false, $"Berkas utama '{installedMainExe}' tidak ditemukan setelah ekstraksi.", string.Empty);
            }

            // Copy current installer executable as uninstaller
            var currentProcessPath = Environment.ProcessPath;
            var uninstallerPath = Path.Combine(targetDir, "DraGon-Setup.exe");
            if (!string.IsNullOrWhiteSpace(currentProcessPath) &&
                File.Exists(currentProcessPath) &&
                !string.Equals(Path.GetFullPath(currentProcessPath), Path.GetFullPath(uninstallerPath), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Copy(currentProcessPath, uninstallerPath, overwrite: true);
                }
                catch
                {
                    // Ignore if single-file copy is locked
                }
            }

            if (options.InitializeWorkspaceAndDataDirs)
            {
                progress?.Report((68, "Menyiapkan seluruh paket runtime (Nginx, PHP, MariaDB, Apache, Composer, Node.js)..."));
                var effectiveRuntimeDir = await ProvisionRuntimesAndSettingsAsync(targetDir, workspaceDir, progress);
                progress?.Report((82, $"Runtime siap digunakan di: {effectiveRuntimeDir}"));
            }

            if (options.CreateDesktopShortcut)
            {
                progress?.Report((88, "Membuat pintasan (shortcut) di Desktop..."));
                CreateWindowsShortcut(DesktopShortcutPath, installedMainExe, targetDir, "DraGon — Lingkungan Development Lokal Windows (.dra)");
            }

            if (options.CreateStartMenuShortcut)
            {
                progress?.Report((93, "Membuat pintasan di Start Menu Windows..."));
                CreateWindowsShortcut(StartMenuShortcutPath, installedMainExe, targetDir, "DraGon — Lingkungan Development Lokal Windows (.dra)");
            }

            progress?.Report((97, "Mendaftarkan aplikasi pada Windows Installed Apps..."));
            RegisterUninstallerInRegistry(targetDir, installedMainExe, uninstallerPath);

            progress?.Report((100, "Instalasi DraGon selesai! Seluruh runtime siap dijalankan dengan 'Start All'."));
            return (true, $"DraGon berhasil dipasang di '{targetDir}'.", installedMainExe);
        }
        catch (Exception ex)
        {
            return (false, $"Instalasi gagal: {ex.Message}", string.Empty);
        }
    }

    public static (bool Success, string Message) RunUninstall()
    {
        try
        {
            var installLocation = DefaultInstallDirectory;
            using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryKeyPath, writable: false))
            {
                if (key?.GetValue("InstallLocation") is string regLoc && !string.IsNullOrWhiteSpace(regLoc))
                {
                    installLocation = regLoc;
                }
            }

            StopRunningDraGonInstances(installLocation);

            if (File.Exists(DesktopShortcutPath))
            {
                File.Delete(DesktopShortcutPath);
            }

            if (File.Exists(StartMenuShortcutPath))
            {
                File.Delete(StartMenuShortcutPath);
            }

            Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryKeyPath, throwOnMissingSubKey: false);

            if (Directory.Exists(installLocation))
            {
                foreach (var file in Directory.GetFiles(installLocation, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore if uninstaller binary itself is still running
                    }
                }
            }

            return (true, "Aplikasi DraGon dan pintasannya telah dihapus. Data proyek dan konfigurasi tetap disimpan dengan aman.");
        }
        catch (Exception ex)
        {
            return (false, $"Gagal menghapus instalasi: {ex.Message}");
        }
    }

    private static async Task ExtractPayloadAsync(string targetDir, IProgress<(int Percent, string Status)>? progress)
    {
        await Task.Run(() =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream("DraGonPayload.zip")
                ?? OpenFallbackPayloadStream();

            if (resourceStream is null)
            {
                throw new FileNotFoundException("Paket payload instalasi (DraGonPayload.zip) tidak ditemukan di dalam installer.");
            }

            using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);
            var totalEntries = Math.Max(1, archive.Entries.Count);
            var processed = 0;

            var lastReportPct = -1;
            foreach (var entry in archive.Entries)
            {
                processed++;
                var destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                if (!destinationPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
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

                SafeExtractEntryToFile(entry, destinationPath, targetDir);

                var pct = 15 + (int)((double)processed / totalEntries * 55.0);
                if (pct != lastReportPct || processed % 80 == 0 || processed == totalEntries)
                {
                    lastReportPct = pct;
                    progress?.Report((pct, $"Mengekstrak paket ({pct}%): {entry.Name}"));
                }
            }
        });
    }

    private static Stream? OpenFallbackPayloadStream()
    {
        var adjacentZip = Path.Combine(AppContext.BaseDirectory, "DraGonPayload.zip");
        return File.Exists(adjacentZip) ? File.OpenRead(adjacentZip) : null;
    }

    private static async Task<string> ProvisionRuntimesAndSettingsAsync(
        string targetDir,
        string workspaceDir,
        IProgress<(int Percent, string Status)>? progress)
    {
        var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DraGon");
        var configDir = Path.Combine(appDataRoot, "config");
        var localAppDataRuntimes = Path.Combine(appDataRoot, "runtimes");
        var targetCustomRuntimes = Path.Combine(targetDir, "runtimes");

        Directory.CreateDirectory(appDataRoot);
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(Path.Combine(appDataRoot, "certs"));
        Directory.CreateDirectory(Path.Combine(appDataRoot, "logs"));
        Directory.CreateDirectory(Path.Combine(appDataRoot, "backups"));
        Directory.CreateDirectory(localAppDataRuntimes);
        Directory.CreateDirectory(targetCustomRuntimes);
        Directory.CreateDirectory(workspaceDir);

        // Check if pre-downloaded runtimes exist in various candidate directories
        var candidateSourceDirs = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "runtimes"),
            Path.Combine(AppContext.BaseDirectory, "..", "runtimes"),
            Path.Combine(AppContext.BaseDirectory, "..", "dist", "runtimes"),
            localAppDataRuntimes,
            targetCustomRuntimes
        };

        string? sourceRuntimes = null;
        foreach (var candidate in candidateSourceDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (DraGon.Core.Services.DraGonPaths.HasInstalledRuntimes(candidate))
            {
                sourceRuntimes = candidate;
                break;
            }
        }

        var effectiveRuntimeDir = targetCustomRuntimes;

        if (sourceRuntimes is not null)
        {
            // Create directory junctions or copy lightweight configs into targetCustomRuntimes so custom app folder has full access
            foreach (var pkgName in new[] { "apache", "nginx", "php", "mariadb", "mysql", "nodejs", "composer", "git", "postgresql", "phpmyadmin" })
            {
                var srcPkg = Path.Combine(sourceRuntimes, pkgName);
                var dstPkg = Path.Combine(targetCustomRuntimes, pkgName);
                var localPkg = Path.Combine(localAppDataRuntimes, pkgName);

                if (!Directory.Exists(srcPkg))
                {
                    Directory.CreateDirectory(dstPkg);
                    continue;
                }

                if (!string.Equals(Path.GetFullPath(srcPkg), Path.GetFullPath(dstPkg), StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report((74, $"Menyiapkan paket runtime {pkgName}..."));
                    EnsureDirectoryLinkOrCopy(srcPkg, dstPkg);
                }

                if (!string.Equals(Path.GetFullPath(srcPkg), Path.GetFullPath(localPkg), StringComparison.OrdinalIgnoreCase) && !Directory.Exists(localPkg))
                {
                    EnsureDirectoryLinkOrCopy(srcPkg, localPkg);
                }
            }

            effectiveRuntimeDir = DraGon.Core.Services.DraGonPaths.HasInstalledRuntimes(targetCustomRuntimes)
                ? targetCustomRuntimes
                : sourceRuntimes;
        }
        else
        {
            // Tidak ada runtime lokal di PC ini (misalnya fresh install di PC baru dari DraGon-Setup.exe standalone)
            progress?.Report((69, "Runtime lokal belum ada di PC ini. Memeriksa koneksi & mengunduh runtime inti resmi..."));
            await TryAutoDownloadCoreRuntimesAsync(targetCustomRuntimes, progress);

            if (DraGon.Core.Services.DraGonPaths.HasInstalledRuntimes(targetCustomRuntimes))
            {
                effectiveRuntimeDir = targetCustomRuntimes;
            }
        }

        // Otomatis konfigurasi php.ini dan inisialisasi data MariaDB jika belum siap
        progress?.Report((88, "Mengonfigurasi php.ini & data storage database..."));
        EnsurePhpIniConfigured(effectiveRuntimeDir);
        EnsureDatabaseDataInitialized(effectiveRuntimeDir);

        // Mendaftarkan direktori runtime ke Windows User PATH untuk Command Prompt
        progress?.Report((92, "Memeriksa dan mendaftarkan runtime ke Windows PATH untuk Command Prompt..."));
        try
        {
            var (changed, activePaths) = DraGon.Core.Services.EnvironmentPathService.SyncUserEnvironmentPath(effectiveRuntimeDir);
            if (activePaths.Count > 0)
            {
                progress?.Report((94, $"PATH aktif ({activePaths.Count} paket terdaftar: PHP, Composer, MySQL, Node.js)."));
            }
        }
        catch
        {
            // Abaikan jika ada pembatasan environment
        }

        // Create sample welcome project if workspace is empty
        var welcomeProjectDir = Path.Combine(workspaceDir, "welcome");
        var welcomeIndexPhp = Path.Combine(welcomeProjectDir, "index.php");
        if (!File.Exists(welcomeIndexPhp))
        {
            Directory.CreateDirectory(welcomeProjectDir);
            File.WriteAllText(welcomeIndexPhp,
                """
                <?php
                header('Content-Type: text/html; charset=utf-8');
                ?>
                <!DOCTYPE html>
                <html lang="id">
                <head>
                    <meta charset="utf-8" />
                    <title>DraGon Local Development Environment (.dra)</title>
                    <style>
                        body { background:#0B0F17; color:#F8FAFC; font-family:Segoe UI, sans-serif; padding:48px; text-align:center; }
                        .card { max-width:640px; margin:0 auto; background:#161F30; border:1px solid #283548; border-radius:12px; padding:32px; }
                        h1 { color:#FF6B35; margin-top:0; }
                        .badge { display:inline-block; background:#0F2922; color:#10B981; padding:6px 14px; border-radius:999px; font-weight:bold; }
                    </style>
                </head>
                <body>
                    <div class="card">
                        <h1>DraGon (.dra) Aktif!</h1>
                        <p class="badge">PHP <?= PHP_VERSION ?> • <?= $_SERVER['SERVER_SOFTWARE'] ?? 'DraGon Web Server' ?></p>
                        <p>Lingkungan development lokal DraGon sudah berjalan dan siap melayani proyek PHP, Laravel, serta Node.js Anda.</p>
                    </div>
                </body>
                </html>
                """,
                new UTF8Encoding(false));
        }

        // Save settings.json pointing to the custom application folder, custom workspace, and ready runtime folder
        var settingsPath = Path.Combine(configDir, "settings.json");
        var settingsObj = new
        {
            applicationFolderPath = targetDir,
            workspacePath = workspaceDir,
            runtimeBasePath = effectiveRuntimeDir,
            preferredWebServer = "Apache",
            webServerPort = 8080,
            webServerHttpsPort = 8443,
            apachePort = 80,
            apacheHttpsPort = 443,
            databasePort = 3306,
            phpCgiPort = 9000,
            enableLocalHttps = true,
            selectedPhpVersionId = "",
            selectedDatabaseVersionId = "",
            preferredBrowser = "SystemDefault",
            customBrowserPath = "",
            runOnWindowsStartup = false,
            theme = "Dark"
        };

        var json = JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json, new UTF8Encoding(false));

        return effectiveRuntimeDir;
    }

    private static async Task TryAutoDownloadCoreRuntimesAsync(
        string targetRuntimesDir,
        IProgress<(int Percent, string Status)>? progress)
    {
        try
        {
            var logger = new DraGon.Core.Services.AppLogger();
            var installer = new DraGon.Core.Services.RuntimeInstallerService(logger);
            var catalog = new DraGon.Core.Services.RuntimePackageCatalogService().CreateCatalog();

            // 1. PHP (Default: PHP 8.3 LTS Populer Laravel)
            var phpPresets = installer.GetPhpVersionPresets();
            var phpTarget = phpPresets.FirstOrDefault(p => p.FolderName.Contains("8.3")) ?? phpPresets.FirstOrDefault();
            if (phpTarget != null && !Directory.Exists(Path.Combine(targetRuntimesDir, "php")))
            {
                progress?.Report((70, $"Mengunduh paket resmi {phpTarget.DisplayName} dari windows.php.net..."));
                var phpProgress = new Progress<(double Percent, string Status)>(p =>
                    progress?.Report((70 + (int)(p.Percent * 0.05), p.Status)));
                await installer.DownloadAndInstallVersionPresetAsync(phpTarget, targetRuntimesDir, phpProgress);
            }

            // 2. Apache HTTP Server (Official Win64)
            var apachePkg = catalog.FirstOrDefault(c => c.Id == "apache");
            if (apachePkg != null && !Directory.Exists(Path.Combine(targetRuntimesDir, "apache")))
            {
                progress?.Report((75, "Mengunduh paket resmi Apache HTTP Server dari apachelounge.com..."));
                var apacheProgress = new Progress<(double Percent, string Status)>(p =>
                    progress?.Report((75 + (int)(p.Percent * 0.05), p.Status)));
                await installer.DownloadAndInstallOfficialArchiveAsync(apachePkg, targetRuntimesDir, apacheProgress);
            }

            // 3. MariaDB Database Server (LTS 11.4)
            var dbPresets = installer.GetDatabaseVersionPresets();
            var dbTarget = dbPresets.FirstOrDefault(p => p.FolderName.Contains("mariadb-11.4")) ?? dbPresets.FirstOrDefault();
            if (dbTarget != null && !Directory.Exists(Path.Combine(targetRuntimesDir, "mariadb")))
            {
                progress?.Report((80, $"Mengunduh paket resmi {dbTarget.DisplayName} dari mariadb.org..."));
                var dbProgress = new Progress<(double Percent, string Status)>(p =>
                    progress?.Report((80 + (int)(p.Percent * 0.05), p.Status)));
                await installer.DownloadAndInstallVersionPresetAsync(dbTarget, targetRuntimesDir, dbProgress);
            }

            // 4. Composer PHP Dependency Manager
            var composerPkg = catalog.FirstOrDefault(c => c.Id == "composer");
            if (composerPkg != null && !Directory.Exists(Path.Combine(targetRuntimesDir, "composer")))
            {
                progress?.Report((85, "Mengunduh Composer dependency manager dari getcomposer.org..."));
                var compProgress = new Progress<(double Percent, string Status)>(p =>
                    progress?.Report((85 + (int)(p.Percent * 0.02), p.Status)));
                await installer.DownloadAndInstallOfficialArchiveAsync(composerPkg, targetRuntimesDir, compProgress);
            }
        }
        catch (Exception ex)
        {
            progress?.Report((87, $"Pengunduhan online otomatis dilewati ({ex.Message}). Anda dapat mengunduh atau mengimpor paket kapan saja via tab 'Packages & Installer'."));
        }
    }

    private static void EnsurePhpIniConfigured(string runtimeDir)
    {
        try
        {
            var phpRoot = Path.Combine(runtimeDir, "php");
            if (!Directory.Exists(phpRoot)) return;

            var phpDirs = Directory.GetDirectories(phpRoot).ToList();
            if (File.Exists(Path.Combine(phpRoot, "php.exe"))) phpDirs.Add(phpRoot);

            foreach (var dir in phpDirs)
            {
                var iniPath = Path.Combine(dir, "php.ini");
                if (File.Exists(iniPath)) continue;

                var devTemplate = Path.Combine(dir, "php.ini-development");
                var prodTemplate = Path.Combine(dir, "php.ini-production");
                var template = File.Exists(devTemplate) ? devTemplate : File.Exists(prodTemplate) ? prodTemplate : null;
                if (template is null) continue;

                var content = File.ReadAllText(template);
                var targetExts = new[] { "curl", "fileinfo", "gd", "mbstring", "mysqli", "openssl", "pdo_mysql", "zip", "exif" };
                foreach (var ext in targetExts)
                {
                    content = content.Replace($";extension={ext}", $"extension={ext}");
                }
                File.WriteAllText(iniPath, content, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Abaikan error minor jika php.ini sudah terkonfigurasi
        }
    }

    private static void EnsureDatabaseDataInitialized(string runtimeDir)
    {
        try
        {
            foreach (var rootName in new[] { "mariadb", "mysql" })
            {
                var dbRoot = Path.Combine(runtimeDir, rootName);
                if (!Directory.Exists(dbRoot)) continue;

                var candidateDirs = Directory.GetDirectories(dbRoot).ToList();
                if (File.Exists(Path.Combine(dbRoot, "bin", "mysqld.exe"))) candidateDirs.Add(dbRoot);

                foreach (var dir in candidateDirs)
                {
                    var dataDir = Path.Combine(dir, "data");
                    if (Directory.Exists(dataDir) && Directory.EnumerateFileSystemEntries(dataDir).Any()) continue;

                    var installDbExe = Path.Combine(dir, "bin", "mariadb-install-db.exe");
                    if (!File.Exists(installDbExe)) installDbExe = Path.Combine(dir, "bin", "mysql_install_db.exe");
                    if (!File.Exists(installDbExe)) continue;

                    Directory.CreateDirectory(dataDir);
                    var psi = new ProcessStartInfo
                    {
                        FileName = installDbExe,
                        Arguments = $"--datadir=\"{dataDir}\"",
                        WorkingDirectory = dir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(15000);
                }
            }
        }
        catch
        {
            // Abaikan error jika proses inisialisasi database ditangani runtime
        }
    }

    private static void EnsureDirectoryLinkOrCopy(string sourceDir, string targetDir)
    {
        try
        {
            if (Directory.Exists(targetDir))
            {
                var info = new DirectoryInfo(targetDir);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return;
                }

                if (!Directory.EnumerateFileSystemEntries(targetDir).Any())
                {
                    Directory.Delete(targetDir, recursive: false);
                }
                else
                {
                    return;
                }
            }

            // Create fast NTFS directory junction (`mklink /J`) so custom application folder has immediate access without duplicating 500MB
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{targetDir}\" \"{sourceDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            if (proc?.ExitCode == 0 && Directory.Exists(targetDir))
            {
                return;
            }
        }
        catch
        {
            // Fallback to directory creation if junction not supported
        }

        Directory.CreateDirectory(targetDir);
    }

    private static void SafeExtractEntryToFile(ZipArchiveEntry entry, string destinationPath, string? targetDir)
    {
        const int maxRetries = 6;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                entry.ExtractToFile(destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                // File sedang di-lock oleh proses aktif (misal httpd.exe, mysqld.exe, php-cgi.exe).
                // Hentikan proses yang mengunci dan tunggu sesaat.
                StopRunningDraGonInstances(targetDir ?? Path.GetDirectoryName(destinationPath));
                Thread.Sleep(attempt * 250);
            }
        }
    }

    private static void StopRunningDraGonInstances(string? targetDir = null)
    {
        // 1. Hentikan aplikasi GUI & elevator DraGon
        foreach (var procName in new[] { "DraGon.App", "DraGon.Elevator" })
        {
            KillProcessesByName(procName);
        }

        // 2. Hentikan seluruh service runtime lokal (Apache httpd, PHP FastCGI php-cgi, MariaDB mysqld/mariadbd, Nginx, PostgreSQL)
        var runtimeProcessNames = new[] { "httpd", "php-cgi", "mysqld", "mariadbd", "nginx", "postgres" };
        foreach (var procName in runtimeProcessNames)
        {
            foreach (var proc in Process.GetProcessesByName(procName))
            {
                try
                {
                    var shouldKill = false;
                    try
                    {
                        var exePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(exePath))
                        {
                            var fullExe = Path.GetFullPath(exePath);
                            if (fullExe.Contains("DraGon", StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrWhiteSpace(targetDir) && fullExe.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase)))
                            {
                                shouldKill = true;
                            }
                        }
                    }
                    catch
                    {
                        // Jika akses MainModule ditolak karena hak akses, tetap hentikan jika nama proses cocok
                        shouldKill = true;
                    }

                    if (shouldKill)
                    {
                        proc.Kill(entireProcessTree: true);
                        proc.WaitForExit(3000);
                    }
                }
                catch
                {
                    // Ignore
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }

        // Berikan jeda waktu agar Windows filesystem melepas file lock sepenuhnya
        Thread.Sleep(500);
    }

    private static void KillProcessesByName(string procName)
    {
        foreach (var proc in Process.GetProcessesByName(procName))
        {
            try
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(3000);
            }
            catch
            {
                // Ignore
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    private static void CreateWindowsShortcut(string shortcutFilePath, string targetExePath, string workingDir, string description)
    {
        try
        {
            var shortcutDir = Path.GetDirectoryName(shortcutFilePath);
            if (!string.IsNullOrWhiteSpace(shortcutDir))
            {
                Directory.CreateDirectory(shortcutDir);
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            var shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return;
            }

            var shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutFilePath]);

            if (shortcut is null)
            {
                return;
            }

            var scType = shortcut.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [targetExePath]);
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [workingDir]);
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, [description]);
            scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [$"{targetExePath},0"]);
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch
        {
            // Ignore shortcut creation failure if restricted by policy
        }
    }

    private static void RegisterUninstallerInRegistry(string installDir, string mainExePath, string uninstallerPath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue("DisplayName", "DraGon — Local Development Environment (.dra)");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "DraGon Local Dev");
            key.SetValue("InstallLocation", installDir);
            key.SetValue("DisplayIcon", mainExePath);
            key.SetValue("UninstallString", $"\"{uninstallerPath}\" --uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch
        {
            // Ignore registry errors
        }
    }
}
