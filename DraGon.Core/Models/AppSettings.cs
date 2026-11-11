namespace DraGon.Core.Models;

public enum WebServerType
{
    Apache,
    Nginx
}

public enum AppTheme
{
    Dark,
    Light
}

public sealed class AppSettings
{
    public string ApplicationFolderPath { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public string RuntimeBasePath { get; set; } = string.Empty;
    public WebServerType PreferredWebServer { get; set; } = WebServerType.Apache;
    public int WebServerPort { get; set; } = 8080;
    public int WebServerHttpsPort { get; set; } = 8443;
    public int ApachePort { get; set; } = 80;
    public int ApacheHttpsPort { get; set; } = 443;
    public int DatabasePort { get; set; } = 3306;
    public int PhpCgiPort { get; set; } = 9000;
    public bool EnableLocalHttps { get; set; } = true;
    public string SelectedPhpVersionId { get; set; } = string.Empty;
    public string SelectedDatabaseVersionId { get; set; } = string.Empty;
    public string PreferredBrowser { get; set; } = "SystemDefault";
    public string CustomBrowserPath { get; set; } = string.Empty;
    public bool RunOnWindowsStartup { get; set; } = false;
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string HostnameSuffix { get; set; } = ".dra";
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
    public string UpdateFeedUrl { get; set; } = string.Empty;

    public AppSettings Clone() => new()
    {
        ApplicationFolderPath = ApplicationFolderPath,
        WorkspacePath = WorkspacePath,
        RuntimeBasePath = RuntimeBasePath,
        PreferredWebServer = PreferredWebServer,
        WebServerPort = WebServerPort,
        WebServerHttpsPort = WebServerHttpsPort,
        ApachePort = ApachePort,
        ApacheHttpsPort = ApacheHttpsPort,
        DatabasePort = DatabasePort,
        PhpCgiPort = PhpCgiPort,
        EnableLocalHttps = EnableLocalHttps,
        SelectedPhpVersionId = SelectedPhpVersionId,
        SelectedDatabaseVersionId = SelectedDatabaseVersionId,
        PreferredBrowser = PreferredBrowser,
        CustomBrowserPath = CustomBrowserPath,
        RunOnWindowsStartup = RunOnWindowsStartup,
        Theme = Theme,
        HostnameSuffix = HostnameSuffix,
        AutoCheckUpdatesOnStartup = AutoCheckUpdatesOnStartup,
        UpdateFeedUrl = UpdateFeedUrl
    };
}
