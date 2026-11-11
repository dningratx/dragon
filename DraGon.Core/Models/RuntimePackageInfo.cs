using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DraGon.Core.Models;

public partial class RuntimePackageInfo : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string RecommendedVersion { get; init; } = string.Empty;
    public string License { get; init; } = string.Empty;
    public string OfficialWebsiteUrl { get; init; } = string.Empty;
    public string OfficialDownloadUrl { get; init; } = string.Empty;
    public string? DirectArchiveDownloadUrl { get; init; }
    public string ExpectedRelativeDir { get; init; } = string.Empty;
    public string ExecutableFileName { get; init; } = string.Empty;
    public string InstallInstructions { get; init; } = string.Empty;

    public ObservableCollection<PackageVersionOption> AvailableVersions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveDownloadUrl))]
    [NotifyPropertyChangedFor(nameof(HasDirectArchiveUrl))]
    [NotifyPropertyChangedFor(nameof(DownloadButtonText))]
    [NotifyPropertyChangedFor(nameof(DownloadButtonToolTip))]
    [NotifyPropertyChangedFor(nameof(CanDownloadAndInstall))]
    private PackageVersionOption? _selectedDownloadVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAndInstall))]
    [NotifyPropertyChangedFor(nameof(DownloadButtonText))]
    [NotifyPropertyChangedFor(nameof(DownloadButtonToolTip))]
    [NotifyPropertyChangedFor(nameof(SelectedVersionStatusText))]
    private bool _isSelectedVersionInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadgeText))]
    [NotifyPropertyChangedFor(nameof(CanDownloadAndInstall))]
    [NotifyPropertyChangedFor(nameof(DownloadButtonText))]
    [NotifyPropertyChangedFor(nameof(DownloadButtonToolTip))]
    [NotifyPropertyChangedFor(nameof(WebsiteOrUpdateActionText))]
    [NotifyPropertyChangedFor(nameof(WebsiteOrUpdateActionToolTip))]
    private bool _isAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadgeText))]
    private bool _isFromSystemPath;

    [ObservableProperty]
    private string? _detectedExecutablePath;

    [ObservableProperty]
    private string? _detectedVersion;

    [ObservableProperty]
    private string _targetRuntimeFolder = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallOrImport))]
    [NotifyPropertyChangedFor(nameof(CanDownloadAndInstall))]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgressPercent;

    [ObservableProperty]
    private string _installStatusMessage = string.Empty;

    public string? EffectiveDownloadUrl => SelectedDownloadVersion?.DownloadUrl ?? DirectArchiveDownloadUrl;
    public bool HasDirectArchiveUrl => !string.IsNullOrWhiteSpace(EffectiveDownloadUrl);
    public bool HasMultipleVersions => AvailableVersions.Count > 0;
    public bool CanInstallOrImport => !IsInstalling;

    public bool CanDownloadAndInstall
    {
        get
        {
            if (IsInstalling) return false;
            if (!IsAvailable) return true;
            if (AvailableVersions.Count > 0 && !IsSelectedVersionInstalled) return true;
            return false;
        }
    }

    public string SelectedVersionStatusText => IsSelectedVersionInstalled ? "✔ Terpasang" : "○ Belum Terpasang";

    public string DownloadButtonText
    {
        get
        {
            if (IsAvailable && (AvailableVersions.Count == 0 || IsSelectedVersionInstalled))
            {
                return "✔ Terpasang";
            }

            return "⬇ Pasang";
        }
    }

    public string DownloadButtonToolTip
    {
        get
        {
            if (IsAvailable && (AvailableVersions.Count == 0 || IsSelectedVersionInstalled))
            {
                return "Versi ini sudah terpasang dan siap digunakan. Jika ingin memasang versi lain, pilih dari menu pilihan versi atau gunakan tombol 'Impor ZIP'.";
            }

            var verName = SelectedDownloadVersion?.DisplayName ?? RecommendedVersion;
            return $"Unduh dan pasang versi '{verName}' langsung ke folder runtime DraGon.";
        }
    }

    public string WebsiteOrUpdateActionText => IsAvailable ? "🌐 Versi Terbaru" : "🌐 Situs Resmi";
    public string WebsiteOrUpdateActionToolTip => IsAvailable
        ? "Buka halaman rilis resmi untuk melihat dan mengunduh versi terbaru"
        : "Buka situs resmi dan dokumentasi paket ini";

    public string StatusBadgeText => !IsAvailable
        ? "○ Belum Terpasang"
        : IsFromSystemPath
            ? "✔ Terdeteksi (System PATH)"
            : "✔ Terpasang di DraGon";

    partial void OnSelectedDownloadVersionChanged(PackageVersionOption? value)
    {
        DraGon.Core.Services.RuntimePackageCatalogService.UpdatePackageVersionSelectionState(this);
    }
}
