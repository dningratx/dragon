using System.Text;
using System.Text.Json;
using DraGon.Core.Services;

namespace DraGon.Elevator;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2 || !string.Equals(args[0], "--sync-hosts", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Argumen tidak valid. Gunakan: DraGon.Elevator --sync-hosts <path-json>");
                return 2;
            }

            var requestPath = args[1];
            if (!File.Exists(requestPath))
            {
                Console.Error.WriteLine($"File permintaan tidak ditemukan: {requestPath}");
                return 3;
            }

            var json = File.ReadAllText(requestPath, Encoding.UTF8);
            var payload = JsonSerializer.Deserialize<HostsSyncRequest>(json);
            if (payload is null)
            {
                Console.Error.WriteLine("Payload permintaan kosong atau rusak.");
                return 4;
            }

            foreach (var domain in payload.ManagedDomains)
            {
                var check = ValidationService.ValidateDomain(domain);
                if (!check.IsValid)
                {
                    Console.Error.WriteLine($"Validasi domain gagal ({domain}): {check.ErrorMessage}");
                    return 5;
                }
            }

            var backupDir = string.IsNullOrWhiteSpace(payload.BackupDirectory)
                ? DraGonPaths.BackupsDirectory
                : payload.BackupDirectory;

            HostsFileService.ApplyManagedDomainsDirectly(
                DraGonPaths.SystemHostsFilePath,
                backupDir,
                payload.ManagedDomains);

            HostsFileService.TryFlushWindowsDnsCache();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Gagal memperbarui file hosts: {ex.Message}");
            return 1;
        }
    }
}
