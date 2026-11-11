using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DraGon.App.Themes;
using DraGon.Core.Models;
using DraGon.Core.Services;
using Microsoft.Win32;

namespace DraGon.App.ViewModels;

public enum NotificationTone
{
    Info,
    Success,
    Warning,
    Error
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppLogger _logger;
    private readonly ConfigurationService _configService;
    private readonly HostsFileService _hostsService;
    private readonly LocalSslService _sslService;
    private readonly PhpVersionManagerService _phpVersionManager;
    private readonly ServerConfigGenerator _vhostGenerator;
    private readonly ServiceProcessManager _serviceManager;
    private readonly ProjectDevProcessManager _projectDevProcessManager;
    private readonly RuntimePackageCatalogService _packageCatalogService;
    private readonly RuntimeInstallerService _runtimeInstallerService;
    private readonly ExternalLauncherService _launcherService;
    private readonly ProjectScaffoldingService _scaffoldingService;
    private readonly AppUpdateService _updateService;

    private AppSettings _activeSettings;

    // Navigation: "Dashboard", "Projects", "Services", "Packages", "Settings"
    [ObservableProperty]
    private string _currentSection = "Dashboard";

    // Global busy / loading state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartAllServices))]
    [NotifyPropertyChangedFor(nameof(CanStopAllServices))]
    [NotifyPropertyChangedFor(nameof(CanReloadServices))]
    [NotifyCanExecuteChangedFor(nameof(StartAllServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopAllServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadServicesCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyMessage = string.Empty;

    // Notification Banner
    [ObservableProperty]
    private bool _hasNotification;

    [ObservableProperty]
    private string _notificationTitle = string.Empty;

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    [ObservableProperty]
    private NotificationTone _notificationTone = NotificationTone.Info;

    // Project Form (Add / Edit)
    [ObservableProperty]
    private bool _isProjectFormOpen;

    [ObservableProperty]
    private bool _isEditingExistingProject;

    [ObservableProperty]
    private Guid? _editingProjectId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGitCloneSelected))]
    [NotifyPropertyChangedFor(nameof(IsExistingFolderSelected))]
    [NotifyPropertyChangedFor(nameof(SaveProjectButtonText))]
    private string _formProjectType = "Standard";

    [ObservableProperty]
    private string _formGitUrl = string.Empty;

    public bool IsGitCloneSelected => string.Equals(FormProjectType, "GitClone", StringComparison.OrdinalIgnoreCase);
    public bool IsExistingFolderSelected => string.Equals(FormProjectType, "Existing", StringComparison.OrdinalIgnoreCase);

    public string SaveProjectButtonText => FormProjectType switch
    {
        "Laravel" => "🚀 Buat Proyek Laravel",
        "WordPress" => "🌐 Pasang WordPress",
        "GitClone" => "🐙 Clone Proyek Git",
        _ => IsEditingExistingProject ? "Simpan Perubahan" : "📁 Buat Proyek Baru"
    };

    [ObservableProperty]
    private string _formProjectName = string.Empty;

    [ObservableProperty]
    private string _formProjectFolderPath = string.Empty;

    [ObservableProperty]
    private string _formProjectDomain = string.Empty;

    [ObservableProperty]
    private bool _formEnableHttps = true;

    [ObservableProperty]
    private bool _formAutoSyncHosts = true;

    [ObservableProperty]
    private string _formDetectedFrameworkHint = "Ketik nama proyek untuk langsung membuat folder di direktori workspace.";

    // Search Filter in Projects tab
    [ObservableProperty]
    private string _projectSearchText = string.Empty;

    // Multi-PHP, Multi-DB & SSL Status
    [ObservableProperty]
    private PhpVersionInfo? _selectedPhpVersion;

    [ObservableProperty]
    private PhpVersionInfo? _selectedPhpVersionCandidate;

    [ObservableProperty]
    private string _activePhpSummaryText = "Belum ada versi PHP terdeteksi";

    [ObservableProperty]
    private DatabaseVersionInfo? _selectedDatabaseVersion;

    [ObservableProperty]
    private DatabaseVersionInfo? _selectedDatabaseVersionCandidate;

    [ObservableProperty]
    private string _activeDatabaseSummaryText = "Belum ada versi Database terdeteksi";

    [ObservableProperty]
    private DownloadableVersionPreset? _selectedPhpDownloadPreset;

    [ObservableProperty]
    private DownloadableVersionPreset? _selectedDatabaseDownloadPreset;

    [ObservableProperty]
    private bool _isDownloadingVersionPreset;

    [ObservableProperty]
    private double _versionPresetDownloadPercent;

    [ObservableProperty]
    private string _versionPresetDownloadStatusText = string.Empty;

    [ObservableProperty]
    private bool _isRootCaTrusted;

    [ObservableProperty]
    private string _rootCaStatusText = "Memeriksa sertifikat Root CA Lokal...";

    // Editable Settings Fields in Settings tab
    [ObservableProperty]
    private string _settingsApplicationFolderPath = string.Empty;

    [ObservableProperty]
    private string _settingsWorkspacePath = string.Empty;

    [ObservableProperty]
    private string _settingsRuntimeBasePath = string.Empty;

    [ObservableProperty]
    private WebServerType _settingsPreferredWebServer = WebServerType.Apache;

    [ObservableProperty]
    private int _settingsWebServerPort = 8080;

    [ObservableProperty]
    private int _settingsWebServerHttpsPort = 8443;

    [ObservableProperty]
    private int _settingsApachePort = 80;

    [ObservableProperty]
    private int _settingsApacheHttpsPort = 443;

    [ObservableProperty]
    private int _settingsDatabasePort = 3306;

    [ObservableProperty]
    private int _settingsPhpCgiPort = 9000;

    [ObservableProperty]
    private bool _settingsEnableLocalHttps = true;

    [ObservableProperty]
    private string _settingsPreferredBrowser = "SystemDefault";

    [ObservableProperty]
    private string _settingsCustomBrowserPath = string.Empty;

    [ObservableProperty]
    private bool _settingsRunOnStartup;

    [ObservableProperty]
    private AppTheme _settingsTheme = AppTheme.Dark;

    [ObservableProperty]
    private string _settingsHostnameSuffix = ".dra";

    [ObservableProperty]
    private string _activeHostnameSuffix = ".dra";

    [ObservableProperty]
    private string _hostnameSuffixBadgeText = "HTTP & HTTPS Lokal: *.dra";

    public string FormDomainLabelText => $"Domain Lokal (misal: tokoku{ActiveHostnameSuffix})";

    partial void OnActiveHostnameSuffixChanged(string value)
    {
        OnPropertyChanged(nameof(FormDomainLabelText));
    }

