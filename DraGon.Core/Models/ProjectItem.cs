using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DraGon.Core.Models;

public enum ProjectFramework
{
    Laravel,
    WordPress,
    PhpGeneric,
    NodeApp,
    StaticOrOther
}

public enum DomainSyncStatus
{
    Synced,
    NotSynced,
    ExternalConflict,
    InvalidDomain
}

public partial class ProjectItem : ObservableObject
{
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryUrlDisplay))]
    private string _domain = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryUrlDisplay))]
    [NotifyPropertyChangedFor(nameof(ProtocolBadgeText))]
    private bool _httpsEnabled = true;

    [ObservableProperty]
    private string? _preferredPhpVersionId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameworkBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsLaravel))]
    private ProjectFramework _frameworkType = ProjectFramework.StaticOrOther;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameworkBadgeText))]
    private string? _detectedLaravelVersion;

    [ObservableProperty]
    private string? _detectedPhpConstraint;

    [ObservableProperty]
    private bool _hasArtisan;

    [ObservableProperty]
    private bool _hasComposerJson;

    [ObservableProperty]
    private bool _hasPackageJson;

    [ObservableProperty]
    private bool _hasNpmDevScript;

    [ObservableProperty]
    private string _documentRoot = string.Empty;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime? _lastOpenedAt;

    [JsonIgnore]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DomainStatusText))]
    private DomainSyncStatus _domainStatus = DomainSyncStatus.NotSynced;

    [JsonIgnore]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SslStatusText))]
    [NotifyPropertyChangedFor(nameof(ProtocolBadgeText))]
    private bool _hasSslCertificate;

    [JsonIgnore]
    [ObservableProperty]
    private bool _folderExists = true;

    [JsonIgnore]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartDevProcess))]
    [NotifyPropertyChangedFor(nameof(CanStopDevProcess))]
    private bool _isDevProcessRunning;

    [JsonIgnore]
    [ObservableProperty]
    private int? _devProcessPid;

    [JsonIgnore]
    [ObservableProperty]
    private string _devProcessStatusText = "Tidak ada proses dev latar belakang";

    [JsonIgnore]
    public bool IsLaravel => FrameworkType == ProjectFramework.Laravel;

    [JsonIgnore]
    public bool IsWordPress => FrameworkType == ProjectFramework.WordPress;

    [JsonIgnore]
    public bool CanStartDevProcess => !IsDevProcessRunning;

    [JsonIgnore]
    public bool CanStopDevProcess => IsDevProcessRunning;

    [JsonIgnore]
    public string PrimaryUrlDisplay => HttpsEnabled && HasSslCertificate
        ? $"https://{Domain}"
        : $"http://{Domain}";

    [JsonIgnore]
    public string ProtocolBadgeText => HttpsEnabled && HasSslCertificate
        ? "HTTPS (SSL Lokal)"
        : "HTTP";

    [JsonIgnore]
    public string SslStatusText => HasSslCertificate
        ? "Sertifikat SSL (.dra) Tersedia"
        : "Belum Ada Sertifikat SSL";

    [JsonIgnore]
    public string FrameworkBadgeText => FrameworkType switch
    {
        ProjectFramework.Laravel => string.IsNullOrWhiteSpace(DetectedLaravelVersion)
            ? "Laravel"
            : $"Laravel ({DetectedLaravelVersion})",
        ProjectFramework.WordPress => "WordPress",
        ProjectFramework.PhpGeneric => "PHP",
        ProjectFramework.NodeApp => "Node.js / Frontend",
        _ => "Web / Statis"
    };

    [JsonIgnore]
    public string DomainStatusText => DomainStatus switch
    {
        DomainSyncStatus.Synced => "Aktif (127.0.0.1)",
        DomainSyncStatus.NotSynced => "Belum Terdaftar di Hosts",
        DomainSyncStatus.ExternalConflict => "Konflik di Hosts Sistem",
        DomainSyncStatus.InvalidDomain => "Format Domain Tidak Valid",
        _ => "Tidak Diketahui"
    };
}
