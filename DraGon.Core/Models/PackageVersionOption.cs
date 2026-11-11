namespace DraGon.Core.Models;

public sealed class PackageVersionOption
{
    public string DisplayName { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string? TargetSubFolder { get; init; }
    public bool IsRecommended { get; init; }

    public override string ToString() => DisplayName;
}
