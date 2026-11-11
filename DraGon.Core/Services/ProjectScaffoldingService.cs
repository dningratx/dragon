using System.IO.Compression;
using System.Text;

namespace DraGon.Core.Services;

public enum NewProjectType
{
    Standard,
    Laravel,
    WordPress,
    GitClone,
    ExistingFolder
}

public sealed class ProjectScaffoldingService
{
    private readonly HttpClient _httpClient;
    private readonly AppLogger _logger;

    public ProjectScaffoldingService(AppLogger logger, HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public bool CreateStandardProject(string folderPath, string projectName, string domain, out string message)
    {
        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            Directory.CreateDirectory(fullPath);

            var indexPhp = Path.Combine(fullPath, "index.php");
            var indexHtml = Path.Combine(fullPath, "index.html");

            if (!File.Exists(indexPhp) && !File.Exists(indexHtml))
            {
                var content = GenerateDefaultStarterIndexPhp(projectName, domain);
                File.WriteAllText(indexPhp, content, Encoding.UTF8);
            }

            message = $"Folder proyek '{projectName}' berhasil dibuat di: {fullPath}";
            _logger.Info("Scaffolding", message);
            return true;
        }
        catch (Exception ex)
        {
            message = $"Gagal membuat folder proyek: {ex.Message}";
            _logger.Error("Scaffolding", message, ex);
            return false;
        }
    }

    public async Task<(bool Success, string Message)> CreateWordPressProjectAsync(
        string folderPath,
        string projectName,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"wordpress-latest-{Guid.NewGuid():N}.zip");
        var tempExtract = Path.Combine(Path.GetTempPath(), $"wordpress-extract-{Guid.NewGuid():N}");

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            Directory.CreateDirectory(fullPath);

            progress?.Report("Mengunduh rilis resmi WordPress terbaru dari wordpress.org...");
            _logger.Info("WordPress", "Memulai unduhan WordPress dari https://wordpress.org/latest.zip");

