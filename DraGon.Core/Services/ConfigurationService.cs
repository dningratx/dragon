using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DraGon.Core.Models;
using Microsoft.Win32;

namespace DraGon.Core.Services;

public sealed class ConfigurationService
{
    private const string WindowsRunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryAppName = "DraGon";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppLogger _logger;

    public ConfigurationService(AppLogger logger)
    {
        _logger = logger;
    }

    public AppSettings LoadSettings()
    {
        DraGonPaths.EnsureDirectoriesExist();

        try
        {
            if (File.Exists(DraGonPaths.SettingsFilePath))
            {
                var json = File.ReadAllText(DraGonPaths.SettingsFilePath, Encoding.UTF8);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    NormalizeSettings(settings);
                    DraGonPaths.EnsureDirectoriesExist(settings.RuntimeBasePath, settings.WorkspacePath, settings.ApplicationFolderPath);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Config", "Gagal memuat settings.json, menggunakan pengaturan bawaan.", ex);
        }

        var defaults = CreateDefaultSettings();
        SaveSettings(defaults);
        return defaults;
    }

    public void SaveSettings(AppSettings settings)
    {
        NormalizeSettings(settings);
        DraGonPaths.EnsureDirectoriesExist(settings.RuntimeBasePath, settings.WorkspacePath, settings.ApplicationFolderPath);

        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tempPath = DraGonPaths.SettingsFilePath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, DraGonPaths.SettingsFilePath, overwrite: true);
            _logger.Info("Config", "Pengaturan aplikasi berhasil disimpan.");
        }
        catch (Exception ex)
        {
            _logger.Error("Config", "Gagal menyimpan settings.json.", ex);
            throw;
        }
    }

    public List<ProjectItem> LoadProjects()
    {
        DraGonPaths.EnsureDirectoriesExist();

        try
        {
            if (File.Exists(DraGonPaths.ProjectsFilePath))
            {
                var json = File.ReadAllText(DraGonPaths.ProjectsFilePath, Encoding.UTF8);
                var projects = JsonSerializer.Deserialize<List<ProjectItem>>(json, JsonOptions);
                if (projects is not null)
                {
                    return projects;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Config", "Gagal memuat projects.json.", ex);
        }

        return [];
    }

    public void SaveProjects(IEnumerable<ProjectItem> projects)
    {
        DraGonPaths.EnsureDirectoriesExist();

        try
        {
            var list = projects.ToList();
            var json = JsonSerializer.Serialize(list, JsonOptions);
            var tempPath = DraGonPaths.ProjectsFilePath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, DraGonPaths.ProjectsFilePath, overwrite: true);
            _logger.Info("Config", $"Daftar proyek berhasil disimpan ({list.Count} proyek).");
        }
        catch (Exception ex)
        {
            _logger.Error("Config", "Gagal menyimpan projects.json.", ex);
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    public bool ApplyWindowsStartupSetting(bool enable, string? executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsRunRegistryKey, writable: true);
            if (key is null)
            {
                return false;
            }

            if (enable)
            {
                if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                {
                    return false;
                }

                key.SetValue(RegistryAppName, $"\"{executablePath}\"");
                _logger.Info("Startup", "Opsi jalankan DraGon saat Windows masuk diaktifkan.");
            }
            else
            {
                if (key.GetValue(RegistryAppName) is not null)
                {
                    key.DeleteValue(RegistryAppName, throwOnMissingValue: false);
                }

                _logger.Info("Startup", "Opsi jalankan DraGon saat Windows masuk dinonaktifkan.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Startup", "Gagal memperbarui entri startup Windows di Registry HKCU.", ex);
            return false;
        }
    }

    public static AppSettings CreateDefaultSettings()
    {
        var appDir = DraGonPaths.ResolveEffectiveApplicationDirectory();
        var runtimeDir = DraGonPaths.ResolveBestRuntimeDirectory(null, appDir);
        return new AppSettings
        {
            ApplicationFolderPath = appDir,
            WorkspacePath = DraGonPaths.DefaultWorkspaceDirectory,
            RuntimeBasePath = runtimeDir,
            PreferredWebServer = WebServerType.Apache,
            WebServerPort = 8080,
            WebServerHttpsPort = 8443,
            ApachePort = 80,
            ApacheHttpsPort = 443,
            DatabasePort = 3306,
            PhpCgiPort = 9000,
            EnableLocalHttps = true,
            SelectedPhpVersionId = string.Empty,
            SelectedDatabaseVersionId = string.Empty,
            PreferredBrowser = "SystemDefault",
            CustomBrowserPath = string.Empty,
            RunOnWindowsStartup = false,
            Theme = AppTheme.Dark,
            HostnameSuffix = ".dra",
            AutoCheckUpdatesOnStartup = true,
            UpdateFeedUrl = string.Empty
        };
    }

    private static void NormalizeSettings(AppSettings settings)
    {
        settings.HostnameSuffix = ValidationService.NormalizeHostnameSuffix(settings.HostnameSuffix);

        if (string.IsNullOrWhiteSpace(settings.ApplicationFolderPath))
        {
            settings.ApplicationFolderPath = DraGonPaths.ResolveEffectiveApplicationDirectory();
        }

        if (string.IsNullOrWhiteSpace(settings.WorkspacePath))
        {
            settings.WorkspacePath = DraGonPaths.DefaultWorkspaceDirectory;
        }

        settings.RuntimeBasePath = DraGonPaths.ResolveBestRuntimeDirectory(
            settings.RuntimeBasePath,
            settings.ApplicationFolderPath);

        if (settings.WebServerPort is < 1 or > 65535)
        {
            settings.WebServerPort = 8080;
        }

        if (settings.WebServerHttpsPort is < 1 or > 65535)
        {
            settings.WebServerHttpsPort = 8443;
        }

        if (settings.ApachePort is < 1 or > 65535)
        {
            settings.ApachePort = 80;
        }

        if (settings.ApacheHttpsPort is < 1 or > 65535)
        {
            settings.ApacheHttpsPort = 443;
        }

        if (settings.DatabasePort is < 1 or > 65535)
        {
            settings.DatabasePort = 3306;
        }

        if (settings.PhpCgiPort is < 1 or > 65535)
        {
            settings.PhpCgiPort = 9000;
        }

        if (string.IsNullOrWhiteSpace(settings.PreferredBrowser))
        {
            settings.PreferredBrowser = "SystemDefault";
        }
    }
}
