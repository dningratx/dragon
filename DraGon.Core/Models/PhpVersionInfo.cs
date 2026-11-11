using CommunityToolkit.Mvvm.ComponentModel;

namespace DraGon.Core.Models;

public partial class PhpVersionInfo : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
    public string DirectoryPath { get; init; } = string.Empty;
    public string PhpExePath { get; init; } = string.Empty;
    public string PhpCgiExePath { get; init; } = string.Empty;
    public string PhpIniPath { get; init; } = string.Empty;
    public bool IsFromSystemPath { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveBadgeText))]
    private bool _isActiveDefault;

    public string ActiveBadgeText => IsActiveDefault ? "Aktif (Utama)" : "Tersedia";

    public override string ToString() => $"{DisplayName} ({VersionNumber})";
}

public partial class PhpExtensionItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string DllFileName { get; init; } = string.Empty;
    public bool IsZendExtension { get; init; }

    [ObservableProperty]
    private bool _isEnabled;

    public string DisplayLabel => IsZendExtension ? $"{Name} (zend)" : Name;
}

public partial class DatabaseVersionInfo : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string EngineType { get; init; } = "MariaDB";
    public string VersionNumber { get; init; } = string.Empty;
    public string DirectoryPath { get; init; } = string.Empty;
    public string MysqldExePath { get; init; } = string.Empty;
    public string DataDirectoryPath { get; init; } = string.Empty;
    public bool IsFromSystemPath { get; init; }

    [ObservableProperty]
    private bool _isActiveDefault;

    public override string ToString() => $"{DisplayName} ({VersionNumber})";
}

public sealed record DownloadableVersionPreset(
    string Category,
    string FolderName,
    string DisplayName,
    string DownloadUrl)
{
    public override string ToString() => DisplayName;
}