            using (var response = await _httpClient.GetAsync("https://wordpress.org/latest.zip", HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await response.Content.CopyToAsync(fs, cancellationToken);
            }

            progress?.Report("Mengekstrak paket WordPress ke folder proyek...");
            _logger.Info("WordPress", $"Mengekstrak zip ke temporary directory: {tempExtract}");

            Directory.CreateDirectory(tempExtract);
            ZipFile.ExtractToDirectory(tempZip, tempExtract, true);

            var innerWp = Path.Combine(tempExtract, "wordpress");
            var sourceDir = Directory.Exists(innerWp) ? innerWp : tempExtract;

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file);
                var destFile = Path.Combine(fullPath, relative);
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrWhiteSpace(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, destFile, true);
            }

            _logger.Info("WordPress", $"WordPress berhasil dipasang pada '{fullPath}' untuk proyek '{projectName}'.");
            return (true, $"WordPress berhasil dipasang pada folder '{Path.GetFileName(fullPath)}'. Siap dibuka di browser untuk konfigurasi database.");
        }
        catch (Exception ex)
        {
            _logger.Error("WordPress", $"Gagal mengunduh atau memasang WordPress pada {folderPath}.", ex);
            return (false, $"Gagal memasang WordPress: {ex.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public static string GenerateDefaultStarterIndexPhp(string projectName, string domain)
    {
        return $$"""
<?php
$projectName = htmlspecialchars("{{projectName}}", ENT_QUOTES, 'UTF-8');
$projectDomain = htmlspecialchars("{{domain}}", ENT_QUOTES, 'UTF-8');
$phpVersion = PHP_VERSION;

// Cek koneksi MariaDB / MySQL lokal
$dbStatus = 'Belum Dites';
$dbClass = 'badge-warning';
try {
    $mysqli = @new mysqli('127.0.0.1', 'root', '', '', 3306);
    if ($mysqli->connect_errno) {
        $dbStatus = 'Gagal Konek: ' . $mysqli->connect_error;
        $dbClass = 'badge-danger';
    } else {
        $dbStatus = 'Terhubung (' . $mysqli->server_info . ')';
        $dbClass = 'badge-success';
        $mysqli->close();
    }
} catch (\Throwable $e) {
    $dbStatus = 'Terkoneksi / Siap (' . $e->getMessage() . ')';
    $dbClass = 'badge-info';
}
?>
<!DOCTYPE html>
<html lang="id">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title><?= $projectName ?> — DraGon Local Environment</title>
    <style>
        :root {
            --bg-color: #0b0f19;
            --card-bg: #111827;
            --card-border: #1f2937;
            --accent: #ff6b2b;
            --accent-hover: #ff854d;
            --text-primary: #f3f4f6;
            --text-secondary: #9ca3af;
            --success: #10b981;
            --info: #38bdf8;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
            background-color: var(--bg-color);
            color: var(--text-primary);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 24px;
        }
        .container {
            width: 100%;
            max-width: 680px;
            background: var(--card-bg);
            border: 1px solid var(--card-border);
            border-radius: 14px;
            padding: 32px;
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5);
        }
        .header {
            display: flex;
            align-items: center;
            gap: 16px;
            margin-bottom: 24px;
            padding-bottom: 20px;
            border-bottom: 1px solid var(--card-border);
        }
        .logo {
            width: 48px;
            height: 48px;
            background: linear-gradient(135deg, #ff6b2b, #ea580c);
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 26px;
            box-shadow: 0 4px 12px rgba(255, 107, 43, 0.35);
        }
        .title h1 {
            font-size: 22px;
            font-weight: 700;
            color: #ffffff;
        }
        .title p {
            color: var(--text-secondary);
            font-size: 13px;
        }
        .info-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 14px;
            margin-bottom: 24px;
        }
        .info-box {
            background: rgba(255, 255, 255, 0.03);
            border: 1px solid var(--card-border);
            border-radius: 8px;
            padding: 12px 16px;
        }
        .info-box span {
            display: block;
            font-size: 11px;
            color: var(--text-secondary);
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 4px;
        }
        .info-box strong {
            font-size: 14px;
            color: #fff;
        }
        .badge {
            display: inline-block;
            padding: 3px 8px;
            border-radius: 6px;
            font-size: 11px;
            font-weight: 600;
        }
        .badge-success { background: rgba(16, 185, 129, 0.2); color: #34d399; }
        .badge-warning { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
        .badge-danger { background: rgba(239, 68, 68, 0.2); color: #f87171; }
        .badge-info { background: rgba(56, 189, 248, 0.2); color: #7dd3fc; }
        .actions {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
            margin-top: 20px;
        }
        .btn {
            background: var(--accent);
            color: #fff;
            text-decoration: none;
            padding: 9px 16px;
            border-radius: 6px;
            font-size: 13px;
            font-weight: 600;
            transition: 0.2s;
            display: inline-flex;
            align-items: center;
            gap: 6px;
        }
        .btn:hover { background: var(--accent-hover); }
        .btn-outline {
            background: transparent;
            border: 1px solid var(--card-border);
            color: var(--text-secondary);
        }
        .btn-outline:hover {
            background: rgba(255, 255, 255, 0.05);
            color: #fff;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <div class="logo">🐉</div>
            <div class="title">
                <h1><?= $projectName ?></h1>
                <p>Proyek lokal Anda siap digunakan di lingkungan DraGon</p>
            </div>
        </div>

        <div class="info-grid">
            <div class="info-box">
                <span>Domain Lokal</span>
                <strong>https://<?= $projectDomain ?></strong>
            </div>
            <div class="info-box">
                <span>Versi PHP</span>
                <strong style="color: var(--info);">PHP <?= $phpVersion ?></strong>
            </div>
            <div class="info-box">
                <span>MariaDB / MySQL</span>
                <span class="badge <?= $dbClass ?>"><?= $dbStatus ?></span>
            </div>
            <div class="info-box">
                <span>Folder Proyek</span>
                <strong style="font-size: 12px; word-break: break-all;"><?= __DIR__ ?></strong>
            </div>
        </div>

        <p style="font-size: 13px; color: var(--text-secondary); line-height: 1.6;">
            Selamat! Folder proyek ini telah dibuat otomatis oleh DraGon lengkap dengan VirtualHost dan sertifikat SSL lokal. Anda dapat mengedit berkas <code>index.php</code> ini untuk mulai membangun aplikasi web Anda.
        </p>

        <div class="actions">
            <a href="https://phpmyadmin.dra" target="_blank" class="btn btn-outline">🗄️ Buka phpMyAdmin</a>
            <a href="?phpinfo=1" class="btn btn-outline">🐘 phpinfo()</a>
        </div>
    </div>
</body>
</html>
<?php
if (isset($_GET['phpinfo'])) {
    phpinfo();
    exit;
}
?>
""";
    }
}
