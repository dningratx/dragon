using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Text;
using System.Text.Json;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed class HostsSyncRequest
{
    public List<string> ManagedDomains { get; set; } = [];
    public string BackupDirectory { get; set; } = string.Empty;
}

public sealed record HostsOperationResult(
    bool Succeeded,
    bool UsedElevation,
    bool CancelledByUser,
    string Message,
    string? BackupFilePath = null);

public sealed record HostsInspectionSnapshot(
    HashSet<string> ManagedDomains,
    HashSet<string> ExternalDomains,
    string RawHostsPath);

public sealed class HostsFileService
{
    private readonly AppLogger _logger;

    public HostsFileService(AppLogger logger)
    {
        _logger = logger;
    }

    public HostsInspectionSnapshot InspectHostsFile(string? customHostsPath = null)
    {
        var hostsPath = customHostsPath ?? DraGonPaths.SystemHostsFilePath;
        var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var external = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(hostsPath))
            {
                return new HostsInspectionSnapshot(managed, external, hostsPath);
            }

            var lines = File.ReadAllLines(hostsPath, Encoding.UTF8);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var isManaged = line.Contains(DraGonPaths.ManagedHostTag, StringComparison.OrdinalIgnoreCase);
                var commentIndex = line.IndexOf('#');
                var activePart = commentIndex >= 0 ? line[..commentIndex].Trim() : line;

                var tokens = activePart.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                {
                    continue;
                }

                for (var i = 1; i < tokens.Length; i++)
                {
                    var host = tokens[i].Trim().ToLowerInvariant();
                    if (isManaged)
                    {
                        managed.Add(host);
                    }
                    else
                    {
                        external.Add(host);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Hosts", "Gagal membaca file hosts sistem.", ex);
        }

        return new HostsInspectionSnapshot(managed, external, hostsPath);
    }

    public void RefreshProjectsDomainStatus(IEnumerable<ProjectItem> projects, string? customHostsPath = null)
    {
        var snapshot = InspectHostsFile(customHostsPath);
        foreach (var project in projects)
        {
            var validation = ValidationService.ValidateDomain(project.Domain);
            if (!validation.IsValid)
            {
                project.DomainStatus = DomainSyncStatus.InvalidDomain;
                continue;
            }

            var domain = project.Domain.Trim().ToLowerInvariant();
            if (snapshot.ExternalDomains.Contains(domain))
            {
                project.DomainStatus = DomainSyncStatus.ExternalConflict;
            }
            else if (snapshot.ManagedDomains.Contains(domain))
            {
                project.DomainStatus = DomainSyncStatus.Synced;
            }
            else
            {
                project.DomainStatus = DomainSyncStatus.NotSynced;
            }
        }
    }