    // Summary properties for Dashboard & Sidebar
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartAllServices))]
    [NotifyPropertyChangedFor(nameof(CanStopAllServices))]
    [NotifyPropertyChangedFor(nameof(CanReloadServices))]
    [NotifyCanExecuteChangedFor(nameof(StartAllServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopAllServicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadServicesCommand))]
    private int _runningServicesCount;

    [ObservableProperty]
    private string _environmentSummaryText = "Memeriksa status layanan...";

    [ObservableProperty]
    private string _activePortsSummaryText = "Web: 80/443 | PHP-CGI: 9000 | DB: 3306";

    public ObservableCollection<ServiceState> Services { get; } = [];
    public ObservableCollection<ServiceState> InstalledServices { get; } = [];
    public ObservableCollection<ProjectItem> Projects { get; } = [];
    public ObservableCollection<ProjectItem> FilteredProjects { get; } = [];
    public ObservableCollection<ProjectItem> RecentProjects { get; } = [];
    public ObservableCollection<RuntimePackageInfo> RuntimePackages { get; } = [];
    public ObservableCollection<PhpVersionInfo> PhpVersions { get; } = [];
    public ObservableCollection<DatabaseVersionInfo> DatabaseVersions { get; } = [];
    public ObservableCollection<DownloadableVersionPreset> PhpDownloadPresets { get; } = [];
    public ObservableCollection<DownloadableVersionPreset> DatabaseDownloadPresets { get; } = [];
    public ObservableCollection<PhpExtensionItem> PhpExtensions { get; } = [];
    public ObservableCollection<string> ApplicationLogs { get; } = [];

    // ==================== PEMBARUAN APLIKASI (PATCH / AUTO UPDATE) ====================
    [ObservableProperty]
    private string _currentAppVersion = DraGonPaths.CurrentAppVersionString;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private double _updateDownloadPercent;

    [ObservableProperty]
    private string _updateDownloadStatusText = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private AppUpdateManifest? _availableUpdateManifest;

    [ObservableProperty]
    private bool _isPatchUpdateAvailable;

    [ObservableProperty]
    private string _updateStatusSummary = $"DraGon v{DraGonPaths.CurrentAppVersionString} siap digunakan.";

    [ObservableProperty]
    private bool _settingsAutoCheckUpdates = true;

    [ObservableProperty]
    private string _settingsUpdateFeedUrl = string.Empty;

    public ObservableCollection<string> UpdateChangedFiles { get; } = [];

    public IReadOnlyList<WebServerType> AvailableWebServers { get; } = [WebServerType.Apache, WebServerType.Nginx];
    public IReadOnlyList<string> AvailableBrowsers { get; } = ["SystemDefault", "Chrome", "Edge", "Firefox", "Custom"];
    public IReadOnlyList<AppTheme> AvailableThemes { get; } = [AppTheme.Dark, AppTheme.Light];

    public string AppDataFolderDisplay => DraGonPaths.AppDataRoot;
    public string ConfigFolderDisplay => DraGonPaths.ConfigDirectory;
    public string CertsFolderDisplay => DraGonPaths.CertificatesDirectory;
    public string LogsFolderDisplay => DraGonPaths.LogsDirectory;
    public string BackupsFolderDisplay => DraGonPaths.BackupsDirectory;
    public string SystemHostsFileDisplay => DraGonPaths.SystemHostsFilePath;

    public bool HasAnyProjects => Projects.Count > 0;
    public bool HasFilteredProjects => FilteredProjects.Count > 0;
    public bool HasRunningServices => _serviceManager.HasRunningServices || _projectDevProcessManager.HasRunningProjectProcesses;
    public bool CanStartAllServices => !IsBusy && RunningServicesCount == 0;
    public bool CanStopAllServices => !IsBusy && RunningServicesCount > 0;
    public bool CanReloadServices => !IsBusy && RunningServicesCount > 0;

    public MainViewModel()
    {
        _logger = new AppLogger();
        _configService = new ConfigurationService(_logger);
        _hostsService = new HostsFileService(_logger);
        _sslService = new LocalSslService(_logger);
        _phpVersionManager = new PhpVersionManagerService(_logger);
        _vhostGenerator = new ServerConfigGenerator(_logger, _sslService);
        _serviceManager = new ServiceProcessManager(_logger, _vhostGenerator, _phpVersionManager);
        _projectDevProcessManager = new ProjectDevProcessManager(_logger, _phpVersionManager);
        _packageCatalogService = new RuntimePackageCatalogService();
        _runtimeInstallerService = new RuntimeInstallerService(_logger);
        _launcherService = new ExternalLauncherService(_logger);
        _scaffoldingService = new ProjectScaffoldingService(_logger);
        _updateService = new AppUpdateService(_logger);

        Action<Action> uiDispatcher = action =>
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(action);
            }
            else
            {
                action();
            }
        };

        _serviceManager.SetUiDispatcher(uiDispatcher);
        _projectDevProcessManager.SetUiDispatcher(uiDispatcher);

        _logger.EntryWritten += entry =>
        {
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => AppendAppLogLine(entry.Formatted));
            }
            else
            {
                AppendAppLogLine(entry.Formatted);
            }
        };

        foreach (var line in _logger.ReadRecentLines(120))
        {
            ApplicationLogs.Add(line);
        }

        _activeSettings = _configService.LoadSettings();
        ActiveHostnameSuffix = _activeSettings.HostnameSuffix;
        HostnameSuffixBadgeText = $"HTTP & HTTPS Lokal: *{_activeSettings.HostnameSuffix}";
        ThemeManager.ApplyTheme(_activeSettings.Theme);
        CopySettingsToForm(_activeSettings);

        foreach (var svc in _serviceManager.Services)
        {
            svc.PropertyChanged += (_, _) => UpdateDashboardSummary();
            Services.Add(svc);
        }

        foreach (var pkg in _packageCatalogService.CreateCatalog())
        {
            RuntimePackages.Add(pkg);
        }

        foreach (var preset in _runtimeInstallerService.GetPhpVersionPresets())
        {
            PhpDownloadPresets.Add(preset);
        }
        SelectedPhpDownloadPreset = PhpDownloadPresets.FirstOrDefault(p => p.FolderName.Contains("8.3")) ?? PhpDownloadPresets.FirstOrDefault();

        foreach (var preset in _runtimeInstallerService.GetDatabaseVersionPresets())
        {
            DatabaseDownloadPresets.Add(preset);
        }
        SelectedDatabaseDownloadPreset = DatabaseDownloadPresets.FirstOrDefault();

        LoadProjectsFromDisk();
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        RefreshAllEnvironmentStatus();
        EnvironmentPathService.SyncUserEnvironmentPath(_activeSettings.RuntimeBasePath, _activeSettings, _logger);

        _logger.Info("App", "DraGon siap digunakan (HTTPS Lokal, Multi-PHP, Node.js Dev Runner, & Installer Runtime aktif).");
        _ = AutoStartDefaultServicesOnLaunchAsync();
        _ = AutoCheckUpdateOnLaunchAsync();
    }

    private async Task AutoStartDefaultServicesOnLaunchAsync()
    {
        try
        {
            await Task.Delay(350);
            var primaryWeb = _activeSettings.PreferredWebServer == WebServerType.Apache
                ? ServiceKind.Apache
                : ServiceKind.Nginx;

            var webSvc = Services.FirstOrDefault(s => s.Kind == primaryWeb);
            if (webSvc is { IsRunning: false, IsMissingRuntime: false })
            {
                await StartAllServicesAsync();
            }
            else if (Projects.Any(p => p.DomainStatus != DomainSyncStatus.Synced))
            {
                await SyncAllDomainsToHostsInternalAsync("Menyinkronkan domain proyek (.dra) yang belum terdaftar ke file hosts Windows...");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("App", $"Auto-start layanan bawaan dilewati: {ex.Message}");
        }
    }

    partial void OnProjectSearchTextChanged(string value)
    {
        RebuildProjectCollections();
    }

    partial void OnSelectedPhpVersionChanged(PhpVersionInfo? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var item in PhpVersions)
        {
            item.IsActiveDefault = ReferenceEquals(item, value);
        }

        ActivePhpSummaryText = $"PHP Aktif: {value.DisplayName} — {value.DirectoryPath}";
        RefreshPhpExtensionsList();

        if (!string.Equals(_activeSettings.SelectedPhpVersionId, value.Id, StringComparison.OrdinalIgnoreCase))
        {
            _activeSettings.SelectedPhpVersionId = value.Id;
            _phpVersionManager.EnsurePhpIniConfigured(value);
            _configService.SaveSettings(_activeSettings);
            _serviceManager.RefreshServiceStates(_activeSettings);
            ShowNotification(
                "Versi PHP Diperbarui",
                $"Versi PHP aktif diubah ke {value.DisplayName}. Jika layanan PHP FastCGI sedang berjalan, mulai ulang layanan agar menggunakan versi baru.",
                NotificationTone.Info);
        }
    }

    partial void OnSelectedDatabaseVersionChanged(DatabaseVersionInfo? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var item in DatabaseVersions)
        {
            item.IsActiveDefault = ReferenceEquals(item, value);
        }

        ActiveDatabaseSummaryText = $"Database Aktif: {value.DisplayName} — {value.DirectoryPath}";

        if (!string.Equals(_activeSettings.SelectedDatabaseVersionId, value.Id, StringComparison.OrdinalIgnoreCase))
        {
            _activeSettings.SelectedDatabaseVersionId = value.Id;
            _configService.SaveSettings(_activeSettings);
            _serviceManager.RefreshServiceStates(_activeSettings);
            ShowNotification(
                "Versi Database Diperbarui",
                $"Versi MariaDB/MySQL aktif diubah ke {value.DisplayName}. Jika layanan database sedang berjalan, mulai ulang layanan agar menggunakan versi baru.",
                NotificationTone.Info);
        }
    }

    partial void OnFormProjectNameChanged(string value)
    {
        if (!IsEditingExistingProject && !string.Equals(FormProjectType, "Existing", StringComparison.OrdinalIgnoreCase))
        {
            AutoFillProjectFolderAndDomain(value);
        }
    }

    partial void OnFormProjectTypeChanged(string value)
    {
        if (!IsEditingExistingProject)
        {
            UpdateFormHintForProjectType(value);
            if (!string.IsNullOrWhiteSpace(FormProjectName) && !string.Equals(value, "Existing", StringComparison.OrdinalIgnoreCase))
            {
                AutoFillProjectFolderAndDomain(FormProjectName);
            }
        }
    }

    partial void OnFormGitUrlChanged(string value)
    {
        if (string.Equals(FormProjectType, "GitClone", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
        {
            if (string.IsNullOrWhiteSpace(FormProjectName))
            {
                var cleanUrl = value.Trim().TrimEnd('/');
                if (cleanUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    cleanUrl = cleanUrl[..^4];
                }
                var lastSegment = Path.GetFileName(cleanUrl);
                if (!string.IsNullOrWhiteSpace(lastSegment))
                {
                    FormProjectName = lastSegment;
                }
            }
        }
    }

    private void UpdateFormHintForProjectType(string typeName)
    {
        FormDetectedFrameworkHint = typeName switch
        {
            "Standard" => "📁 Mode Standar: Ketik nama, folder proyek langsung terbuat di workspace dengan template index.php siap pakai.",
            "Laravel" => "🔴 Mode Laravel: Otomatis menjalankan 'composer create-project laravel/laravel' di direktori workspace.",
            "WordPress" => "🔵 Mode WordPress: Otomatis mengunduh & mengekstrak rilis resmi WordPress terbaru dari wordpress.org.",
            "GitClone" => "🐙 Mode Git: Otomatis melakukan 'git clone' dari URL repositori ke dalam folder workspace.",
            _ => "📂 Mode Folder: Pilih folder proyek yang sudah ada pada komputer Anda."
        };
    }

    private void AutoFillProjectFolderAndDomain(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            var baseDir = !string.IsNullOrWhiteSpace(_activeSettings.WorkspacePath)
                ? _activeSettings.WorkspacePath
                : DraGonPaths.DefaultWorkspaceDirectory;
            FormProjectFolderPath = Path.Combine(baseDir, trimmed);
            FormProjectDomain = ValidationService.SuggestDomainFromFolderOrName(trimmed, _activeSettings.HostnameSuffix);
        }
    }

    partial void OnFormProjectFolderPathChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
        {
            if (!IsEditingExistingProject && !string.Equals(FormProjectType, "Existing", StringComparison.OrdinalIgnoreCase))
            {
                // Folder belum dibuat fisik, tidak masalah untuk proyek baru yang akan di-scaffold
                return;
            }
            FormDetectedFrameworkHint = "Pilih folder proyek yang valid pada komputer Anda.";
            return;
        }

        var folderName = Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!IsEditingExistingProject)
        {
            if (string.IsNullOrWhiteSpace(FormProjectName))
            {
                FormProjectName = folderName;
            }

            if (string.IsNullOrWhiteSpace(FormProjectDomain))
            {
                FormProjectDomain = ValidationService.SuggestDomainFromFolderOrName(folderName, _activeSettings.HostnameSuffix);
            }
        }

        var tempItem = new ProjectItem { FolderPath = value };
        ProjectDetectorService.AnalyzeProjectFolder(tempItem);
        FormDetectedFrameworkHint = tempItem.IsLaravel
            ? $"Terdeteksi Proyek {tempItem.FrameworkBadgeText} (artisan & composer.json ditemukan). Document Root: {tempItem.DocumentRoot}"
            : $"Terdeteksi Proyek {tempItem.FrameworkBadgeText}. Document Root: {tempItem.DocumentRoot}";
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        if (!string.IsNullOrWhiteSpace(section))
        {
            CurrentSection = section;
        }
    }

    [RelayCommand]
    private void DismissNotification()
    {
        HasNotification = false;
    }

    [RelayCommand]
    private void ToggleThemeQuick()
    {
        var nextTheme = _activeSettings.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        _activeSettings.Theme = nextTheme;
        SettingsTheme = nextTheme;
        ThemeManager.ApplyTheme(nextTheme);
        _configService.SaveSettings(_activeSettings);
        ShowNotification(
            "Tema Diperbarui",
            nextTheme == AppTheme.Dark ? "Tema Gelap (Dark Mode) diaktifkan." : "Tema Terang (Light Mode) diaktifkan.",
            NotificationTone.Info);
    }

    [RelayCommand]
    private void RefreshAllEnvironmentStatus()
    {
        RefreshPhpVersionsList();
        RefreshSslRootCaStatus();

        _serviceManager.RefreshServiceStates(_activeSettings);
        _packageCatalogService.RefreshCatalogStatus(RuntimePackages, _activeSettings.RuntimeBasePath);

        foreach (var project in Projects)
        {
            ProjectDetectorService.AnalyzeProjectFolder(project);
        }

        _hostsService.RefreshProjectsDomainStatus(Projects);
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        UpdateDashboardSummary();
    }

    private void RefreshPhpVersionsList()
    {
        var discovered = _phpVersionManager.DiscoverPhpVersions(
            _activeSettings.RuntimeBasePath,
            _activeSettings.SelectedPhpVersionId);

        PhpVersions.Clear();
        foreach (var v in discovered)
        {
            PhpVersions.Add(v);
        }

        var active = PhpVersions.FirstOrDefault(v => v.IsActiveDefault) ?? PhpVersions.FirstOrDefault();
        SelectedPhpVersion = active;
        SelectedPhpVersionCandidate = active;
        ActivePhpSummaryText = active is not null
            ? $"PHP Aktif: {active.DisplayName}"
            : "Belum ada runtime PHP yang terdeteksi di folder runtime atau PATH.";
        RefreshPhpExtensionsList();
        RefreshDatabaseVersionsList();
    }

    private void RefreshDatabaseVersionsList()
    {
        var discovered = _phpVersionManager.DiscoverDatabaseVersions(
            _activeSettings.RuntimeBasePath,
            _activeSettings.SelectedDatabaseVersionId);

        DatabaseVersions.Clear();
        foreach (var db in discovered)
        {
            DatabaseVersions.Add(db);
        }

        var active = DatabaseVersions.FirstOrDefault(v => v.IsActiveDefault) ?? DatabaseVersions.FirstOrDefault();
        SelectedDatabaseVersion = active;
        SelectedDatabaseVersionCandidate = active;
        ActiveDatabaseSummaryText = active is not null
            ? $"Database Aktif: {active.DisplayName}"
            : "Belum ada runtime MariaDB/MySQL yang terdeteksi di folder runtime.";
    }

    [RelayCommand]
    private async Task ApplySelectedPhpVersionAsync()
    {
        var target = SelectedPhpVersionCandidate ?? SelectedPhpVersion;
        if (target is null)
        {
            ShowNotification("Peringatan", "Pilih versi PHP yang sudah di-download terlebih dahulu.", NotificationTone.Warning);
            return;
        }

        _activeSettings.SelectedPhpVersionId = target.Id;
        _phpVersionManager.EnsurePhpIniConfigured(target);
        _configService.SaveSettings(_activeSettings);

        SelectedPhpVersion = target;
        SelectedPhpVersionCandidate = target;
        foreach (var item in PhpVersions)
        {
            item.IsActiveDefault = ReferenceEquals(item, target);
        }

        ActivePhpSummaryText = $"PHP Aktif: {target.DisplayName} — {target.DirectoryPath}";
        RefreshPhpExtensionsList();
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);

        // Jika layanan PHP FastCGI sedang berjalan, mulai ulang agar versi baru langsung aktif
        var phpService = Services.FirstOrDefault(s => s.Kind == ServiceKind.PhpCgi);
        bool restarted = false;
        if (phpService is not null && phpService.Status == ServiceStatus.Running)
        {
            await _serviceManager.StopServiceAsync(ServiceKind.PhpCgi);
            await Task.Delay(400);
            await _serviceManager.StartServiceAsync(ServiceKind.PhpCgi, _activeSettings, Projects);
            restarted = true;
        }

        _serviceManager.RefreshServiceStates(_activeSettings);
        EnvironmentPathService.SyncUserEnvironmentPath(_activeSettings.RuntimeBasePath, _activeSettings, _logger);

        var msg = restarted
            ? $"Versi {target.DisplayName} berhasil dipasang sebagai PHP aktif dan layanan PHP FastCGI telah dimulai ulang otomatis."
            : $"Versi {target.DisplayName} berhasil dipasang sebagai PHP aktif.";

        ShowNotification("Versi PHP Terpasang", msg, NotificationTone.Success);
        _logger.Info("PHP", msg);
    }

    [RelayCommand]
    private async Task ApplySelectedDatabaseVersionAsync()
    {
        var target = SelectedDatabaseVersionCandidate ?? SelectedDatabaseVersion;
        if (target is null)
        {
            ShowNotification("Peringatan", "Pilih versi Database yang sudah di-download terlebih dahulu.", NotificationTone.Warning);
            return;
        }

        _activeSettings.SelectedDatabaseVersionId = target.Id;
        _configService.SaveSettings(_activeSettings);

        SelectedDatabaseVersion = target;
        SelectedDatabaseVersionCandidate = target;
        foreach (var item in DatabaseVersions)
        {
            item.IsActiveDefault = ReferenceEquals(item, target);
        }

        ActiveDatabaseSummaryText = $"Database Aktif: {target.DisplayName} — {target.DirectoryPath}";
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);

        // Jika layanan Database sedang berjalan, mulai ulang agar versi baru langsung aktif
        var dbService = Services.FirstOrDefault(s => s.Kind == ServiceKind.MariaDb);
        bool restarted = false;
        if (dbService is not null && dbService.Status == ServiceStatus.Running)
        {
            await _serviceManager.StopServiceAsync(ServiceKind.MariaDb);
            await Task.Delay(500);
            await _serviceManager.StartServiceAsync(ServiceKind.MariaDb, _activeSettings, Projects);
            restarted = true;
        }

        _serviceManager.RefreshServiceStates(_activeSettings);
        EnvironmentPathService.SyncUserEnvironmentPath(_activeSettings.RuntimeBasePath, _activeSettings, _logger);

        var msg = restarted
            ? $"Versi {target.DisplayName} berhasil dipasang sebagai Database aktif dan layanan database telah dimulai ulang otomatis."
            : $"Versi {target.DisplayName} berhasil dipasang sebagai Database aktif.";

        ShowNotification("Versi Database Terpasang", msg, NotificationTone.Success);
        _logger.Info("Database", msg);
    }

    [RelayCommand]
    private async Task DownloadSelectedPhpPresetAsync()
    {
        if (SelectedPhpDownloadPreset is null || IsDownloadingVersionPreset)
        {
            return;
        }

        await RunPresetDownloadAsync(SelectedPhpDownloadPreset, isPhp: true);
    }

    [RelayCommand]
    private async Task DownloadSelectedDatabasePresetAsync()
    {
        if (SelectedDatabaseDownloadPreset is null || IsDownloadingVersionPreset)
        {
            return;
        }

        await RunPresetDownloadAsync(SelectedDatabaseDownloadPreset, isPhp: false);
    }

    private async Task RunPresetDownloadAsync(DownloadableVersionPreset preset, bool isPhp)
    {
        try
        {
            IsDownloadingVersionPreset = true;
            VersionPresetDownloadPercent = 2;
            VersionPresetDownloadStatusText = $"Memulai pengunduhan {preset.DisplayName}...";

            var progress = new Progress<(double Percent, string Status)>(report =>
            {
                VersionPresetDownloadPercent = report.Percent;
                VersionPresetDownloadStatusText = report.Status;
            });

            var (ok, msg, installedDir) = await _runtimeInstallerService.DownloadAndInstallVersionPresetAsync(
                preset,
                _activeSettings.RuntimeBasePath,
                progress);

            RefreshAllEnvironmentStatus();

            if (ok && !string.IsNullOrWhiteSpace(installedDir))
            {
                if (isPhp)
                {
                    var matchPhp = PhpVersions.FirstOrDefault(v =>
                        string.Equals(Path.GetFullPath(v.DirectoryPath), Path.GetFullPath(installedDir), StringComparison.OrdinalIgnoreCase));
                    if (matchPhp is not null)
                    {
                        SelectedPhpVersion = matchPhp;
                    }
                }
                else
                {
                    var matchDb = DatabaseVersions.FirstOrDefault(v =>
                        string.Equals(Path.GetFullPath(v.DirectoryPath), Path.GetFullPath(installedDir), StringComparison.OrdinalIgnoreCase));
                    if (matchDb is not null)
                    {
                        SelectedDatabaseVersion = matchDb;
                    }
                }
            }

            ShowNotification(
                ok ? $"{preset.DisplayName} Terpasang & Aktif!" : $"Gagal Memasang {preset.DisplayName}",
                msg,
                ok ? NotificationTone.Success : NotificationTone.Error);
        }
        finally
        {
            IsDownloadingVersionPreset = false;
        }
    }

    [RelayCommand]
    private async Task ImportAdditionalPhpZipAsync()
    {
        var phpPkg = RuntimePackages.FirstOrDefault(p => string.Equals(p.Id, "php", StringComparison.OrdinalIgnoreCase));
        if (phpPkg is not null)
        {
            await ImportPackageZipAsync(phpPkg);
        }
    }

    [RelayCommand]
    private async Task ImportAdditionalDatabaseZipAsync()
    {
        var dbPkg = RuntimePackages.FirstOrDefault(p => string.Equals(p.Id, "mariadb", StringComparison.OrdinalIgnoreCase));
        if (dbPkg is not null)
        {
            await ImportPackageZipAsync(dbPkg);
        }
    }

    private void RefreshPhpExtensionsList()
    {
        PhpExtensions.Clear();
        var active = SelectedPhpVersion ?? _phpVersionManager.GetActivePhpVersion(_activeSettings);
        if (active is null)
        {
            return;
        }

        foreach (var ext in _phpVersionManager.GetExtensions(active))
        {
            PhpExtensions.Add(ext);
        }
    }

    [RelayCommand]
    private async Task TogglePhpExtensionAsync(PhpExtensionItem? extensionItem)
    {
        var active = SelectedPhpVersion ?? _phpVersionManager.GetActivePhpVersion(_activeSettings);
        if (extensionItem is null || active is null)
        {
            return;
        }

        var (ok, msg) = _phpVersionManager.SetExtensionEnabled(active, extensionItem.Name, extensionItem.IsEnabled);
        var phpService = Services.FirstOrDefault(s => s.Kind == ServiceKind.PhpCgi);
        if (ok && phpService is { IsRunning: true })
        {
            await _serviceManager.StopServiceAsync(ServiceKind.PhpCgi);
            await _serviceManager.StartServiceAsync(ServiceKind.PhpCgi, _activeSettings, Projects);
            msg += " Layanan PHP FastCGI otomatis dimulai ulang.";
        }

        ShowNotification(
            ok ? $"Ekstensi PHP '{extensionItem.Name}' Diperbarui" : "Gagal Mengubah Ekstensi PHP",
            msg,
            ok ? NotificationTone.Success : NotificationTone.Error);
    }

    [RelayCommand]
    private async Task EnableRecommendedPhpExtensionsAsync()
    {
        var active = SelectedPhpVersion ?? _phpVersionManager.GetActivePhpVersion(_activeSettings);
        if (active is null)
        {
            ShowNotification("PHP Belum Terdeteksi", "Pilih versi PHP terlebih dahulu.", NotificationTone.Warning);
            return;
        }

        var (ok, msg) = _phpVersionManager.EnableRecommendedExtensions(active);
        RefreshPhpExtensionsList();

        var phpService = Services.FirstOrDefault(s => s.Kind == ServiceKind.PhpCgi);
        if (ok && phpService is { IsRunning: true })
        {
            await _serviceManager.StopServiceAsync(ServiceKind.PhpCgi);
            await _serviceManager.StartServiceAsync(ServiceKind.PhpCgi, _activeSettings, Projects);
            msg += " Layanan PHP FastCGI otomatis dimulai ulang.";
        }

        ShowNotification("Ekstensi Rekomendasi Diaktifkan", msg, ok ? NotificationTone.Success : NotificationTone.Warning);
    }

    [RelayCommand]
    private void OpenActivePhpIniFile()
    {
        var active = SelectedPhpVersion ?? _phpVersionManager.GetActivePhpVersion(_activeSettings);
        if (active is null)
        {
            ShowNotification("PHP Belum Terdeteksi", "Tidak ada versi PHP aktif.", NotificationTone.Warning);
            return;
        }

        _phpVersionManager.EnsurePhpIniConfigured(active);
        if (File.Exists(active.PhpIniPath))
        {
            _launcherService.OpenFileInDefaultViewer(active.PhpIniPath);
        }
    }

    [RelayCommand]
    private void OpenActivePhpExtFolder()
    {
        var active = SelectedPhpVersion ?? _phpVersionManager.GetActivePhpVersion(_activeSettings);
        if (active is null)
        {
            return;
        }

        var extDir = Path.Combine(active.DirectoryPath, "ext");
        if (Directory.Exists(extDir))
        {
            _launcherService.OpenFolder(extDir);
        }
        else if (Directory.Exists(active.DirectoryPath))
        {
            _launcherService.OpenFolder(active.DirectoryPath);
        }
    }

    private void RefreshSslRootCaStatus()
    {
        try
        {
            _sslService.EnsureRootCaCertificate().Dispose();
            if (OperatingSystem.IsWindows())
            {
                IsRootCaTrusted = _sslService.IsRootCaTrustedInCurrentUserStore();
                if (!IsRootCaTrusted)
                {
                    _sslService.TrustRootCaInCurrentUserStore();
                    IsRootCaTrusted = _sslService.IsRootCaTrustedInCurrentUserStore();
                }

                RootCaStatusText = IsRootCaTrusted
                    ? "Root CA Lokal DraGon sudah dipercaya di Windows (CurrentUser\\Root) — HTTPS .dra siap."
                    : "Root CA Lokal DraGon sudah dibuat, klik 'Percayai Root CA di Windows' agar browser tidak menampilkan peringatan SSL.";
            }
        }
        catch (Exception ex)
        {
            IsRootCaTrusted = false;
            RootCaStatusText = $"Status SSL: {ex.Message}";
        }
    }

    // ==================== SSL / HTTPS COMMANDS ====================

    [RelayCommand]
    private void TrustRootCa()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var (ok, msg) = _sslService.TrustRootCaInCurrentUserStore();
        RefreshSslRootCaStatus();
        ShowNotification(
            ok ? "Root CA Lokal Dipercaya" : "Gagal Memasang Root CA",
            msg,
            ok ? NotificationTone.Success : NotificationTone.Warning);
    }

    [RelayCommand]
    private async Task RegenerateAllSslCertificatesAsync()
    {
        _sslService.EnsureCertificatesForProjects(Projects);
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        await ReloadActiveWebServersAsync();
        ShowNotification(
            "Sertifikat SSL (.dra) Diperbarui",
            $"Sertifikat HTTPS lokal untuk {Projects.Count(p => p.HasSslCertificate)} domain .dra telah disiapkan dan diterapkan ke web server.",
            NotificationTone.Success);
    }

    [RelayCommand]
    private async Task ToggleProjectHttpsAsync(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        project.HttpsEnabled = !project.HttpsEnabled;
        _sslService.EnsureDomainCertificate(project.Domain);
        project.HasSslCertificate = true;

        _configService.SaveProjects(Projects);
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        await ReloadActiveWebServersAsync();

        ShowNotification(
            project.HttpsEnabled ? $"HTTPS Diaktifkan ({project.Domain})" : $"HTTPS Dinonaktifkan ({project.Domain})",
            project.HttpsEnabled
                ? $"Sertifikat SSL lokal diterbitkan dan konfigurasi vhost HTTPS untuk https://{project.Domain} telah aktif."
                : $"Proyek '{project.Name}' kini menggunakan protokol HTTP standar saat dibuka dari DraGon (HTTPS tetap didukung).",
            NotificationTone.Info);
    }

    private async Task ReloadActiveWebServersAsync()
    {
        var primaryWeb = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? ServiceKind.Apache
            : ServiceKind.Nginx;

        foreach (var webKind in new[] { ServiceKind.Apache, ServiceKind.Nginx })
        {
            var svc = Services.FirstOrDefault(s => s.Kind == webKind);
            if (svc is { IsRunning: true })
            {
                await _serviceManager.StopServiceAsync(webKind);
                await _serviceManager.StartServiceAsync(webKind, _activeSettings, Projects);
            }
        }

        // Pastikan web server utama tetap berjalan setelah perubahan proyek
        var primarySvc = Services.FirstOrDefault(s => s.Kind == primaryWeb);
        if (primarySvc is { IsRunning: false, IsMissingRuntime: false })
        {
            await _serviceManager.StartServiceAsync(primaryWeb, _activeSettings, Projects);
        }

        var phpSvc = Services.FirstOrDefault(s => s.Kind == ServiceKind.PhpCgi);
        if (phpSvc is { IsRunning: true })
        {
            await _serviceManager.StopServiceAsync(ServiceKind.PhpCgi);
            await _serviceManager.StartServiceAsync(ServiceKind.PhpCgi, _activeSettings, Projects);
        }
        else if (phpSvc is { IsRunning: false, IsMissingRuntime: false })
        {
            await _serviceManager.StartServiceAsync(ServiceKind.PhpCgi, _activeSettings, Projects);
        }

        _serviceManager.RefreshServiceStates(_activeSettings);
        UpdateDashboardSummary();
    }

    [RelayCommand]
    private void OpenCertsFolder()
    {
        Directory.CreateDirectory(DraGonPaths.CertificatesDirectory);
        _launcherService.OpenFolder(DraGonPaths.CertificatesDirectory);
    }

    // ==================== SERVICE COMMANDS ====================

    [RelayCommand(CanExecute = nameof(CanStartAllServices))]
    private async Task StartAllServicesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            SetBusyState(true, "Memulai seluruh layanan utama (Web Server, PHP FastCGI, Database)...");
            var (started, failed, summary) = await _serviceManager.StartAllAsync(_activeSettings, Projects);
            UpdateDashboardSummary();

            if (Projects.Any(p => p.DomainStatus != DomainSyncStatus.Synced))
            {
                SetBusyState(true, "Menyinkronkan domain lokal (.dra) ke file hosts Windows...");
                await SyncDomainsCoreAsync();
            }

            if (started > 0 && failed == 0)
            {
                ShowNotification("Layanan Berjalan", summary, NotificationTone.Success);
            }
            else if (started > 0)
            {
                ShowNotification("Sebagian Layanan Berjalan", summary, NotificationTone.Warning);
            }
            else
            {
                ShowNotification("Runtime Belum Siap / Port Konflik", summary, NotificationTone.Warning);
            }
        }
        finally
        {
            SetBusyState(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopAllServices))]
    private async Task StopAllServicesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            SetBusyState(true, "Menghentikan seluruh layanan yang sedang berjalan...");
            var summary = await _serviceManager.StopAllAsync();
            await _projectDevProcessManager.StopAllProjectDevTasksAsync();
            _serviceManager.RefreshServiceStates(_activeSettings);
            UpdateDashboardSummary();
            ShowNotification("Layanan Dihentikan", summary, NotificationTone.Info);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanReloadServices))]
    private async Task ReloadServicesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            SetBusyState(true, "Memuat ulang (reload) konfigurasi vhost dan layanan web aktif...");
            _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
            await ReloadActiveWebServersAsync();
            RefreshAllEnvironmentStatus();
            ShowNotification(
                "Layanan Berhasil Di-reload",
                "Konfigurasi virtual host diperbarui dan server web aktif telah dimuat ulang.",
                NotificationTone.Success);
        }
        catch (Exception ex)
        {
            _logger.Error("Reload", $"Gagal me-reload layanan: {ex.Message}");
            ShowNotification("Gagal Reload Layanan", ex.Message, NotificationTone.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    [RelayCommand]
    private async Task StartSingleServiceAsync(ServiceState? service)
    {
        if (service is null || IsBusy)
        {
            return;
        }

        try
        {
            SetBusyState(true, $"Memulai {service.DisplayName}...");
            var (success, message) = await _serviceManager.StartServiceAsync(service.Kind, _activeSettings, Projects);
            UpdateDashboardSummary();
            ShowNotification(
                success ? $"{service.DisplayName} Aktif" : $"Gagal Memulai {service.DisplayName}",
                message,
                success ? NotificationTone.Success : NotificationTone.Warning);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    [RelayCommand]
    private async Task StopSingleServiceAsync(ServiceState? service)
    {
        if (service is null || IsBusy)
        {
            return;
        }

        try
        {
            SetBusyState(true, $"Menghentikan {service.DisplayName}...");
            var (success, message) = await _serviceManager.StopServiceAsync(service.Kind);
            _serviceManager.RefreshServiceStates(_activeSettings);
            UpdateDashboardSummary();
            ShowNotification(
                $"{service.DisplayName} Dihentikan",
                message,
                success ? NotificationTone.Info : NotificationTone.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    [RelayCommand]
    private async Task InitializeMariaDbDatabaseAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            SetBusyState(true, "Menginisialisasi struktur data awal MariaDB/MySQL...");
            var dbDir = SelectedDatabaseVersion?.DirectoryPath ?? Path.Combine(_activeSettings.RuntimeBasePath, "mariadb");
            var (ok, msg) = await _runtimeInstallerService.TryInitializeMariaDbDataDirectoryAsync(dbDir);
            ShowNotification(
                ok ? "Database Siap" : "Inisialisasi Database Gagal",
                msg,
                ok ? NotificationTone.Success : NotificationTone.Warning);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    [RelayCommand]
    private void OpenServiceLogFile(ServiceState? service)
    {
        if (service is null)
        {
            return;
        }

        if (!File.Exists(service.LogFilePath))
        {
            ShowNotification("Log Belum Tersedia", $"File log untuk {service.DisplayName} belum terbentuk karena layanan belum menghasilkan output.", NotificationTone.Info);
            return;
        }

        var (ok, msg) = _launcherService.OpenFileInDefaultViewer(service.LogFilePath);
        if (!ok)
        {
            ShowNotification("Gagal Membuka Log", msg, NotificationTone.Error);
        }
    }

    [RelayCommand]
    private void OpenServiceRuntimeFolder(ServiceState? service)
    {
        if (service is null)
        {
            return;
        }

        var subfolder = service.Kind switch
        {
            ServiceKind.Nginx => "nginx",
            ServiceKind.PhpCgi => "php",
            ServiceKind.MariaDb => "mariadb",
            ServiceKind.Apache => "apache",
            ServiceKind.PostgreSql => "postgresql",
            _ => string.Empty
        };

        var target = Path.Combine(_activeSettings.RuntimeBasePath, subfolder);
        Directory.CreateDirectory(target);
        _launcherService.OpenFolder(target);
    }

    // ==================== PROJECT COMMANDS ====================

    [RelayCommand]
    private void OpenAddProjectForm()
    {
        CurrentSection = "Projects";
        IsEditingExistingProject = false;
        EditingProjectId = null;
        FormProjectType = "Standard";
        FormProjectName = string.Empty;
        FormGitUrl = string.Empty;
        FormProjectFolderPath = string.Empty;
        FormProjectDomain = string.Empty;
        FormEnableHttps = true;
        FormAutoSyncHosts = true;
        UpdateFormHintForProjectType("Standard");
        IsProjectFormOpen = true;
    }

    [RelayCommand]
    private void BrowseAndAddProject()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Proyek Web / PHP / Laravel / Node.js",
            InitialDirectory = Directory.Exists(_activeSettings.WorkspacePath)
                ? _activeSettings.WorkspacePath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            CurrentSection = "Projects";
            IsEditingExistingProject = false;
            EditingProjectId = null;
            FormProjectType = "Existing";
            FormProjectName = string.Empty;
            FormGitUrl = string.Empty;
            FormProjectDomain = string.Empty;
            FormProjectFolderPath = dialog.FolderName;
            FormEnableHttps = true;
            FormAutoSyncHosts = true;
            IsProjectFormOpen = true;
        }
    }

    [RelayCommand]
    private void BrowseProjectFolderForForm()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Sumber Proyek",
            InitialDirectory = Directory.Exists(_activeSettings.WorkspacePath)
                ? _activeSettings.WorkspacePath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            FormProjectFolderPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void OpenEditProjectForm(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        CurrentSection = "Projects";
        IsEditingExistingProject = true;
        EditingProjectId = project.Id;
        FormProjectType = "Existing";
        FormProjectName = project.Name;
        FormGitUrl = string.Empty;
        FormProjectFolderPath = project.FolderPath;
        FormProjectDomain = project.Domain;
        FormEnableHttps = project.HttpsEnabled;
        FormAutoSyncHosts = true;
        IsProjectFormOpen = true;
    }

    [RelayCommand]
    private void CancelProjectForm()
    {
        IsProjectFormOpen = false;
    }

    [RelayCommand]
    private async Task SaveProjectFormAsync()
    {
        var nameValidation = ValidationService.ValidateProjectName(FormProjectName);
        if (!nameValidation.IsValid)
        {
            ShowNotification("Validasi Nama Proyek", nameValidation.ErrorMessage, NotificationTone.Error);
            return;
        }

        var targetFolder = FormProjectFolderPath.Trim();
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            var baseDir = !string.IsNullOrWhiteSpace(_activeSettings.WorkspacePath)
                ? _activeSettings.WorkspacePath
                : DraGonPaths.DefaultWorkspaceDirectory;
            targetFolder = Path.Combine(baseDir, FormProjectName.Trim());
            FormProjectFolderPath = targetFolder;
        }

        var mustExist = string.Equals(FormProjectType, "Existing", StringComparison.OrdinalIgnoreCase) || IsEditingExistingProject;
        var pathValidation = ValidationService.ValidateDirectoryPath(targetFolder, "Folder proyek", mustExist: mustExist);
        if (!pathValidation.IsValid)
        {
            ShowNotification("Validasi Folder Proyek", pathValidation.ErrorMessage, NotificationTone.Error);
            return;
        }

        var activeSuffix = ValidationService.NormalizeHostnameSuffix(_activeSettings.HostnameSuffix);
        var normalizedDomain = FormProjectDomain.Trim().ToLowerInvariant();
        if (!normalizedDomain.Contains('.') && !string.IsNullOrWhiteSpace(normalizedDomain))
        {
            normalizedDomain += activeSuffix;
            FormProjectDomain = normalizedDomain;
        }

        var conflictCheck = _hostsService.CheckDomainConflict(normalizedDomain, Projects, EditingProjectId, expectedSuffix: activeSuffix);
        if (!conflictCheck.IsValid)
        {
            ShowNotification("Konflik / Format Domain", conflictCheck.ErrorMessage, NotificationTone.Error);
            return;
        }

        // Jalankan Scaffolding untuk proyek baru
        if (!IsEditingExistingProject)
        {
            if (string.Equals(FormProjectType, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                _scaffoldingService.CreateStandardProject(targetFolder, FormProjectName.Trim(), normalizedDomain, out _);
            }
            else if (string.Equals(FormProjectType, "WordPress", StringComparison.OrdinalIgnoreCase))
            {
                IsBusy = true;
                BusyMessage = $"Mengunduh dan memasang WordPress untuk '{FormProjectName.Trim()}'...";
                try
                {
                    var (wpOk, wpMsg) = await _scaffoldingService.CreateWordPressProjectAsync(targetFolder, FormProjectName.Trim());
                    if (!wpOk)
                    {
                        ShowNotification("Gagal Memasang WordPress", wpMsg, NotificationTone.Error);
                        return;
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
            else if (string.Equals(FormProjectType, "Laravel", StringComparison.OrdinalIgnoreCase))
            {
                var workspaceDir = !string.IsNullOrWhiteSpace(_activeSettings.WorkspacePath)
                    ? _activeSettings.WorkspacePath
                    : DraGonPaths.DefaultWorkspaceDirectory;
                Directory.CreateDirectory(workspaceDir);

                var laravelCmd = $"composer create-project laravel/laravel \"{FormProjectName.Trim()}\"";
                _launcherService.OpenTerminalWithCommand(workspaceDir, laravelCmd, _activeSettings);
            }
            else if (string.Equals(FormProjectType, "GitClone", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(FormGitUrl))
                {
                    ShowNotification("URL Repositori Kosong", "Masukkan URL repositori Git untuk di-clone.", NotificationTone.Error);
                    return;
                }

                var workspaceDir = !string.IsNullOrWhiteSpace(_activeSettings.WorkspacePath)
                    ? _activeSettings.WorkspacePath
                    : DraGonPaths.DefaultWorkspaceDirectory;
                Directory.CreateDirectory(workspaceDir);

                var gitCmd = $"git clone \"{FormGitUrl.Trim()}\" \"{targetFolder}\"";
                _launcherService.OpenTerminalWithCommand(workspaceDir, gitCmd, _activeSettings);
            }
        }

        if (IsEditingExistingProject && EditingProjectId.HasValue)
        {
            var existing = Projects.FirstOrDefault(p => p.Id == EditingProjectId.Value);
            if (existing is not null)
            {
                existing.Name = FormProjectName.Trim();
                existing.FolderPath = Path.GetFullPath(targetFolder);
                existing.Domain = normalizedDomain;
                existing.HttpsEnabled = FormEnableHttps;
                ProjectDetectorService.AnalyzeProjectFolder(existing);
                _logger.Info("Projects", $"Proyek '{existing.Name}' ({existing.Domain}) diperbarui.");
            }
        }
        else
        {
            var newProject = new ProjectItem
            {
                Id = Guid.NewGuid(),
                Name = FormProjectName.Trim(),
                FolderPath = Path.GetFullPath(targetFolder),
                Domain = normalizedDomain,
                HttpsEnabled = FormEnableHttps,
                CreatedAt = DateTime.Now
            };

            if (string.Equals(FormProjectType, "Laravel", StringComparison.OrdinalIgnoreCase))
            {
                newProject.FrameworkType = ProjectFramework.Laravel;
                newProject.DocumentRoot = Path.Combine(newProject.FolderPath, "public");
            }
            else if (string.Equals(FormProjectType, "WordPress", StringComparison.OrdinalIgnoreCase))
            {
                newProject.FrameworkType = ProjectFramework.WordPress;
            }

            ProjectDetectorService.AnalyzeProjectFolder(newProject);
            Projects.Insert(0, newProject);
            _logger.Info("Projects", $"Proyek baru '{newProject.Name}' ({newProject.Domain}) ditambahkan dari '{newProject.FolderPath}'.");
        }

        _configService.SaveProjects(Projects);
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        IsProjectFormOpen = false;
        RebuildProjectCollections();

        if (FormAutoSyncHosts)
        {
            await SyncAllDomainsToHostsInternalAsync("Menyimpan proyek, menerbitkan sertifikat SSL .dra, dan menyinkronkan ke file hosts...");
        }
        else
        {
            _hostsService.RefreshProjectsDomainStatus(Projects);
            RebuildProjectCollections();
            ShowNotification(
                "Proyek & Sertifikat SSL Tersimpan",
                $"Proyek '{FormProjectName.Trim()}' ({normalizedDomain}) berhasil disimpan. Klik 'Sinkronkan Domain ke Hosts' ketika siap mendaftarkan ke 127.0.0.1.",
                NotificationTone.Success);
        }

        await ReloadActiveWebServersAsync();
    }

    [RelayCommand]
    private async Task RemoveProjectFromListAsync(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Hapus proyek '{project.Name}' ({project.Domain}) dari daftar DraGon?\n\n" +
            $"Catatan Keamanan:\n" +
            $"• Folder sumber proyek di '{project.FolderPath}' TIDAK akan dihapus.\n" +
            $"• Entri domain '{project.Domain}' yang dikelola DraGon akan dilepas dari daftar.",
            "Konfirmasi Hapus Proyek dari Daftar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await _projectDevProcessManager.StopProjectDevTaskAsync(project);

        var wasSynced = project.DomainStatus == DomainSyncStatus.Synced;
        var removedName = project.Name;
        var removedDomain = project.Domain;

        Projects.Remove(project);
        _configService.SaveProjects(Projects);
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        RebuildProjectCollections();

        _logger.Info("Projects", $"Proyek '{removedName}' ({removedDomain}) dihapus dari daftar (folder sumber tetap utuh).");

        if (wasSynced)
        {
            await SyncAllDomainsToHostsInternalAsync($"Melepas entri domain {removedDomain} dari file hosts...");
        }
        else
        {
            ShowNotification(
                "Proyek Dihapus dari Daftar",
                $"Proyek '{removedName}' dihapus dari daftar DraGon. Folder sumber di disk tetap aman dan tidak disentuh.",
                NotificationTone.Info);
        }
    }

    [RelayCommand]
    private async Task SyncAllDomainsToHostsAsync()
    {
        await SyncAllDomainsToHostsInternalAsync("Menyinkronkan domain lokal (.dra) ke file hosts Windows (127.0.0.1)...");
    }

    private List<string> GetManagedDomainsForSync()
    {
        var (_, externalDomains, _) = _hostsService.InspectHostsFile();
        var list = new List<string>();

        var suffix = ValidationService.NormalizeHostnameSuffix(_activeSettings.HostnameSuffix);
        var utilityDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost.dra",
            "phpmyadmin.dra",
            "phpinfo.dra",
            $"localhost{suffix}",
            $"phpmyadmin{suffix}",
            $"phpinfo{suffix}"
        };

        foreach (var domain in Projects.Select(p => p.Domain).Concat(utilityDomains))
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                continue;
            }

            var normalized = domain.Trim().ToLowerInvariant();
            if (!externalDomains.Contains(normalized) && !list.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(normalized);
            }
        }

        return list;
    }

    private async Task<HostsOperationResult> SyncDomainsCoreAsync()
    {
        var targetDomains = GetManagedDomainsForSync();
        var result = await _hostsService.SyncManagedDomainsAsync(targetDomains);
        _hostsService.RefreshProjectsDomainStatus(Projects);
        RebuildProjectCollections();
        return result;
    }

    private async Task SyncAllDomainsToHostsInternalAsync(string busyText)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            SetBusyState(true, busyText);
            var result = await SyncDomainsCoreAsync();

            if (result.Succeeded)
            {
                ShowNotification("Sinkronisasi Domain (.dra) Berhasil", result.Message, NotificationTone.Success);
            }
            else if (result.CancelledByUser)
            {
                ShowNotification("Izin Administrator Dibatalkan", result.Message, NotificationTone.Warning);
            }
            else
            {
                ShowNotification("Gagal Memperbarui File Hosts", result.Message, NotificationTone.Error);
            }
        }
        finally
        {
            SetBusyState(false);
        }
    }

    [RelayCommand]
    private async Task ScanWorkspaceForProjectsAsync()
    {
        if (!Directory.Exists(_activeSettings.WorkspacePath))
        {
            ShowNotification("Workspace Belum Ada", $"Folder workspace '{_activeSettings.WorkspacePath}' belum ditemukan.", NotificationTone.Warning);
            return;
        }

        var addedCount = 0;
        var dirs = Directory.GetDirectories(_activeSettings.WorkspacePath);
        foreach (var dir in dirs)
        {
            var fullDir = Path.GetFullPath(dir);
            if (Projects.Any(p => string.Equals(Path.GetFullPath(p.FolderPath), fullDir, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var folderName = Path.GetFileName(fullDir);
            if (string.Equals(folderName, "welcome", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var activeSuffix = ValidationService.NormalizeHostnameSuffix(_activeSettings.HostnameSuffix);
            var baseDomain = ValidationService.SuggestDomainFromFolderOrName(folderName, activeSuffix);
            var candidateDomain = baseDomain;
            var counter = 2;

            while (!_hostsService.CheckDomainConflict(candidateDomain, Projects, expectedSuffix: activeSuffix).IsValid && counter < 50)
            {
                var prefix = baseDomain.EndsWith(activeSuffix, StringComparison.OrdinalIgnoreCase)
                    ? baseDomain[..^activeSuffix.Length]
                    : baseDomain;
                candidateDomain = $"{prefix}-{counter}{activeSuffix}";
                counter++;
            }

            if (!_hostsService.CheckDomainConflict(candidateDomain, Projects, expectedSuffix: activeSuffix).IsValid)
            {
                continue;
            }

            var item = new ProjectItem
            {
                Id = Guid.NewGuid(),
                Name = folderName,
                FolderPath = fullDir,
                Domain = candidateDomain,
                HttpsEnabled = true,
                CreatedAt = DateTime.Now
            };
            ProjectDetectorService.AnalyzeProjectFolder(item);
            Projects.Add(item);
            addedCount++;
        }

        if (addedCount > 0)
        {
            _configService.SaveProjects(Projects);
            _hostsService.RefreshProjectsDomainStatus(Projects);
            _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
            RebuildProjectCollections();

            await SyncAllDomainsToHostsInternalAsync($"Menyinkronkan {addedCount} domain proyek baru (.dra) ke file hosts Windows...");
            await ReloadActiveWebServersAsync();

            ShowNotification(
                "Pemindaian Workspace & Sinkronisasi Selesai",
                $"Ditemukan dan ditambahkan {addedCount} proyek baru beserta sertifikat SSL (.dra) dan sinkronisasi file hosts.",
                NotificationTone.Success);
        }
        else
        {
            if (Projects.Any(p => p.DomainStatus != DomainSyncStatus.Synced))
            {
                await SyncAllDomainsToHostsInternalAsync("Menyinkronkan domain proyek (.dra) yang belum aktif di file hosts...");
            }
            else
            {
                ShowNotification(
                    "Tidak Ada Proyek Baru",
                    $"Seluruh subfolder di '{_activeSettings.WorkspacePath}' sudah terdaftar dan tersinkronisasi.",
                    NotificationTone.Info);
            }
        }
    }

    [RelayCommand]
    private async Task OpenProjectInBrowserAsync(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var neededHostsSync = project.DomainStatus != DomainSyncStatus.Synced;

        project.LastOpenedAt = DateTime.Now;
        _sslService.EnsureDomainCertificate(project.Domain);
        project.HasSslCertificate = true;
        _configService.SaveProjects(Projects);
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);

        // Pastikan web server utama (Apache/Nginx) dan PHP FastCGI berjalan agar tidak terjadi ERR_CONNECTION_REFUSED
        var primaryWebKind = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? ServiceKind.Apache
            : ServiceKind.Nginx;

        var webSvc = Services.FirstOrDefault(s => s.Kind == primaryWebKind);
        var phpSvc = Services.FirstOrDefault(s => s.Kind == ServiceKind.PhpCgi);

        if (phpSvc is { IsRunning: false })
        {
            await _serviceManager.StartServiceAsync(ServiceKind.PhpCgi, _activeSettings, Projects);
        }

        if (webSvc is { IsRunning: false })
        {
            await _serviceManager.StartServiceAsync(primaryWebKind, _activeSettings, Projects);
        }
        else if (neededHostsSync)
        {
            await ReloadActiveWebServersAsync();
        }

        _hostsService.RefreshProjectsDomainStatus(Projects);
        if (project.DomainStatus != DomainSyncStatus.Synced)
        {
            try
            {
                SetBusyState(true, $"Mendaftarkan domain '{project.Domain}' ke file hosts Windows (127.0.0.1)...");
                await SyncDomainsCoreAsync();
            }
            finally
            {
                SetBusyState(false);
            }
        }

        UpdateDashboardSummary();

        if (project.DomainStatus != DomainSyncStatus.Synced)
        {
            ShowNotification(
                "Perhatian: Domain Belum Sinkron di Hosts",
                $"Domain '{project.Domain}' belum terdaftar aktif di file hosts Windows. Setujui izin Administrator (UAC) atau klik tombol 'Sinkronkan Hosts (.dra)' terlebih dahulu.",
                NotificationTone.Warning);
        }

        var (ok, msg) = _launcherService.OpenProjectInBrowser(project, _activeSettings);
        if (!ok)
        {
            ShowNotification("Gagal Membuka Browser", msg, NotificationTone.Error);
        }
    }

    [RelayCommand]
    private async Task OpenPhpInfoInBrowserAsync()
    {
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        await EnsureWebAndPhpRunningAsync();

        var port = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? _activeSettings.ApachePort
            : _activeSettings.WebServerPort;
        var portPart = port == 80 ? string.Empty : $":{port}";
        var url = $"http://localhost{portPart}/phpinfo.php";

        var (ok, msg) = _launcherService.OpenUrl(url, _activeSettings);
        if (!ok)
        {
            ShowNotification("Gagal Membuka PHP Info", msg, NotificationTone.Error);
        }
    }

    [RelayCommand]
    private async Task OpenPhpMyAdminInBrowserAsync()
    {
        var pmaDir = ServerConfigGenerator.ResolvePhpMyAdminDirectory(_activeSettings.RuntimeBasePath);
        if (string.IsNullOrWhiteSpace(pmaDir))
        {
            var pmaPkg = RuntimePackages.FirstOrDefault(p => string.Equals(p.Id, "phpmyadmin", StringComparison.OrdinalIgnoreCase));
            if (pmaPkg is not null && !IsBusy)
            {
                try
                {
                    SetBusyState(true, "Mengunduh dan memasang phpMyAdmin 5.2.3...");
                    await DownloadAndInstallPackageAsync(pmaPkg);
                }
                finally
                {
                    SetBusyState(false);
                }
            }

            pmaDir = ServerConfigGenerator.ResolvePhpMyAdminDirectory(_activeSettings.RuntimeBasePath);
            if (string.IsNullOrWhiteSpace(pmaDir))
            {
                ShowNotification(
                    "phpMyAdmin Belum Terpasang",
                    "Silakan pasang paket 'phpMyAdmin' melalui menu Packages & Installer terlebih dahulu.",
                    NotificationTone.Warning);
                return;
            }
        }

        ServerConfigGenerator.EnsurePhpMyAdminConfigured(pmaDir, _activeSettings.DatabasePort);
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);

        var dbSvc = Services.FirstOrDefault(s => s.Kind == ServiceKind.MariaDb);
        if (dbSvc is { IsRunning: false, IsMissingRuntime: false })
        {
            await _serviceManager.StartServiceAsync(ServiceKind.MariaDb, _activeSettings, Projects);
        }

        await EnsureWebAndPhpRunningAsync();

        var port = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? _activeSettings.ApachePort
            : _activeSettings.WebServerPort;
        var portPart = port == 80 ? string.Empty : $":{port}";
        var url = $"http://localhost{portPart}/phpmyadmin/";

        var (ok, msg) = _launcherService.OpenUrl(url, _activeSettings);
        if (!ok)
        {
            ShowNotification("Gagal Membuka phpMyAdmin", msg, NotificationTone.Error);
        }
    }

    [RelayCommand]
    private async Task OpenHeidiSqlAsync()
    {
        var (ok, msg) = _launcherService.LaunchHeidiSql(_activeSettings.RuntimeBasePath);
        if (ok)
        {
            return;
        }

        var heidiPkg = RuntimePackages.FirstOrDefault(p => string.Equals(p.Id, "heidisql", StringComparison.OrdinalIgnoreCase));
        if (heidiPkg is not null && !heidiPkg.IsAvailable && !IsBusy)
        {
            try
            {
                SetBusyState(true, "Mengunduh dan memasang HeidiSQL Portable...");
                await DownloadAndInstallPackageAsync(heidiPkg);
                var (retryOk, retryMsg) = _launcherService.LaunchHeidiSql(_activeSettings.RuntimeBasePath);
                if (retryOk)
                {
                    return;
                }
            }
            finally
            {
                SetBusyState(false);
            }
        }

        ShowNotification(
            "HeidiSQL Belum Terpasang",
            msg,
            NotificationTone.Warning);
    }

    [RelayCommand]
    private async Task OpenLocalhostInBrowserAsync()
    {
        _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
        await EnsureWebAndPhpRunningAsync();

        var port = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? _activeSettings.ApachePort
            : _activeSettings.WebServerPort;
        var portPart = port == 80 ? string.Empty : $":{port}";
        var url = $"http://localhost{portPart}/";

        var (ok, msg) = _launcherService.OpenUrl(url, _activeSettings);
        if (!ok)
        {
            ShowNotification("Gagal Membuka Localhost", msg, NotificationTone.Error);
        }
    }

    private async Task EnsureWebAndPhpRunningAsync()
    {
        var primaryWebKind = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? ServiceKind.Apache
            : ServiceKind.Nginx;

        var phpSvc = Services.FirstOrDefault(s => s.Kind == ServiceKind.PhpCgi);
        if (phpSvc is { IsRunning: false, IsMissingRuntime: false })
        {
            await _serviceManager.StartServiceAsync(ServiceKind.PhpCgi, _activeSettings, Projects);
        }

        var webSvc = Services.FirstOrDefault(s => s.Kind == primaryWebKind);
        if (webSvc is { IsRunning: false, IsMissingRuntime: false })
        {
            await _serviceManager.StartServiceAsync(primaryWebKind, _activeSettings, Projects);
        }

        UpdateDashboardSummary();
    }

    [RelayCommand]
    private void OpenProjectInTerminal(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = _launcherService.OpenTerminalInFolder(project.FolderPath, _activeSettings);
        ShowNotification(
            ok ? "Terminal Proyek Dibuka" : "Gagal Membuka Terminal",
            msg,
            ok ? NotificationTone.Info : NotificationTone.Error);
    }

    [RelayCommand]
    private void OpenProjectFolder(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = _launcherService.OpenFolder(project.FolderPath);
        if (!ok)
        {
            ShowNotification("Gagal Membuka Folder Proyek", msg, NotificationTone.Error);
        }
    }

    // Managed Background Project Dev Processes (Node.js / Laravel)
    [RelayCommand]
    private async Task StartProjectNpmDevBgAsync(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = await _projectDevProcessManager.StartProjectDevTaskAsync(
            project,
            ManagedProjectTaskKind.NpmRunDev,
            _activeSettings);

        UpdateDashboardSummary();
        ShowNotification(
            ok ? "Node.js Dev Server Berjalan" : "Gagal Memulai npm run dev",
            msg,
            ok ? NotificationTone.Success : NotificationTone.Warning);
    }

    [RelayCommand]
    private async Task StartProjectArtisanServeBgAsync(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = await _projectDevProcessManager.StartProjectDevTaskAsync(
            project,
            ManagedProjectTaskKind.ArtisanServe,
            _activeSettings);

        UpdateDashboardSummary();
        ShowNotification(
            ok ? "Laravel Artisan Serve Berjalan" : "Gagal Memulai php artisan serve",
            msg,
            ok ? NotificationTone.Success : NotificationTone.Warning);
    }

    [RelayCommand]
    private async Task StopProjectDevTaskAsync(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = await _projectDevProcessManager.StopProjectDevTaskAsync(project);
        UpdateDashboardSummary();
        ShowNotification("Proses Dev Proyek Dihentikan", msg, ok ? NotificationTone.Info : NotificationTone.Warning);
    }

    // Interactive Terminal Shortcuts
    [RelayCommand]
    private void RunArtisanServe(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = _launcherService.RunProjectQuickCommandInTerminal(
            project,
            ProjectQuickCommand.ArtisanServe,
            _activeSettings);

        ShowNotification(
            ok ? "Menjalankan php artisan serve" : "Perintah Tidak Dapat Dijalankan",
            msg,
            ok ? NotificationTone.Success : NotificationTone.Warning);
    }

    [RelayCommand]
    private void RunComposerInstall(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = _launcherService.RunProjectQuickCommandInTerminal(
            project,
            ProjectQuickCommand.ComposerInstall,
            _activeSettings);

        ShowNotification(
            ok ? "Menjalankan composer install" : "Perintah Tidak Dapat Dijalankan",
            msg,
            ok ? NotificationTone.Success : NotificationTone.Warning);
    }

    [RelayCommand]
    private void RunNpmInstall(ProjectItem? project)
    {
        if (project is null)
        {
            return;
        }

        var (ok, msg) = _launcherService.RunProjectQuickCommandInTerminal(
            project,
            ProjectQuickCommand.NpmInstall,
            _activeSettings);

        ShowNotification(
            ok ? "Menjalankan npm install" : "Perintah Tidak Dapat Dijalankan",
            msg,
            ok ? NotificationTone.Success : NotificationTone.Warning);
    }

    // ==================== PACKAGES & RUNTIME INSTALLER COMMANDS ====================

    [RelayCommand]
    private async Task DownloadAndInstallPackageAsync(RuntimePackageInfo? package)
    {
        if (package is null || package.IsInstalling)
        {
            return;
        }

        try
        {
            package.IsInstalling = true;
            package.InstallProgressPercent = 1;
            package.InstallStatusMessage = "Memulai pengunduhan resmi...";

            var progress = new Progress<(double Percent, string Status)>(report =>
            {
                package.InstallProgressPercent = report.Percent;
                package.InstallStatusMessage = report.Status;
            });

            var (ok, msg) = await _runtimeInstallerService.DownloadAndInstallOfficialArchiveAsync(
                package,
                _activeSettings.RuntimeBasePath,
                progress);

            RefreshAllEnvironmentStatus();
            package.InstallStatusMessage = msg;

            ShowNotification(
                ok ? $"{package.Name} Terpasang" : $"Pemasangan {package.Name} Belum Selesai",
                msg,
                ok ? NotificationTone.Success : NotificationTone.Warning);
        }
        finally
        {
            package.IsInstalling = false;
        }
    }

    [RelayCommand]
    private async Task ImportPackageZipAsync(RuntimePackageInfo? package)
    {
        if (package is null || package.IsInstalling)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"Pilih Arsip ZIP Resmi untuk {package.Name}",
            Filter = "Arsip ZIP Resmi (*.zip)|*.zip"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            package.IsInstalling = true;
            package.InstallProgressPercent = 25;
            package.InstallStatusMessage = "Mengekstrak arsip ZIP...";

            var progress = new Progress<(double Percent, string Status)>(report =>
            {
                package.InstallProgressPercent = report.Percent;
                package.InstallStatusMessage = report.Status;
            });

            var (ok, msg) = await _runtimeInstallerService.ImportOfficialZipArchiveAsync(
                package,
                dialog.FileName,
                _activeSettings.RuntimeBasePath,
                progress);

            RefreshAllEnvironmentStatus();
            package.InstallStatusMessage = msg;

            ShowNotification(
                ok ? $"{package.Name} Berhasil Diimpor" : $"Gagal Mengimpor {package.Name}",
                msg,
                ok ? NotificationTone.Success : NotificationTone.Error);
        }
        finally
        {
            package.IsInstalling = false;
        }
    }

    [RelayCommand]
    private void OpenPackageDownloadPage(RuntimePackageInfo? package)
    {
        if (package is null)
        {
            return;
        }

        _launcherService.OpenUrl(package.OfficialDownloadUrl, _activeSettings);
    }

    [RelayCommand]
    private void OpenPackageTargetFolder(RuntimePackageInfo? package)
    {
        if (package is null)
        {
            return;
        }

        var folder = string.IsNullOrWhiteSpace(package.TargetRuntimeFolder)
            ? Path.Combine(_activeSettings.RuntimeBasePath, package.ExpectedRelativeDir)
            : package.TargetRuntimeFolder;

        Directory.CreateDirectory(folder);
        _launcherService.OpenFolder(folder);
    }

    [RelayCommand]
    private void RescanRuntimes()
    {
        RefreshAllEnvironmentStatus();
        var installedCount = RuntimePackages.Count(p => p.IsAvailable);
        ShowNotification(
            "Pemindaian Runtime & Multi-PHP Selesai",
            $"Terdeteksi {installedCount} dari {RuntimePackages.Count} paket runtime dan {PhpVersions.Count} versi PHP.",
            NotificationTone.Info);
    }

    // ==================== SETTINGS & DATA FOLDER COMMANDS ====================

    [RelayCommand]
    private void BrowseApplicationFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Aplikasi DraGon (Custom)",
            InitialDirectory = Directory.Exists(SettingsApplicationFolderPath)
                ? SettingsApplicationFolderPath
                : @"C:\"
        };

        if (dialog.ShowDialog() == true)
        {
            SettingsApplicationFolderPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void OpenApplicationFolder()
    {
        var folder = string.IsNullOrWhiteSpace(SettingsApplicationFolderPath)
            ? _activeSettings.ApplicationFolderPath
            : SettingsApplicationFolderPath.Trim();

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            _launcherService.OpenFolder(folder);
        }
    }

    [RelayCommand]
    private void BrowseWorkspaceFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Workspace Proyek DraGon",
            InitialDirectory = Directory.Exists(SettingsWorkspacePath)
                ? SettingsWorkspacePath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            SettingsWorkspacePath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseRuntimeFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Induk Runtime DraGon",
            InitialDirectory = Directory.Exists(SettingsRuntimeBasePath)
                ? SettingsRuntimeBasePath
                : DraGonPaths.AppDataRoot
        };

        if (dialog.ShowDialog() == true)
        {
            SettingsRuntimeBasePath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseCustomBrowserExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih File Executable Browser (.exe)",
            Filter = "Program Executable (*.exe)|*.exe"
        };

        if (dialog.ShowDialog() == true)
        {
            SettingsCustomBrowserPath = dialog.FileName;
            SettingsPreferredBrowser = "Custom";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var appFolderCheck = ValidationService.ValidateDirectoryPath(SettingsApplicationFolderPath, "Folder Aplikasi Custom", mustExist: false);
        if (!appFolderCheck.IsValid)
        {
            ShowNotification("Validasi Folder Aplikasi Gagal", appFolderCheck.ErrorMessage, NotificationTone.Error);
            return;
        }

        var wsCheck = ValidationService.ValidateDirectoryPath(SettingsWorkspacePath, "Folder Workspace", mustExist: false);
        if (!wsCheck.IsValid)
        {
            ShowNotification("Validasi Workspace Gagal", wsCheck.ErrorMessage, NotificationTone.Error);
            return;
        }

        var rtCheck = ValidationService.ValidateDirectoryPath(SettingsRuntimeBasePath, "Lokasi Runtime", mustExist: false);
        if (!rtCheck.IsValid)
        {
            ShowNotification("Validasi Lokasi Runtime Gagal", rtCheck.ErrorMessage, NotificationTone.Error);
            return;
        }

        var portChecks = new[]
        {
            ValidationService.ValidatePort(SettingsWebServerPort, "Nginx Web Server (HTTP)"),
            ValidationService.ValidatePort(SettingsWebServerHttpsPort, "Nginx Web Server (HTTPS)"),
            ValidationService.ValidatePort(SettingsApachePort, "Apache HTTP Server (HTTP)"),
            ValidationService.ValidatePort(SettingsApacheHttpsPort, "Apache HTTP Server (HTTPS)"),
            ValidationService.ValidatePort(SettingsDatabasePort, "Database MariaDB/MySQL"),
            ValidationService.ValidatePort(SettingsPhpCgiPort, "PHP FastCGI")
        };

        foreach (var check in portChecks)
        {
            if (!check.IsValid)
            {
                ShowNotification("Validasi Port Gagal", check.ErrorMessage, NotificationTone.Error);
                return;
            }
        }

        var distinctPorts = new HashSet<int>
        {
            SettingsWebServerPort,
            SettingsWebServerHttpsPort,
            SettingsApachePort,
            SettingsApacheHttpsPort,
            SettingsDatabasePort,
            SettingsPhpCgiPort
        };

        if (distinctPorts.Count < 6)
        {
            ShowNotification(
                "Konflik Port Internal",
                "Seluruh port HTTP, HTTPS, Database, dan PHP FastCGI harus berbeda satu sama lain.",
                NotificationTone.Error);
            return;
        }

        var suffixCheck = ValidationService.ValidateHostnameSuffix(SettingsHostnameSuffix);
        if (!suffixCheck.IsValid)
        {
            ShowNotification("Validasi Akhiran Hostname Gagal", suffixCheck.ErrorMessage, NotificationTone.Error);
            return;
        }

        var normalizedSuffix = ValidationService.NormalizeHostnameSuffix(SettingsHostnameSuffix);
        var suffixChanged = !string.Equals(_activeSettings.HostnameSuffix, normalizedSuffix, StringComparison.OrdinalIgnoreCase);

        _activeSettings.ApplicationFolderPath = Path.GetFullPath(SettingsApplicationFolderPath.Trim());
        _activeSettings.WorkspacePath = Path.GetFullPath(SettingsWorkspacePath.Trim());
        _activeSettings.RuntimeBasePath = Path.GetFullPath(SettingsRuntimeBasePath.Trim());
        _activeSettings.PreferredWebServer = SettingsPreferredWebServer;
        _activeSettings.WebServerPort = SettingsWebServerPort;
        _activeSettings.WebServerHttpsPort = SettingsWebServerHttpsPort;
        _activeSettings.ApachePort = SettingsApachePort;
        _activeSettings.ApacheHttpsPort = SettingsApacheHttpsPort;
        _activeSettings.DatabasePort = SettingsDatabasePort;
        _activeSettings.PhpCgiPort = SettingsPhpCgiPort;
        _activeSettings.EnableLocalHttps = SettingsEnableLocalHttps;
        _activeSettings.PreferredBrowser = SettingsPreferredBrowser;
        _activeSettings.CustomBrowserPath = SettingsCustomBrowserPath.Trim();
        _activeSettings.RunOnWindowsStartup = SettingsRunOnStartup;
        _activeSettings.Theme = SettingsTheme;
        _activeSettings.HostnameSuffix = normalizedSuffix;
        _activeSettings.AutoCheckUpdatesOnStartup = SettingsAutoCheckUpdates;
        _activeSettings.UpdateFeedUrl = SettingsUpdateFeedUrl.Trim();
        ActiveHostnameSuffix = normalizedSuffix;
        HostnameSuffixBadgeText = $"HTTP & HTTPS Lokal: *{normalizedSuffix}";
        SettingsHostnameSuffix = normalizedSuffix;

        _configService.SaveSettings(_activeSettings);
        ThemeManager.ApplyTheme(_activeSettings.Theme);

        if (suffixChanged)
        {
            _vhostGenerator.GenerateConfigs(_activeSettings, Projects);
            await ReloadActiveWebServersAsync();
            _ = SyncAllDomainsToHostsInternalAsync($"Menyinkronkan domain utilitas baru ({normalizedSuffix}) ke file hosts...");
        }

        if (OperatingSystem.IsWindows())
        {
            var exePath = Environment.ProcessPath;
            _configService.ApplyWindowsStartupSetting(_activeSettings.RunOnWindowsStartup, exePath);
        }

        RefreshAllEnvironmentStatus();
        ShowNotification(
            "Pengaturan Disimpan",
            "Seluruh perubahan konfigurasi, port HTTP/HTTPS, akhiran hostname, tema, dan lokasi folder berhasil diterapkan.",
            NotificationTone.Success);
    }

    [RelayCommand]
    private void ResetSettingsToDefault()
    {
        var defaults = ConfigurationService.CreateDefaultSettings();
        CopySettingsToForm(defaults);
        ShowNotification(
            "Pengaturan Dikembalikan ke Bawaan",
            "Nilai formulir telah diatur ke bawaan. Klik 'Simpan Pengaturan' untuk menerapkan.",
            NotificationTone.Info);
    }

    [RelayCommand]
    private void OpenGlobalTerminal()
    {
        var targetFolder = !string.IsNullOrWhiteSpace(_activeSettings.WorkspacePath)
            ? _activeSettings.WorkspacePath
            : DraGonPaths.DefaultWorkspaceDirectory;

        try
        {
            Directory.CreateDirectory(targetFolder);
        }
        catch { }

        var (sOk, sMsg) = _launcherService.OpenTerminalInFolder(targetFolder, _activeSettings);
        ShowNotification(
            sOk ? "Terminal DraGon Dibuka" : "Gagal Membuka Terminal",
            sMsg,
            sOk ? NotificationTone.Info : NotificationTone.Error);
    }

    [RelayCommand]
    private void OpenDraGonDataFolder()
    {
        DraGonPaths.EnsureDirectoriesExist(_activeSettings.RuntimeBasePath, _activeSettings.WorkspacePath);
        _launcherService.OpenFolder(DraGonPaths.AppDataRoot);
    }

    [RelayCommand]
    private void OpenWorkspaceFolder()
    {
        Directory.CreateDirectory(_activeSettings.WorkspacePath);
        _launcherService.OpenFolder(_activeSettings.WorkspacePath);
    }

    [RelayCommand]
    private void OpenRuntimeBaseFolder()
    {
        Directory.CreateDirectory(_activeSettings.RuntimeBasePath);
        _launcherService.OpenFolder(_activeSettings.RuntimeBasePath);
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(DraGonPaths.LogsDirectory);
        _launcherService.OpenFolder(DraGonPaths.LogsDirectory);
    }

    [RelayCommand]
    private void OpenMainLogFile()
    {
        if (!File.Exists(DraGonPaths.MainLogFilePath))
        {
            ShowNotification("File Log Belum Ada", "File log utama belum ditemukan.", NotificationTone.Info);
            return;
        }

        _launcherService.OpenFileInDefaultViewer(DraGonPaths.MainLogFilePath);
    }

    [RelayCommand]
    private void OpenBackupsFolder()
    {
        Directory.CreateDirectory(DraGonPaths.BackupsDirectory);
        _launcherService.OpenFolder(DraGonPaths.BackupsDirectory);
    }

    [RelayCommand]
    private void ClearLogView()
    {
        ApplicationLogs.Clear();
    }

    public async Task StopAllServicesBeforeExitAsync()
    {
        await _serviceManager.StopAllAsync();
        await _projectDevProcessManager.StopAllProjectDevTasksAsync();
    }

    private void LoadProjectsFromDisk()
    {
        Projects.Clear();
        var loaded = _configService.LoadProjects();
        foreach (var item in loaded)
        {
            ProjectDetectorService.AnalyzeProjectFolder(item);
            Projects.Add(item);
        }

        _hostsService.RefreshProjectsDomainStatus(Projects);
        RebuildProjectCollections();
    }

    private void RebuildProjectCollections()
    {
        FilteredProjects.Clear();
        var query = ProjectSearchText?.Trim() ?? string.Empty;

        foreach (var proj in Projects)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                proj.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                proj.Domain.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                proj.FolderPath.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredProjects.Add(proj);
            }
        }

        RecentProjects.Clear();
        foreach (var proj in Projects
                     .OrderByDescending(p => p.LastOpenedAt ?? p.CreatedAt)
                     .Take(5))
        {
            RecentProjects.Add(proj);
        }

        OnPropertyChanged(nameof(HasAnyProjects));
        OnPropertyChanged(nameof(HasFilteredProjects));
    }

    private void UpdateDashboardSummary()
    {
        var running = Services.Count(s => s.IsRunning);
        var missing = Services.Count(s => s.IsMissingRuntime);
        RunningServicesCount = running;

        var webPort = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? _activeSettings.ApachePort
            : _activeSettings.WebServerPort;
        var httpsPort = _activeSettings.PreferredWebServer == WebServerType.Apache
            ? _activeSettings.ApacheHttpsPort
            : _activeSettings.WebServerHttpsPort;
        var webName = _activeSettings.PreferredWebServer == WebServerType.Apache ? "Apache" : "Nginx";

        ActivePortsSummaryText = $"{webName}: {webPort} (SSL:{httpsPort})  •  PHP-CGI: {_activeSettings.PhpCgiPort}  •  DB: {_activeSettings.DatabasePort}";

        if (running > 0)
        {
            EnvironmentSummaryText = $"{running} layanan lokal sedang berjalan aktif";
        }
        else if (missing == Services.Count)
        {
            EnvironmentSummaryText = "Semua layanan berhenti — Runtime dapat dipasang melalui menu Packages";
        }
        else
        {
            EnvironmentSummaryText = "Semua layanan lokal sedang berhenti — Siap dijalankan";
        }

        OnPropertyChanged(nameof(HasRunningServices));
        OnPropertyChanged(nameof(CanStartAllServices));
        OnPropertyChanged(nameof(CanStopAllServices));
        OnPropertyChanged(nameof(CanReloadServices));
        StartAllServicesCommand.NotifyCanExecuteChanged();
        StopAllServicesCommand.NotifyCanExecuteChanged();
        ReloadServicesCommand.NotifyCanExecuteChanged();

        UpdateInstalledServices();
    }

    private void UpdateInstalledServices()
    {
        var installed = Services.Where(s => !s.IsMissingRuntime).ToList();

        for (var i = InstalledServices.Count - 1; i >= 0; i--)
        {
            if (!installed.Contains(InstalledServices[i]))
            {
                InstalledServices.RemoveAt(i);
            }
        }

        for (var i = 0; i < installed.Count; i++)
        {
            var item = installed[i];
            var currentIndex = InstalledServices.IndexOf(item);
            if (currentIndex == -1)
            {
                InstalledServices.Insert(Math.Min(i, InstalledServices.Count), item);
            }
            else if (currentIndex != i)
            {
                InstalledServices.Move(currentIndex, i);
            }
        }
    }

    private void CopySettingsToForm(AppSettings source)
    {
        SettingsApplicationFolderPath = source.ApplicationFolderPath;
        SettingsWorkspacePath = source.WorkspacePath;
        SettingsRuntimeBasePath = source.RuntimeBasePath;
        SettingsPreferredWebServer = source.PreferredWebServer;
        SettingsWebServerPort = source.WebServerPort;
        SettingsWebServerHttpsPort = source.WebServerHttpsPort;
        SettingsApachePort = source.ApachePort;
        SettingsApacheHttpsPort = source.ApacheHttpsPort;
        SettingsDatabasePort = source.DatabasePort;
        SettingsPhpCgiPort = source.PhpCgiPort;
        SettingsEnableLocalHttps = source.EnableLocalHttps;
        SettingsPreferredBrowser = source.PreferredBrowser;
        SettingsCustomBrowserPath = source.CustomBrowserPath;
        SettingsRunOnStartup = source.RunOnWindowsStartup;
        SettingsTheme = source.Theme;
        SettingsHostnameSuffix = source.HostnameSuffix;
        SettingsAutoCheckUpdates = source.AutoCheckUpdatesOnStartup;
        SettingsUpdateFeedUrl = source.UpdateFeedUrl;
    }

    private void SetBusyState(bool busy, string message = "")
    {
        IsBusy = busy;
        BusyMessage = message;
        OnPropertyChanged(nameof(CanStartAllServices));
        OnPropertyChanged(nameof(CanStopAllServices));
        OnPropertyChanged(nameof(CanReloadServices));
        StartAllServicesCommand.NotifyCanExecuteChanged();
        StopAllServicesCommand.NotifyCanExecuteChanged();
        ReloadServicesCommand.NotifyCanExecuteChanged();
    }

    private void ShowNotification(string title, string message, NotificationTone tone)
    {
        NotificationTitle = title;
        NotificationMessage = message;
        NotificationTone = tone;
        HasNotification = true;
    }

    private void AppendAppLogLine(string formattedLine)
    {
        ApplicationLogs.Add(formattedLine);
        while (ApplicationLogs.Count > 250)
        {
            ApplicationLogs.RemoveAt(0);
        }
    }

    private async Task AutoCheckUpdateOnLaunchAsync()
    {
        try
        {
            if (!_activeSettings.AutoCheckUpdatesOnStartup)
            {
                return;
            }

            await Task.Delay(2500);
            await CheckForUpdateInternalAsync(isManual: false);
        }
        catch
        {
            // Abaikan kesalahan silent check di awal aplikasi
        }
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        await CheckForUpdateInternalAsync(isManual: true);
    }

    private async Task CheckForUpdateInternalAsync(bool isManual)
    {
        if (IsCheckingForUpdates || IsDownloadingUpdate)
        {
            return;
        }

        IsCheckingForUpdates = true;
        UpdateStatusSummary = "Sedang memeriksa ketersediaan pembaruan...";

        try
        {
            var feed = string.IsNullOrWhiteSpace(SettingsUpdateFeedUrl) ? null : SettingsUpdateFeedUrl;
            var result = await _updateService.CheckForUpdateAsync(feed, CurrentAppVersion);

            if (result.HasUpdate && result.Manifest is not null)
            {
                AvailableUpdateManifest = result.Manifest;
                IsUpdateAvailable = true;
                IsPatchUpdateAvailable = result.IsPatchAvailable;

                UpdateChangedFiles.Clear();
                foreach (var file in result.Manifest.ChangedFiles)
                {
                    UpdateChangedFiles.Add(file);
                }

                var badge = result.IsPatchAvailable
                    ? $"✨ Pembaruan Kecil / Patch ({result.Manifest.FormattedPatchSize})"
                    : $"📦 Pembaruan Penuh / Full ({result.Manifest.FormattedFullSize})";

                UpdateStatusSummary = $"Tersedia versi baru v{result.Manifest.Version}! ({badge})";

                ShowNotification(
                    "Pembaruan Aplikasi Tersedia",
                    $"Versi baru {result.Manifest.Version} siap dipasang ({badge}). Buka tab Pengaturan untuk melihat rincian dan memasang pembaruan.",
                    NotificationTone.Info);
            }
            else
            {
                IsUpdateAvailable = false;
                AvailableUpdateManifest = null;
                UpdateChangedFiles.Clear();
                UpdateStatusSummary = $"DraGon sudah menggunakan versi terbaru (v{CurrentAppVersion}).";

                if (isManual)
                {
                    ShowNotification(
                        "Versi Terbaru",
                        $"Aplikasi DraGon Anda (v{CurrentAppVersion}) sudah merupakan versi terbaru. Tidak ada pembaruan saat ini.",
                        NotificationTone.Success);
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatusSummary = $"Gagal memeriksa pembaruan: {ex.Message}";
            if (isManual)
            {
                ShowNotification("Gagal Memeriksa Pembaruan", ex.Message, NotificationTone.Error);
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndApplyPatchUpdateAsync()
    {
        if (AvailableUpdateManifest is null || IsDownloadingUpdate)
        {
            return;
        }

        var manifest = AvailableUpdateManifest;
        IsDownloadingUpdate = true;
        UpdateDownloadPercent = 0;
        UpdateDownloadStatusText = "Mempersiapkan unduhan...";

        try
        {
            var progress = new Progress<(double Percent, string Status)>(report =>
            {
                UpdateDownloadPercent = report.Percent;
                UpdateDownloadStatusText = report.Status;
            });

            var (success, message, zipPath) = await _updateService.DownloadPatchAsync(manifest, progress);

            if (!success || string.IsNullOrWhiteSpace(zipPath))
            {
                ShowNotification("Gagal Mengunduh Pembaruan", message, NotificationTone.Error);
                IsDownloadingUpdate = false;
                return;
            }

            UpdateDownloadStatusText = "Menerapkan pembaruan aplikasi...";

            var targetAppDir = DraGonPaths.ResolveEffectiveApplicationDirectory(_activeSettings.ApplicationFolderPath);
            var currentPid = Environment.ProcessId;

            // Jika aplikasi berjalan di mode development (Visual Studio / dotnet run), ekstrak langsung
            var isDevEnvironment = AppContext.BaseDirectory.Contains(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) ||
                                   AppContext.BaseDirectory.Contains(@"\bin\Release", StringComparison.OrdinalIgnoreCase);

            if (isDevEnvironment)
            {
                var count = _updateService.ExtractPatchDirectly(zipPath, targetAppDir);
                CurrentAppVersion = manifest.Version;
                IsUpdateAvailable = false;
                UpdateStatusSummary = $"Pembaruan v{manifest.Version} berhasil diterapkan!";
                ShowNotification(
                    "Pembaruan Berhasil Dipasang",
                    $"Patch v{manifest.Version} berhasil diterapkan ({count} berkas diperbarui) ke folder aplikasi: {targetAppDir}.",
                    NotificationTone.Success);
            }
            else
            {
                var launchRes = _updateService.PrepareAndLaunchPatchUpdater(zipPath, targetAppDir, currentPid, restartApp: true);
                if (launchRes.Success)
                {
                    ShowNotification(
                        "Memasang Pembaruan",
                        "Aplikasi DraGon akan segera ditutup untuk memperbarui berkas dan akan terbuka kembali secara otomatis...",
                        NotificationTone.Info);

                    await Task.Delay(1200);
                    Application.Current?.Shutdown();
                }
                else
                {
                    ShowNotification("Gagal Memasang Pembaruan", launchRes.Message, NotificationTone.Error);
                }
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Gagal Memasang Pembaruan", ex.Message, NotificationTone.Error);
        }
        finally
        {
            IsDownloadingUpdate = false;
        }
    }

    [RelayCommand]
    private void DismissUpdateNotice()
    {
        IsUpdateAvailable = false;
    }

    [RelayCommand]
    private async Task SimulateSamplePatchUpdateAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusSummary = "Mensimulasikan pemeriksaan update kecil (patch)...";
        await Task.Delay(400);

        var samplePatchZip = Path.Combine(DraGonPaths.UpdatesDirectory, "DraGon-Patch-v1.0.1-Demo.zip");
        AppUpdateService.CreateSamplePatchPackage(samplePatchZip, new Dictionary<string, string>
        {
            ["patch-notes.txt"] = "DraGon v1.0.1 Patch Applied Successfully!\nUpdated at: " + DateTime.Now,
            ["version.txt"] = "1.0.1"
        });

        AvailableUpdateManifest = new AppUpdateManifest
        {
            Version = "1.0.1",
            MinimumSupportedVersion = "1.0.0",
            ReleaseDate = DateTime.Now,
            Title = "DraGon v1.0.1 (Patch Kecil / Delta Update)",
            Changelog = "• Perbaikan performa query database lokal & port detection\n• Optimalisasi pemindaian proyek Laravel & Node.js dev runner\n• Pembaharuan sertifikat SSL internal DraGon\n• Ukuran unduhan hanya 350 KB (Hanya berkas yang berubah)",
            UpdateType = UpdatePackageType.Patch,
            PatchPackageUrl = samplePatchZip,
            PatchSizeBytes = 358400, // 350 KB
            FullPackageUrl = "https://github.com/example/dragon/releases/download/v1.0.1/DraGon-Setup.exe",
            FullSizeBytes = 89128960, // 85 MB
            ChangedFiles =
            [
                "DraGon.Core.dll (~150 KB)",
                "DraGon.App.dll (~180 KB)",
                "config/templates/nginx.conf (~20 KB)"
            ]
        };

        UpdateChangedFiles.Clear();
        foreach (var f in AvailableUpdateManifest.ChangedFiles)
        {
            UpdateChangedFiles.Add(f);
        }

        IsUpdateAvailable = true;
        IsPatchUpdateAvailable = true;
        IsCheckingForUpdates = false;
        UpdateStatusSummary = "Simulasi: Pembaruan kecil (Patch 350 KB) siap dipasang!";

        ShowNotification(
            "Pembaruan Patch Tersedia (Simulasi)",
            "Ditemukan patch pembaruan kecil v1.0.1 (hanya ~350 KB, bukan seluruh aplikasi). Anda dapat mencoba menekan tombol '⚡ Pasang Update Kecil (Patch)'!",
            NotificationTone.Info);
    }

    public void Dispose()
    {
        _projectDevProcessManager.Dispose();
        _serviceManager.Dispose();
    }
}
