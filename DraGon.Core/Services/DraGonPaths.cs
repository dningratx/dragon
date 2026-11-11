namespace DraGon.Core.Services;

public static class DraGonPaths
{
    public const string ManagedHostTag = "# [DraGon-Managed]";

    public static string DefaultCustomApplicationDirectory => @"C:\DraGon";

    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DraGon");

    public static string ConfigDirectory => Path.Combine(AppDataRoot, "config");
    public static string GeneratedServerConfigDirectory => Path.Combine(ConfigDirectory, "generated");
    public static string WebToolsDirectory => Path.Combine(GeneratedServerConfigDirectory, "tools");
    public static string PhpInfoFilePath => Path.Combine(WebToolsDirectory, "phpinfo.php");
    public static string CertificatesDirectory => Path.Combine(AppDataRoot, "certs");
    public static string DownloadsCacheDirectory => Path.Combine(AppDataRoot, "downloads");
    public static string LogsDirectory => Path.Combine(AppDataRoot, "logs");
    public static string BackupsDirectory => Path.Combine(AppDataRoot, "backups");
    public static string UpdatesDirectory => Path.Combine(AppDataRoot, "updates");
    public static string DefaultRuntimesDirectory => Path.Combine(AppDataRoot, "runtimes");
    public static string TempDirectory => Path.Combine(AppDataRoot, "temp");

    public static readonly Version CurrentAppVersion = typeof(DraGonPaths).Assembly.GetName().Version ?? new Version(1, 0, 0);
    public static string CurrentAppVersionString => $"{CurrentAppVersion.Major}.{CurrentAppVersion.Minor}.{CurrentAppVersion.Build}";

    public static string DefaultWorkspaceDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DraGon-Workspace");

    public static string SettingsFilePath => Path.Combine(ConfigDirectory, "settings.json");
    public static string ProjectsFilePath => Path.Combine(ConfigDirectory, "projects.json");
    public static string MainLogFilePath => Path.Combine(LogsDirectory, "dragon.log");

    public static string RootCaPfxPath => Path.Combine(CertificatesDirectory, "DraGon-Local-RootCA.pfx");
    public static string RootCaCerPath => Path.Combine(CertificatesDirectory, "DraGon-Local-RootCA.crt");

    public static string SystemHostsFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

    public static string ResolveEffectiveApplicationDirectory(string? configuredAppFolder = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredAppFolder))
        {
            return configuredAppFolder;
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!baseDir.Contains(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) &&
            !baseDir.Contains(@"\bin\Release", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(Path.Combine(baseDir, "DraGon.App.exe")))
        {
            return baseDir;
        }

        return DefaultCustomApplicationDirectory;
    }

    public static string ResolveBestRuntimeDirectory(string? configuredRuntimeDir = null, string? configuredAppFolder = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredRuntimeDir) && HasInstalledRuntimes(configuredRuntimeDir))
        {
            return configuredRuntimeDir;
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredAppFolder))
        {
            candidates.Add(Path.Combine(configuredAppFolder, "runtimes"));
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        candidates.Add(Path.Combine(baseDir, "runtimes"));
        candidates.Add(DefaultRuntimesDirectory);
        candidates.Add(Path.Combine(DefaultCustomApplicationDirectory, "runtimes"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (HasInstalledRuntimes(candidate))
            {
                return candidate;
            }
        }

        return !string.IsNullOrWhiteSpace(configuredRuntimeDir)
            ? configuredRuntimeDir
            : DefaultRuntimesDirectory;
    }

    public static bool HasInstalledRuntimes(string runtimeBaseDir)
    {
        if (string.IsNullOrWhiteSpace(runtimeBaseDir) || !Directory.Exists(runtimeBaseDir))
        {
            return false;
        }

        var hasNginx = File.Exists(Path.Combine(runtimeBaseDir, "nginx", "nginx.exe")) ||
            RuntimePackageCatalogService.FindExecutableInRuntimeFolder(Path.Combine(runtimeBaseDir, "nginx"), "nginx.exe") is not null;
        var hasPhp = File.Exists(Path.Combine(runtimeBaseDir, "php", "php-cgi.exe")) ||
            RuntimePackageCatalogService.FindExecutableInRuntimeFolder(Path.Combine(runtimeBaseDir, "php"), "php-cgi.exe") is not null;
        var hasApache = File.Exists(Path.Combine(runtimeBaseDir, "apache", "bin", "httpd.exe")) ||
            RuntimePackageCatalogService.FindExecutableInRuntimeFolder(Path.Combine(runtimeBaseDir, "apache"), @"bin\httpd.exe") is not null;

        return hasNginx || hasPhp || hasApache;
    }

    public static void EnsureDirectoriesExist(
        string? customRuntimeDir = null,
        string? customWorkspaceDir = null,
        string? customAppDir = null)
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(GeneratedServerConfigDirectory);
        Directory.CreateDirectory(WebToolsDirectory);
        Directory.CreateDirectory(CertificatesDirectory);
        Directory.CreateDirectory(DownloadsCacheDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(UpdatesDirectory);
        Directory.CreateDirectory(DefaultRuntimesDirectory);
        Directory.CreateDirectory(TempDirectory);

        if (!string.IsNullOrWhiteSpace(customAppDir))
        {
            try
            {
                Directory.CreateDirectory(customAppDir);
            }
            catch
            {
                // Ignore if custom app path cannot be created yet
            }
        }

        var runtimeDir = string.IsNullOrWhiteSpace(customRuntimeDir) ? DefaultRuntimesDirectory : customRuntimeDir;
        try
        {
            Directory.CreateDirectory(runtimeDir);
            Directory.CreateDirectory(Path.Combine(runtimeDir, "nginx"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "apache"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "php"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "mariadb"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "postgresql"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "git"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "phpmyadmin"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "nodejs"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "composer"));
        }
        catch
        {
            // Ignore if custom path cannot be created yet
        }

        var workspaceDir = string.IsNullOrWhiteSpace(customWorkspaceDir) ? DefaultWorkspaceDirectory : customWorkspaceDir;
        try
        {
            Directory.CreateDirectory(workspaceDir);
        }
        catch
        {
            // Ignore if custom workspace path is not ready
        }
    }
}
