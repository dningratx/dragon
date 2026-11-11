using System.Globalization;
using System.Text.Json.Serialization;

namespace DraGon.Core.Models;

public enum UpdatePackageType
{
    Patch,
    Full
}

public sealed class AppUpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("minimumSupportedVersion")]
    public string MinimumSupportedVersion { get; set; } = "1.0.0";

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("changelog")]
    public string Changelog { get; set; } = string.Empty;

    [JsonPropertyName("updateType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UpdatePackageType UpdateType { get; set; } = UpdatePackageType.Patch;

    [JsonPropertyName("patchPackageUrl")]
    public string PatchPackageUrl { get; set; } = string.Empty;

    [JsonPropertyName("patchSizeBytes")]
    public long PatchSizeBytes { get; set; }

    [JsonPropertyName("patchSha256")]
    public string? PatchSha256 { get; set; }

    [JsonPropertyName("fullPackageUrl")]
    public string FullPackageUrl { get; set; } = string.Empty;

    [JsonPropertyName("fullSizeBytes")]
    public long FullSizeBytes { get; set; }

    [JsonPropertyName("changedFiles")]
    public List<string> ChangedFiles { get; set; } = [];

    [JsonPropertyName("isMandatory")]
    public bool IsMandatory { get; set; }

    public string FormattedPatchSize => FormatBytes(PatchSizeBytes);
    public string FormattedFullSize => FormatBytes(FullSizeBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < units.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len.ToString("0.#", CultureInfo.InvariantCulture)} {units[order]}";
    }
}

public sealed class AppUpdateCheckResult
{
    public bool HasUpdate { get; init; }
    public bool IsPatchAvailable { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public AppUpdateManifest? Manifest { get; init; }
    public string Message { get; init; } = string.Empty;
}
