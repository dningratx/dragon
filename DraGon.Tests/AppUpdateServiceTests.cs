using System.IO.Compression;
using System.Text.Json;
using DraGon.Core.Models;
using DraGon.Core.Services;
using Xunit;

namespace DraGon.Tests;

public sealed class AppUpdateServiceTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly AppLogger _logger;
    private readonly AppUpdateService _updateService;

    public AppUpdateServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), $"dragon_test_update_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testTempDir);
        _logger = new AppLogger();
        _updateService = new AppUpdateService(_logger);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v1.0.0", "v1.0.1", true)]
    [InlineData("1.0.0-preview", "1.0.1", true)]
    [InlineData("1.0.0.0", "1.0.0.1", true)]
    public void IsNewerVersion_ComparesSemVerAccurately(string current, string candidate, bool expectedIsNewer)
    {
        var isNewer = AppUpdateService.IsNewerVersion(current, candidate);
        Assert.Equal(expectedIsNewer, isNewer);
    }

    [Fact]
    public async Task CheckForUpdate_FromLocalManifest_DetectsPatchUpdate()
    {
        var manifestPath = Path.Combine(_testTempDir, "manifest.json");
        var manifest = new AppUpdateManifest
        {
            Version = "1.0.1",
            MinimumSupportedVersion = "1.0.0",
            Title = "DraGon v1.0.1 Patch",
            Changelog = "Fix bug database & SSL",
            UpdateType = UpdatePackageType.Patch,
            PatchPackageUrl = "https://example.com/patch-1.0.1.zip",
            PatchSizeBytes = 350000,
            FullPackageUrl = "https://example.com/full-1.0.1.exe",
            FullSizeBytes = 85000000,
            ChangedFiles = ["DraGon.Core.dll", "DraGon.App.dll"]
        };

        var json = JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(manifestPath, json);

        var result = await _updateService.CheckForUpdateAsync(manifestPath, currentVersionOverride: "1.0.0");

        Assert.True(result.HasUpdate);
        Assert.True(result.IsPatchAvailable);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("1.0.1", result.LatestVersion);
        Assert.NotNull(result.Manifest);
        Assert.Equal(2, result.Manifest.ChangedFiles.Count);
        Assert.Equal("341.8 KB", result.Manifest.FormattedPatchSize);
    }

    [Fact]
    public async Task CheckForUpdate_WhenAppAlreadyLatest_ReportsNoUpdate()
    {
        var manifestPath = Path.Combine(_testTempDir, "manifest.json");
        var manifest = new AppUpdateManifest
        {
            Version = "1.0.0",
            MinimumSupportedVersion = "1.0.0",
            UpdateType = UpdatePackageType.Patch
        };

        var json = JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(manifestPath, json);

        var result = await _updateService.CheckForUpdateAsync(manifestPath, currentVersionOverride: "1.0.0");

        Assert.False(result.HasUpdate);
        Assert.False(result.IsPatchAvailable);
    }

    [Fact]
    public async Task CheckForUpdate_WhenVersionBelowMinimumSupported_ForcesFullUpdate()
    {
        var manifestPath = Path.Combine(_testTempDir, "manifest.json");
        var manifest = new AppUpdateManifest
        {
            Version = "2.0.0",
            MinimumSupportedVersion = "1.5.0",
            UpdateType = UpdatePackageType.Patch,
            PatchPackageUrl = "https://example.com/patch-2.0.0.zip",
            FullPackageUrl = "https://example.com/full-2.0.0.exe"
        };

        var json = JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(manifestPath, json);

        // Versi saat ini 1.0.0 lebih rendah dari MinimumSupportedVersion (1.5.0) -> Patch tidak diizinkan, harus full update
        var result = await _updateService.CheckForUpdateAsync(manifestPath, currentVersionOverride: "1.0.0");

        Assert.True(result.HasUpdate);
        Assert.False(result.IsPatchAvailable);
    }

    [Fact]
    public void ExtractPatchDirectly_UpdatesOnlyChangedFiles_PreservesAllOtherDataAndRuntimes()
    {
        var appDir = Path.Combine(_testTempDir, "app");
        Directory.CreateDirectory(appDir);

        // File-file yang sudah ada di folder aplikasi sebelum update
        var coreDllPath = Path.Combine(appDir, "DraGon.Core.dll");
        var appDllPath = Path.Combine(appDir, "DraGon.App.dll");
        var runtimePhpPath = Path.Combine(appDir, "runtimes", "php", "php.exe");
        var projectFilePath = Path.Combine(appDir, "www", "proyekku", "index.php");
        var userConfigPath = Path.Combine(appDir, "config", "settings.json");

        Directory.CreateDirectory(Path.GetDirectoryName(runtimePhpPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFilePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath)!);

        File.WriteAllText(coreDllPath, "v1.0.0-CORE-ORIGINAL");
        File.WriteAllText(appDllPath, "v1.0.0-APP-ORIGINAL");
        File.WriteAllText(runtimePhpPath, "PHP-BINARY-8.3.12-DATA");
        File.WriteAllText(projectFilePath, "<?php echo 'Hello World';");
        File.WriteAllText(userConfigPath, "{ \"theme\": \"Dark\", \"userSetting\": true }");

        // Buat file patch zip yang HANYA berisi DraGon.Core.dll yang diperbarui
        var patchZip = Path.Combine(_testTempDir, "patch.zip");
        AppUpdateService.CreateSamplePatchPackage(patchZip, new Dictionary<string, string>
        {
            ["DraGon.Core.dll"] = "v1.0.1-CORE-UPDATED-PATCH"
        });

        // Ekstrak patch ke folder aplikasi
        var updatedCount = _updateService.ExtractPatchDirectly(patchZip, appDir);

        Assert.Equal(1, updatedCount);

        // 1. Verifikasi bahwa DraGon.Core.dll terbarui
        Assert.Equal("v1.0.1-CORE-UPDATED-PATCH", File.ReadAllText(coreDllPath));

        // 2. Verifikasi bahwa file lain (DraGon.App.dll, runtime PHP, proyek www, konfigurasi user) TETAP AMAN dan tidak berubah
        Assert.Equal("v1.0.0-APP-ORIGINAL", File.ReadAllText(appDllPath));
        Assert.Equal("PHP-BINARY-8.3.12-DATA", File.ReadAllText(runtimePhpPath));
        Assert.Equal("<?php echo 'Hello World';", File.ReadAllText(projectFilePath));
        Assert.Equal("{ \"theme\": \"Dark\", \"userSetting\": true }", File.ReadAllText(userConfigPath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup
        }
    }
}