    public ValidationResult CheckDomainConflict(
        string candidateDomain,
        IEnumerable<ProjectItem> existingProjects,
        Guid? ignoreProjectId = null,
        string? customHostsPath = null,
        string? expectedSuffix = null)
    {
        var suffixToValidate = !string.IsNullOrWhiteSpace(expectedSuffix)
            ? expectedSuffix
            : (candidateDomain.Contains('.') ? candidateDomain[candidateDomain.LastIndexOf('.')..] : ".dra");

        var formatValidation = ValidationService.ValidateDomain(candidateDomain, suffixToValidate);
        if (!formatValidation.IsValid)
        {
            return formatValidation;
        }

        var normalized = candidateDomain.Trim().ToLowerInvariant();

        foreach (var project in existingProjects)
        {
            if (ignoreProjectId.HasValue && project.Id == ignoreProjectId.Value)
            {
                continue;
            }

            if (string.Equals(project.Domain.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Fail(
                    $"Domain '{normalized}' sudah digunakan oleh proyek '{project.Name}' di DraGon.");
            }
        }

        var snapshot = InspectHostsFile(customHostsPath);
        if (snapshot.ExternalDomains.Contains(normalized))
        {
            return ValidationResult.Fail(
                $"Domain '{normalized}' sudah terdaftar pada file hosts di luar kendali DraGon. Demi keamanan, DraGon tidak menimpa entri milik pengguna atau aplikasi lain.");
        }

        return ValidationResult.Success();
    }

    public async Task<HostsOperationResult> SyncManagedDomainsAsync(
        IEnumerable<string> targetDomains,
        string? customHostsPath = null)
    {
        var hostsPath = customHostsPath ?? DraGonPaths.SystemHostsFilePath;
        var cleanDomains = new List<string>();

        foreach (var raw in targetDomains)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var norm = raw.Trim().ToLowerInvariant();
            var check = ValidationService.ValidateDomain(norm);
            if (!check.IsValid)
            {
                return new HostsOperationResult(false, false, false, check.ErrorMessage);
            }

            if (!cleanDomains.Contains(norm, StringComparer.OrdinalIgnoreCase))
            {
                cleanDomains.Add(norm);
            }
        }

        var snapshot = InspectHostsFile(hostsPath);
        foreach (var domain in cleanDomains)
        {
            if (snapshot.ExternalDomains.Contains(domain))
            {
                return new HostsOperationResult(
                    false,
                    false,
                    false,
                    $"Konflik terdeteksi: Domain '{domain}' sudah ada di file hosts sebagai entri eksternal (bukan dikelola DraGon).");
            }
        }

        if (snapshot.ManagedDomains.Count == cleanDomains.Count &&
            cleanDomains.All(d => snapshot.ManagedDomains.Contains(d)))
        {
            return new HostsOperationResult(
                true,
                UsedElevation: false,
                CancelledByUser: false,
                $"Seluruh domain ({cleanDomains.Count} domain .dra) sudah terdaftar aktif di file hosts Windows.");
        }

        // First attempt direct write (succeeds if user already has permission or custom test hosts path)
        try
        {
            var backupPath = ApplyManagedDomainsDirectly(hostsPath, DraGonPaths.BackupsDirectory, cleanDomains);
            TryFlushWindowsDnsCache();
            _logger.Info("Hosts", $"Sinkronisasi {cleanDomains.Count} domain .dra ke hosts berhasil (tanpa elevasi tambahan). Backup: {backupPath}");
            return new HostsOperationResult(
                true,
                UsedElevation: false,
                CancelledByUser: false,
                $"File hosts berhasil diperbarui ({cleanDomains.Count} domain aktif). Salinan cadangan dibuat.",
                backupPath);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.Info("Hosts", "Mengubah file hosts memerlukan hak Administrator. Meminta izin UAC melalui DraGon.Elevator...");
        }
        catch (Exception ex) when (ex is IOException or SecurityException)
        {
            _logger.Info("Hosts", $"Akses langsung ke hosts ditolak ({ex.Message}). Menggunakan helper DraGon.Elevator...");
        }

        return await RunElevatedSyncAsync(cleanDomains);
    }

    public static string ApplyManagedDomainsDirectly(
        string hostsPath,
        string backupDirectory,
        IReadOnlyCollection<string> managedDomains)
    {
        foreach (var domain in managedDomains)
        {
            var validation = ValidationService.ValidateDomain(domain);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.ErrorMessage);
            }
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var backupFilePath = Path.Combine(backupDirectory, $"hosts_{timestamp}.bak");

