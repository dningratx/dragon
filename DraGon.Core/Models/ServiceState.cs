using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DraGon.Core.Models;

public enum ServiceKind
{
    Nginx,
    Apache,
    PhpCgi,
    MariaDb,
    PostgreSql
}

public enum ServiceStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    MissingRuntime,
    PortConflict,
    Error
}

public partial class ServiceState : ObservableObject
{
    public ServiceKind Kind { get; init; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PortDisplay))]
    private int _configuredPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPortInUseExternally))]
    private bool _portOccupiedByExternalProcess;

    [ObservableProperty]
    private string? _portOwnerInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsMissingRuntime))]
    private ServiceStatus _status = ServiceStatus.MissingRuntime;

    [ObservableProperty]
    private string _statusMessage = "Memeriksa runtime...";

    [ObservableProperty]
    private string _installationGuide = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PidDisplay))]
    private int? _processId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveVersion))]
    [NotifyPropertyChangedFor(nameof(ActiveVersionDisplay))]
    [NotifyPropertyChangedFor(nameof(ActiveVersionToolTip))]
    private string? _activeVersion;

    [ObservableProperty]
    private string? _executablePath;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    private string? _argumentsSummary;

    [ObservableProperty]
    private DateTime? _startedAt;

    [ObservableProperty]
    private string _logFilePath = string.Empty;

    public ObservableCollection<string> RecentLogs { get; } = [];

    public bool IsRunning => Status == ServiceStatus.Running;
    public bool IsMissingRuntime => Status == ServiceStatus.MissingRuntime;
    public bool IsPortInUseExternally => PortOccupiedByExternalProcess && Status != ServiceStatus.Running;

    public bool CanStart => Status is ServiceStatus.Stopped or ServiceStatus.Error or ServiceStatus.PortConflict;
    public bool CanStop => Status is ServiceStatus.Running or ServiceStatus.Starting;

    public string PortDisplay => ConfiguredPort > 0 ? $"Port {ConfiguredPort}" : "N/A";
    public string PidDisplay => ProcessId.HasValue ? $"PID: {ProcessId.Value}" : "PID: -";

    public bool HasActiveVersion => !string.IsNullOrWhiteSpace(ActiveVersion);

    public string ActiveVersionDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ActiveVersion))
            {
                return string.Empty;
            }

            var trimmed = ActiveVersion.Trim();
            if (trimmed.StartsWith("MariaDB", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("MySQL", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Nginx", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Apache", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("PHP", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return trimmed.StartsWith('v') || trimmed.StartsWith('V')
                ? trimmed
                : $"v{trimmed}";
        }
    }

    public string ActiveVersionToolTip
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ActiveVersion))
            {
                return string.Empty;
            }

            return Status == ServiceStatus.Running
                ? $"Versi yang dijalankan: {ActiveVersionDisplay}"
                : $"Versi terpasang: {ActiveVersionDisplay}";
        }
    }

    public string StatusLabel => Status switch
    {
        ServiceStatus.Running => "Berjalan",
        ServiceStatus.Starting => "Memulai...",
        ServiceStatus.Stopping => "Menghentikan...",
        ServiceStatus.Stopped => "Berhenti",
        ServiceStatus.MissingRuntime => "Runtime Belum Tersedia",
        ServiceStatus.PortConflict => "Konflik Port",
        ServiceStatus.Error => "Gagal / Error",
        _ => "Tidak Diketahui"
    };
}
