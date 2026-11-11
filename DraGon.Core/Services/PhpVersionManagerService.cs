using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed class PhpVersionManagerService
{
    private static readonly string[] RecommendedExtensions =
    [
        "curl",
        "exif",
        "fileinfo",
        "gd",
        "intl",
        "mbstring",
        "mysqli",
        "openssl",
        "pdo_mysql",
        "pdo_sqlite",
        "sodium",
        "sqlite3",
        "zip",
        "opcache"
    ];

    private static readonly HashSet<string> IgnoredTestExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "dl_test",
        "zend_test"
    };

    private readonly AppLogger _logger;

    public PhpVersionManagerService(AppLogger logger)
    {
        _logger = logger;
    }

    public List<PhpVersionInfo> DiscoverPhpVersions(string runtimeBasePath, string? selectedVersionId = null)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var phpRoot = Path.Combine(baseDir, "php");
        var discovered = new List<PhpVersionInfo>();
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(phpRoot))
        {
            // 1. Direct installation in runtimes\php
            TryAddPhpDirectory(phpRoot, isSystemPath: false, discovered, seenDirs);

            // 2. Subfolders inside runtimes\php (e.g. runtimes\php\8.2, runtimes\php\8.3, runtimes\php\8.4)
            try
            {
                foreach (var subDir in Directory.GetDirectories(phpRoot))
                {
                    if (!TryAddPhpDirectory(subDir, isSystemPath: false, discovered, seenDirs))
                    {
                        foreach (var nestedDir in Directory.GetDirectories(subDir))
                        {
                            TryAddPhpDirectory(nestedDir, isSystemPath: false, discovered, seenDirs);
                        }
                    }
                }
            }
            catch
            {
                // Ignore directory access errors
            }
        }

        // 3. Also check Windows System PATH for php.exe / php-cgi.exe
        var pathPhpExe = RuntimePackageCatalogService.FindInSystemPath("php.exe");
        if (!string.IsNullOrWhiteSpace(pathPhpExe))
        {
            var pathDir = Path.GetDirectoryName(pathPhpExe);
            if (!string.IsNullOrWhiteSpace(pathDir))
            {
                TryAddPhpDirectory(pathDir, isSystemPath: true, discovered, seenDirs);
            }
        }

        // Mark active default
        if (discovered.Count > 0)
        {
            var match = !string.IsNullOrWhiteSpace(selectedVersionId)
                ? discovered.FirstOrDefault(v => string.Equals(v.Id, selectedVersionId, StringComparison.OrdinalIgnoreCase))
                : null;

            match ??= discovered.FirstOrDefault(v => !v.IsFromSystemPath) ?? discovered[0];
            match.IsActiveDefault = true;
        }

        return discovered;
    }

    public PhpVersionInfo? GetActivePhpVersion(AppSettings settings)
    {
        var all = DiscoverPhpVersions(settings.RuntimeBasePath, settings.SelectedPhpVersionId);
        return all.FirstOrDefault(v => v.IsActiveDefault) ?? all.FirstOrDefault();
    }

    public List<DatabaseVersionInfo> DiscoverDatabaseVersions(string runtimeBasePath, string? selectedVersionId = null)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var discovered = new List<DatabaseVersionInfo>();
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folderName in new[] { "mariadb", "mysql" })
        {
            var dbRoot = Path.Combine(baseDir, folderName);
            if (!Directory.Exists(dbRoot))
            {
                continue;
            }

            TryAddDatabaseDirectory(dbRoot, isSystemPath: false, discovered, seenDirs);

            try
            {
                foreach (var subDir in Directory.GetDirectories(dbRoot))
                {
                    if (!TryAddDatabaseDirectory(subDir, isSystemPath: false, discovered, seenDirs))
                    {
                        foreach (var nestedDir in Directory.GetDirectories(subDir))
                        {
                            TryAddDatabaseDirectory(nestedDir, isSystemPath: false, discovered, seenDirs);
                        }
                    }
                }
            }
            catch
            {
                // Ignore directory access errors
            }
        }

        if (discovered.Count > 0)
        {
            var match = !string.IsNullOrWhiteSpace(selectedVersionId)
                ? discovered.FirstOrDefault(v => string.Equals(v.Id, selectedVersionId, StringComparison.OrdinalIgnoreCase))
                : null;

            match ??= discovered.FirstOrDefault(v => !v.IsFromSystemPath) ?? discovered[0];
            match.IsActiveDefault = true;
        }

        return discovered;
    }

    public DatabaseVersionInfo? GetActiveDatabaseVersion(AppSettings settings)
    {
        var all = DiscoverDatabaseVersions(settings.RuntimeBasePath, settings.SelectedDatabaseVersionId);
        return all.FirstOrDefault(v => v.IsActiveDefault) ?? all.FirstOrDefault();
    }

    public (bool Success, string Message) EnsurePhpIniConfigured(PhpVersionInfo phpVersion)
    {
        if (string.IsNullOrWhiteSpace(phpVersion.DirectoryPath) || !Directory.Exists(phpVersion.DirectoryPath))
        {
            return (false, "Folder instalasi PHP tidak ditemukan.");
        }

        var iniPath = Path.Combine(phpVersion.DirectoryPath, "php.ini");
        if (File.Exists(iniPath))
        {
            return (true, $"File php.ini sudah tersedia di {iniPath}");
        }

        var devTemplate = Path.Combine(phpVersion.DirectoryPath, "php.ini-development");
        var prodTemplate = Path.Combine(phpVersion.DirectoryPath, "php.ini-production");
        var sourceTemplate = File.Exists(devTemplate) ? devTemplate : File.Exists(prodTemplate) ? prodTemplate : null;

        try
        {
            string content;
            if (sourceTemplate is not null)
            {
                content = File.ReadAllText(sourceTemplate, Encoding.UTF8);
                content = content.Replace(";extension_dir = \"ext\"", "extension_dir = \"ext\"");
                foreach (var ext in RecommendedExtensions)
                {
                    if (string.Equals(ext, "opcache", StringComparison.OrdinalIgnoreCase))
                    {
                        content = content.Replace(";zend_extension=opcache", "zend_extension=opcache");
                    }
                    else
                    {
                        content = content.Replace($";extension={ext}", $"extension={ext}");
                    }
                }
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("; Auto-generated php.ini by DraGon");
                sb.AppendLine("extension_dir = \"ext\"");
                sb.AppendLine("memory_limit = 512M");
                sb.AppendLine("upload_max_filesize = 64M");
                sb.AppendLine("post_max_size = 64M");
                sb.AppendLine("max_execution_time = 120");
                foreach (var ext in RecommendedExtensions)
                {
                    if (string.Equals(ext, "opcache", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine("zend_extension=opcache");
                    }
                    else
                    {
                        sb.AppendLine($"extension={ext}");
                    }
                }
                content = sb.ToString();
            }

            File.WriteAllText(iniPath, content, new UTF8Encoding(false));
            _logger.Info("PHP", $"Berhasil menyiapkan konfigurasi php.ini untuk {phpVersion.DisplayName} di {iniPath}.");
            return (true, $"Konfigurasi php.ini dengan ekstensi Laravel berhasil disiapkan untuk {phpVersion.DisplayName}.");
        }
        catch (Exception ex)
        {
            _logger.Error("PHP", $"Gagal membuat php.ini untuk {phpVersion.DisplayName}.", ex);
            return (false, $"Gagal menyiapkan php.ini: {ex.Message}");
        }
    }

    public List<PhpExtensionItem> GetExtensions(PhpVersionInfo? phpVersion)
    {
        if (phpVersion is null || string.IsNullOrWhiteSpace(phpVersion.DirectoryPath) || !Directory.Exists(phpVersion.DirectoryPath))
        {
            return [];
        }

        EnsurePhpIniConfigured(phpVersion);
        var iniPath = Path.Combine(phpVersion.DirectoryPath, "php.ini");
        var iniLines = File.Exists(iniPath) ? File.ReadAllLines(iniPath, Encoding.UTF8) : [];

        var enabledSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in iniLines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
            {
                continue;
            }

            // Match extension=curl or extension=php_curl.dll or zend_extension=opcache
            var match = Regex.Match(line, @"^(?:zend_)?extension\s*=\s*[""']?(?:php_)?([a-zA-Z0-9_]+?)(?:\.dll)?[""']?\s*(?:;.*)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                enabledSet.Add(match.Groups[1].Value);
            }
        }

        var extDir = Path.Combine(phpVersion.DirectoryPath, "ext");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(extDir))
        {
            foreach (var dllPath in Directory.GetFiles(extDir, "php_*.dll"))
            {
                var fileName = Path.GetFileNameWithoutExtension(dllPath);
                if (fileName.StartsWith("php_", StringComparison.OrdinalIgnoreCase))
                {
                    var extName = fileName[4..];
                    if (!IgnoredTestExtensions.Contains(extName))
                    {
                        names.Add(extName);
                    }
                }
            }
        }

        // Fallback if ext directory doesn't exist yet (e.g. unit test stubs)
        if (names.Count == 0)
        {
            foreach (var rec in RecommendedExtensions)
            {
                names.Add(rec);
            }
        }

        return names
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(name => new PhpExtensionItem
            {
                Name = name,
                DllFileName = $"php_{name}.dll",
                IsZendExtension = string.Equals(name, "opcache", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(name, "xdebug", StringComparison.OrdinalIgnoreCase),
                IsEnabled = enabledSet.Contains(name)
            })
            .ToList();
    }

    public (bool Success, string Message) SetExtensionEnabled(PhpVersionInfo phpVersion, string extensionName, bool enabled)
    {
        EnsurePhpIniConfigured(phpVersion);
        var iniPath = Path.Combine(phpVersion.DirectoryPath, "php.ini");
        if (!File.Exists(iniPath))
        {
            return (false, "File php.ini belum tersedia.");
        }

        try
        {
            var lines = File.ReadAllLines(iniPath, Encoding.UTF8).ToList();
            var isZend = string.Equals(extensionName, "opcache", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(extensionName, "xdebug", StringComparison.OrdinalIgnoreCase);
            var directiveKey = isZend ? "zend_extension" : "extension";
            var targetDirective = $"{directiveKey}={extensionName}";

            var pattern = new Regex(
                $@"^\s*;?\s*(?:zend_)?extension\s*=\s*[""']?(?:php_)?{Regex.Escape(extensionName)}(?:\.dll)?[""']?\s*(;.*)?$",
                RegexOptions.IgnoreCase);

            var found = false;
            for (var i = 0; i < lines.Count; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    lines[i] = enabled ? targetDirective : $";{targetDirective}";
                    found = true;
                }
            }

            if (!found && enabled)
            {
                lines.Add(targetDirective);
            }

            // Ensure extension_dir = "ext" is active
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Equals(";extension_dir = \"ext\"", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = "extension_dir = \"ext\"";
                }
            }

            File.WriteAllLines(iniPath, lines, new UTF8Encoding(false));
            var statusWord = enabled ? "diaktifkan" : "dinonaktifkan";
            _logger.Info("PHP", $"Ekstensi PHP '{extensionName}' berhasil {statusWord} pada {iniPath}.");
            return (true, $"Ekstensi PHP '{extensionName}' berhasil {statusWord} di php.ini.");
        }
        catch (Exception ex)
        {
            _logger.Error("PHP", $"Gagal mengubah status ekstensi '{extensionName}'.", ex);
            return (false, $"Gagal memperbarui php.ini: {ex.Message}");
        }
    }

    public (bool Success, string Message) EnableRecommendedExtensions(PhpVersionInfo phpVersion)
    {
        EnsurePhpIniConfigured(phpVersion);
        foreach (var ext in RecommendedExtensions)
        {
            SetExtensionEnabled(phpVersion, ext, enabled: true);
        }

        return (true, $"Seluruh ekstensi rekomendasi Laravel & Web ({string.Join(", ", RecommendedExtensions)}) telah diaktifkan di php.ini.");
    }

    private static bool TryAddPhpDirectory(
        string directory,
        bool isSystemPath,
        List<PhpVersionInfo> targetList,
        HashSet<string> seenDirs)
    {
        var fullDir = Path.GetFullPath(directory);
        if (seenDirs.Contains(fullDir))
        {
            return false;
        }

        var phpExe = Path.Combine(fullDir, "php.exe");
        var phpCgiExe = Path.Combine(fullDir, "php-cgi.exe");

        if (!File.Exists(phpExe) && !File.Exists(phpCgiExe))
        {
            return false;
        }

        seenDirs.Add(fullDir);
        var primaryBin = File.Exists(phpCgiExe) ? phpCgiExe : phpExe;
        var version = ReadExecutableVersion(primaryBin);
        var folderLabel = Path.GetFileName(fullDir.TrimEnd(Path.DirectorySeparatorChar));

        var id = isSystemPath ? $"system-{version}" : $"{folderLabel}-{version}";
        var displayName = isSystemPath
            ? $"PHP {version} (System PATH)"
            : $"PHP {version} ({folderLabel})";

        targetList.Add(new PhpVersionInfo
        {
            Id = id,
            DisplayName = displayName,
            VersionNumber = version,
            DirectoryPath = fullDir,
            PhpExePath = File.Exists(phpExe) ? phpExe : string.Empty,
            PhpCgiExePath = File.Exists(phpCgiExe) ? phpCgiExe : string.Empty,
            PhpIniPath = Path.Combine(fullDir, "php.ini"),
            IsFromSystemPath = isSystemPath
        });

        return true;
    }

    private static bool TryAddDatabaseDirectory(
        string directory,
        bool isSystemPath,
        List<DatabaseVersionInfo> targetList,
        HashSet<string> seenDirs)
    {
        var fullDir = Path.GetFullPath(directory);
        if (seenDirs.Contains(fullDir))
        {
            return false;
        }

        var mysqldBin = Path.Combine(fullDir, "bin", "mysqld.exe");
        var mariadbdBin = Path.Combine(fullDir, "bin", "mariadbd.exe");
        if (!File.Exists(mysqldBin) && !File.Exists(mariadbdBin))
        {
            return false;
        }

        seenDirs.Add(fullDir);
        var primaryExe = File.Exists(mysqldBin) ? mysqldBin : mariadbdBin;
        var isMariaDb = File.Exists(mariadbdBin) || File.Exists(Path.Combine(fullDir, "bin", "mysql_install_db.exe"));
        var engineName = isMariaDb ? "MariaDB" : "MySQL";
        var version = ReadExecutableVersion(primaryExe);
        var folderLabel = Path.GetFileName(fullDir.TrimEnd(Path.DirectorySeparatorChar));

        var id = $"{folderLabel}-{version}";
        var displayName = $"{engineName} {version} ({folderLabel})";

        targetList.Add(new DatabaseVersionInfo
        {
            Id = id,
            DisplayName = displayName,
            EngineType = engineName,
            VersionNumber = version,
            DirectoryPath = fullDir,
            MysqldExePath = primaryExe,
            DataDirectoryPath = Path.Combine(fullDir, "data"),
            IsFromSystemPath = isSystemPath
        });

        return true;
    }

    private static string ReadExecutableVersion(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.ProductVersion))
            {
                return info.ProductVersion.Split(' ')[0].Trim();
            }

            if (!string.IsNullOrWhiteSpace(info.FileVersion))
            {
                return info.FileVersion.Split(' ')[0].Trim();
            }
        }
        catch
        {
            // Ignore
        }

        return "8.x";
    }
}