        var existingLines = Array.Empty<string>();
        if (File.Exists(hostsPath))
        {
            // Verify write access before creating backup so non-elevated probe doesn't leave duplicate backups
            using (new FileStream(hostsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
            }

            Directory.CreateDirectory(backupDirectory);
            File.Copy(hostsPath, backupFilePath, overwrite: true);
            existingLines = File.ReadAllLines(hostsPath, Encoding.UTF8);
        }
        else
        {
            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(backupFilePath, string.Empty, Encoding.UTF8);
        }

        var preservedLines = new List<string>(existingLines.Length);
        foreach (var line in existingLines)
        {
            if (line.Contains(DraGonPaths.ManagedHostTag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            preservedLines.Add(line);
        }

        // Trim trailing empty lines softly while keeping existing content intact
        while (preservedLines.Count > 0 && string.IsNullOrWhiteSpace(preservedLines[^1]))
        {
            preservedLines.RemoveAt(preservedLines.Count - 1);
        }

        if (managedDomains.Count > 0)
        {
            if (preservedLines.Count > 0)
            {
                preservedLines.Add(string.Empty);
            }

            foreach (var domain in managedDomains.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                preservedLines.Add($"127.0.0.1 {domain.ToLowerInvariant()} {DraGonPaths.ManagedHostTag}");
            }
        }

        preservedLines.Add(string.Empty);
        var finalContent = string.Join(Environment.NewLine, preservedLines);
        File.WriteAllText(hostsPath, finalContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return backupFilePath;
    }

    private async Task<HostsOperationResult> RunElevatedSyncAsync(List<string> cleanDomains)
    {
        var elevatorPath = ResolveElevatorExecutablePath();
        if (elevatorPath is null || !File.Exists(elevatorPath))
        {
            const string msg = "Perubahan file hosts memerlukan hak Administrator, tetapi komponen helper 'DraGon.Elevator.exe' tidak ditemukan.";
            _logger.Error("Hosts", msg);
            return new HostsOperationResult(false, false, false, msg);
        }

        DraGonPaths.EnsureDirectoriesExist();
        var requestFile = Path.Combine(DraGonPaths.TempDirectory, $"hosts_sync_{Guid.NewGuid():N}.json");

        try
        {
            var payload = new HostsSyncRequest
            {
                ManagedDomains = cleanDomains,
                BackupDirectory = DraGonPaths.BackupsDirectory
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(requestFile, json, Encoding.UTF8);

            var exitCode = await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = elevatorPath,
                    Arguments = $"--sync-hosts \"{requestFile}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return -999;
                }

                process.WaitForExit(30000);
                return process.HasExited ? process.ExitCode : -998;
            });

            if (exitCode == -999)
            {
                return new HostsOperationResult(
                    false,
                    true,
                    false,
                    "Gagal menjalankan helper elevasi Administrator (DraGon.Elevator).");
            }

            if (exitCode == 0)
            {
                TryFlushWindowsDnsCache();
                _logger.Info("Hosts", $"Sinkronisasi hosts via DraGon.Elevator berhasil ({cleanDomains.Count} domain).");
                return new HostsOperationResult(
                    true,
                    UsedElevation: true,
                    CancelledByUser: false,
                    $"File hosts berhasil diperbarui melalui izin Administrator ({cleanDomains.Count} domain .dra aktif). Salinan cadangan telah disimpan di folder backups.");
            }

            var errMsg = $"Helper Administrator selesai dengan kode keluar {exitCode}. Pastikan file hosts tidak dikunci oleh antivirus.";
            _logger.Error("Hosts", errMsg);
            return new HostsOperationResult(false, true, false, errMsg);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            const string cancelMsg = "Permintaan hak Administrator (UAC) dibatalkan oleh pengguna. Perubahan pada file hosts belum diterapkan.";
            _logger.Warn("Hosts", cancelMsg);
            return new HostsOperationResult(false, true, true, cancelMsg);
        }
        catch (Exception ex)
        {
            _logger.Error("Hosts", "Gagal menjalankan elevasi UAC untuk pembaruan hosts.", ex);
            return new HostsOperationResult(
                false,
                true,
                false,
                $"Gagal memperbarui file hosts: {ex.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(requestFile))
                {
                    File.Delete(requestFile);
                }
            }
            catch
            {
                // Ignore temp cleanup failure
            }
        }
    }

    private static string? ResolveElevatorExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var directCandidate = Path.Combine(baseDir, "DraGon.Elevator.exe");
        if (File.Exists(directCandidate))
        {
            return directCandidate;
        }

        // Fallback search in sibling build output folders during development
        var dirInfo = new DirectoryInfo(baseDir);
        var rootCandidate = dirInfo.Parent?.Parent?.Parent?.Parent;
        if (rootCandidate is not null && rootCandidate.Exists)
        {
            var devCandidates = new[]
            {
                Path.Combine(rootCandidate.FullName, "DraGon.Elevator", "bin", "Debug", "net10.0", "DraGon.Elevator.exe"),
                Path.Combine(rootCandidate.FullName, "DraGon.Elevator", "bin", "Release", "net10.0", "DraGon.Elevator.exe")
            };

            foreach (var candidate in devCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static void TryFlushWindowsDnsCache()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig.exe",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
        }
        catch
        {
            // Ignore DNS cache flush failure
        }
    }
}
