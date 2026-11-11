using System.Diagnostics;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public static class EnvironmentPathService
{
    private static readonly string[] RuntimeFolderNames =
    [
        "php",
        "composer",
        "mariadb",
        "mysql",
        "nodejs",
        "apache",
        "git",
        "postgresql",
        "nginx"
    ];

    /// <summary>
    /// Mencari semua direktori binary runtime yang perlu didaftarkan ke Windows PATH
    /// agar perintah seperti php, composer, mysql, node, git dapat langsung dijalankan di Command Prompt.
    /// </summary>
    public static List<string> ResolveRuntimeBinaryDirectories(string runtimeBasePath, AppSettings? settings = null)
    {
        var resolvedDirs = new List<string>();

        if (string.IsNullOrWhiteSpace(runtimeBasePath) || !Directory.Exists(runtimeBasePath))
        {
            return resolvedDirs;
        }

        // 1. PHP binary directory
        var phpDir = ResolveActivePhpDirectory(runtimeBasePath, settings);
        if (!string.IsNullOrWhiteSpace(phpDir) && Directory.Exists(phpDir))
        {
            resolvedDirs.Add(Path.GetFullPath(phpDir));
        }

        // 2. Composer binary directory (mengandung composer.bat)
        var composerDir = ResolveComposerDirectory(runtimeBasePath);
        if (!string.IsNullOrWhiteSpace(composerDir) && Directory.Exists(composerDir))
        {
            resolvedDirs.Add(Path.GetFullPath(composerDir));
        }

        // 3. Database (MariaDB / MySQL) bin directory (mengandung mysql.exe)
        var dbBinDir = ResolveActiveDatabaseBinDirectory(runtimeBasePath, settings);
        if (!string.IsNullOrWhiteSpace(dbBinDir) && Directory.Exists(dbBinDir))
        {
            resolvedDirs.Add(Path.GetFullPath(dbBinDir));
        }

        // 4. Node.js binary directory (mengandung node.exe & npm.cmd)
        var nodeDir = ResolveNodeJsDirectory(runtimeBasePath);
        if (!string.IsNullOrWhiteSpace(nodeDir) && Directory.Exists(nodeDir))
        {
            resolvedDirs.Add(Path.GetFullPath(nodeDir));
        }

        // 5. Git binary directory (mengandung git.exe)
        var gitDir = ResolveGitDirectory(runtimeBasePath);
        if (!string.IsNullOrWhiteSpace(gitDir) && Directory.Exists(gitDir))
        {
            resolvedDirs.Add(Path.GetFullPath(gitDir));
        }

        // 6. Apache bin directory (mengandung httpd.exe)
        var apacheBinDir = ResolveApacheBinDirectory(runtimeBasePath);
        if (!string.IsNullOrWhiteSpace(apacheBinDir) && Directory.Exists(apacheBinDir))
        {
            resolvedDirs.Add(Path.GetFullPath(apacheBinDir));
        }

        // 7. Folder root aplikasi DraGon (jika terdaftar di settings)
        if (!string.IsNullOrWhiteSpace(settings?.ApplicationFolderPath) && Directory.Exists(settings.ApplicationFolderPath))
        {
            resolvedDirs.Add(Path.GetFullPath(settings.ApplicationFolderPath));
        }

        return resolvedDirs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Menyinkronkan daftar direktori runtime DraGon ke Windows User Environment PATH.
    /// Path DraGon akan ditempatkan pada posisi terdepan agar menjadi prioritas saat command dijalankan di Command Prompt.
    /// </summary>
    public static (bool Changed, List<string> ActivePaths) SyncUserEnvironmentPath(
        string runtimeBasePath,
        AppSettings? settings = null,
        AppLogger? logger = null)
    {
        try
        {
            var desiredPaths = ResolveRuntimeBinaryDirectories(runtimeBasePath, settings);
            if (desiredPaths.Count == 0)
            {
                return (false, desiredPaths);
            }

            var currentRaw = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            var currentEntries = currentRaw
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Saring entri: hapus entri DraGon lama (yang berada dalam subfolder runtimes yang tidak lagi aktif)
            var normalizedRuntimeBase = Path.GetFullPath(runtimeBasePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var filteredExisting = new List<string>();

            foreach (var entry in currentEntries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                try
                {
                    var fullEntry = Path.GetFullPath(entry).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    // Jika entri ini merupakan bagian dari runtime DraGon, tetapi tidak termasuk desiredPaths, kita skip (hapus versi lama)
                    if (fullEntry.StartsWith(normalizedRuntimeBase, StringComparison.OrdinalIgnoreCase))
                    {
                        if (desiredPaths.Any(dp => string.Equals(dp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fullEntry, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Tetap di-skip di sini karena akan diletakkan di depan
                            continue;
                        }

                        // Entri versi lama DraGon, buang
                        continue;
                    }

                    filteredExisting.Add(entry);
                }
                catch
                {
                    filteredExisting.Add(entry);
                }
            }

            // Gabungkan: letakkan desiredPaths di depan, diikuti entri existing pengguna
            var newPathList = desiredPaths.Concat(filteredExisting).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var newPathString = string.Join(";", newPathList) + ";";

            // Cek apakah ada perubahan
            if (!string.Equals(currentRaw.TrimEnd(';'), newPathString.TrimEnd(';'), StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", newPathString, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("PATH", newPathString, EnvironmentVariableTarget.Process);

                logger?.Info("EnvironmentPath", $"Windows User PATH berhasil diperbarui untuk Command Prompt: {string.Join(", ", desiredPaths)}");
                return (true, desiredPaths);
            }

            return (false, desiredPaths);
        }
        catch (Exception ex)
        {
            logger?.Warn("EnvironmentPath", $"Gagal menyinkronkan Windows User PATH: {ex.Message}");
            return (false, []);
        }
    }

    /// <summary>
    /// Memeriksa apakah semua direktori binary runtime DraGon sudah tercantum dalam User PATH.
    /// </summary>
    public static bool AreAllPathsConfiguredInUserEnvironment(string runtimeBasePath, AppSettings? settings = null)
    {
        var desired = ResolveRuntimeBinaryDirectories(runtimeBasePath, settings);
        if (desired.Count == 0) return true;

        var currentRaw = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        var currentEntries = currentRaw
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p =>
            {
                try { return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
                catch { return p; }
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return desired.All(d =>
        {
            var norm = Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return currentEntries.Contains(norm);
        });
    }

    private static string? ResolveActivePhpDirectory(string runtimeBasePath, AppSettings? settings)
    {
        var phpRoot = Path.Combine(runtimeBasePath, "php");
        if (!Directory.Exists(phpRoot)) return null;

        // Jika ada selected PHP version id
        if (!string.IsNullOrWhiteSpace(settings?.SelectedPhpVersionId))
        {
            var match = Directory.GetDirectories(phpRoot)
                .FirstOrDefault(d => Path.GetFileName(d).Contains(settings.SelectedPhpVersionId, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(d, "php.exe")));
            if (match is not null) return match;
        }

        // Cari subfolder yang memiliki php.exe
        var directExe = Path.Combine(phpRoot, "php.exe");
        if (File.Exists(directExe)) return phpRoot;

        var candidate = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(phpRoot, "php.exe");
        return candidate is not null ? Path.GetDirectoryName(candidate) : null;
    }

    private static string? ResolveComposerDirectory(string runtimeBasePath)
    {
        var composerRoot = Path.Combine(runtimeBasePath, "composer");
        if (!Directory.Exists(composerRoot)) return null;

        if (File.Exists(Path.Combine(composerRoot, "composer.bat"))) return composerRoot;

        var candidate = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(composerRoot, "composer.bat");
        return candidate is not null ? Path.GetDirectoryName(candidate) : null;
    }

    private static string? ResolveActiveDatabaseBinDirectory(string runtimeBasePath, AppSettings? settings)
    {
        foreach (var rootName in new[] { "mariadb", "mysql" })
        {
            var dbRoot = Path.Combine(runtimeBasePath, rootName);
            if (!Directory.Exists(dbRoot)) continue;

            if (!string.IsNullOrWhiteSpace(settings?.SelectedDatabaseVersionId))
            {
                var match = Directory.GetDirectories(dbRoot)
                    .FirstOrDefault(d => Path.GetFileName(d).Contains(settings.SelectedDatabaseVersionId, StringComparison.OrdinalIgnoreCase) &&
                                         File.Exists(Path.Combine(d, "bin", "mysql.exe")));
                if (match is not null) return Path.Combine(match, "bin");
            }

            var directBin = Path.Combine(dbRoot, "bin", "mysql.exe");
            if (File.Exists(directBin)) return Path.Combine(dbRoot, "bin");

            var candidate = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(dbRoot, @"bin\mysql.exe")
                ?? RuntimePackageCatalogService.FindExecutableInRuntimeFolder(dbRoot, @"bin\mysqld.exe");
            if (candidate is not null) return Path.GetDirectoryName(candidate);
        }

        return null;
    }

    private static string? ResolveNodeJsDirectory(string runtimeBasePath)
    {
        var nodeRoot = Path.Combine(runtimeBasePath, "nodejs");
        if (!Directory.Exists(nodeRoot)) return null;

        if (File.Exists(Path.Combine(nodeRoot, "node.exe"))) return nodeRoot;

        var candidate = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(nodeRoot, "node.exe");
        return candidate is not null ? Path.GetDirectoryName(candidate) : null;
    }

    private static string? ResolveGitDirectory(string runtimeBasePath)
    {
        var gitRoot = Path.Combine(runtimeBasePath, "git");
        if (!Directory.Exists(gitRoot)) return null;

        var cmdDir = Path.Combine(gitRoot, "cmd");
        if (File.Exists(Path.Combine(cmdDir, "git.exe"))) return cmdDir;

        var candidate = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(gitRoot, @"cmd\git.exe")
            ?? RuntimePackageCatalogService.FindExecutableInRuntimeFolder(gitRoot, @"bin\git.exe");
        return candidate is not null ? Path.GetDirectoryName(candidate) : null;
    }

    private static string? ResolveApacheBinDirectory(string runtimeBasePath)
    {
        var apacheRoot = Path.Combine(runtimeBasePath, "apache");
        if (!Directory.Exists(apacheRoot)) return null;

        if (File.Exists(Path.Combine(apacheRoot, "bin", "httpd.exe"))) return Path.Combine(apacheRoot, "bin");

        var candidate = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(apacheRoot, @"bin\httpd.exe");
        return candidate is not null ? Path.GetDirectoryName(candidate) : null;
    }
}
