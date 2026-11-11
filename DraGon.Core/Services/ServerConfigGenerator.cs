using System.Text;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed class ServerConfigGenerator
{
    private readonly AppLogger _logger;
    private readonly LocalSslService _sslService;

    public ServerConfigGenerator(AppLogger logger, LocalSslService? sslService = null)
    {
        _logger = logger;
        _sslService = sslService ?? new LocalSslService(logger);
    }

    public string NginxGeneratedRoot => Path.Combine(DraGonPaths.GeneratedServerConfigDirectory, "nginx");
    public string NginxSitesDirectory => Path.Combine(NginxGeneratedRoot, "sites-enabled");
    public string NginxMainConfigPath => Path.Combine(NginxGeneratedRoot, "nginx.conf");

    public string ApacheGeneratedRoot => Path.Combine(DraGonPaths.GeneratedServerConfigDirectory, "apache");
    public string ApacheSitesDirectory => Path.Combine(ApacheGeneratedRoot, "sites-enabled");
    public string ApacheVhostsIncludePath => Path.Combine(ApacheGeneratedRoot, "dragon-vhosts.conf");
    public string ApacheMainConfigPath => Path.Combine(ApacheGeneratedRoot, "httpd.conf");

    public void GenerateConfigs(
        AppSettings settings,
        IEnumerable<ProjectItem> projects,
        string? nginxRuntimeDir = null,
        string? apacheRuntimeDir = null)
    {
        try
        {
            Directory.CreateDirectory(NginxSitesDirectory);
            Directory.CreateDirectory(ApacheSitesDirectory);
            Directory.CreateDirectory(Path.Combine(NginxGeneratedRoot, "logs"));
            Directory.CreateDirectory(Path.Combine(ApacheGeneratedRoot, "logs"));
            Directory.CreateDirectory(Path.Combine(NginxGeneratedRoot, "temp", "client_body_temp"));
            Directory.CreateDirectory(Path.Combine(NginxGeneratedRoot, "temp", "proxy_temp"));
            Directory.CreateDirectory(Path.Combine(NginxGeneratedRoot, "temp", "fastcgi_temp"));
            Directory.CreateDirectory(Path.Combine(NginxGeneratedRoot, "temp", "uwsgi_temp"));
            Directory.CreateDirectory(Path.Combine(NginxGeneratedRoot, "temp", "scgi_temp"));

            var runtimeBase = string.IsNullOrWhiteSpace(settings.RuntimeBasePath)
                ? DraGonPaths.DefaultRuntimesDirectory
                : settings.RuntimeBasePath;

            if (string.IsNullOrWhiteSpace(nginxRuntimeDir))
            {
                nginxRuntimeDir = ResolveNginxRuntimeDirectory(runtimeBase);
            }

            if (string.IsNullOrWhiteSpace(apacheRuntimeDir))
            {
                apacheRuntimeDir = ResolveApacheRuntimeDirectory(runtimeBase);
            }

            var validProjects = projects
                .Where(p => ValidationService.ValidateDomain(p.Domain).IsValid && !string.IsNullOrWhiteSpace(p.FolderPath))
                .ToList();

            var phpMyAdminDir = ResolvePhpMyAdminDirectory(runtimeBase);
            EnsureBuiltInWebTools(settings, validProjects, phpMyAdminDir);

            _sslService.EnsureCertificatesForProjects(validProjects);
            if (settings.EnableLocalHttps)
            {
                TryEnsureUtilityDomainCertificates(settings.HostnameSuffix);
            }

            GenerateNginxSiteFiles(settings, validProjects, nginxRuntimeDir, phpMyAdminDir);
            GenerateApacheSiteFiles(settings, validProjects, apacheRuntimeDir, phpMyAdminDir);

            _logger.Info("VHost", $"Konfigurasi virtual host HTTP/HTTPS ({settings.HostnameSuffix}), PHP Info, dan phpMyAdmin berhasil diperbarui untuk {validProjects.Count} proyek.");
        }
        catch (Exception ex)
        {
            _logger.Error("VHost", "Gagal memperbarui file konfigurasi virtual host.", ex);
        }
    }

    public static string? ResolveNginxRuntimeDirectory(string? runtimeBasePath = null)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var root = Path.Combine(baseDir, "nginx");
        if (!Directory.Exists(root)) return null;

        if (File.Exists(Path.Combine(root, "nginx.exe")))
        {
            return root;
        }

        try
        {
            foreach (var sub in Directory.GetDirectories(root))
            {
                if (File.Exists(Path.Combine(sub, "nginx.exe")))
                {
                    return sub;
                }
            }
        }
        catch { }

        return root;
    }

    public static string? ResolveApacheRuntimeDirectory(string? runtimeBasePath = null)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var root = Path.Combine(baseDir, "apache");
        if (!Directory.Exists(root)) return null;

        if (File.Exists(Path.Combine(root, "bin", "httpd.exe")))
        {
            return root;
        }

        try
        {
            foreach (var sub in Directory.GetDirectories(root))
            {
                if (File.Exists(Path.Combine(sub, "bin", "httpd.exe")))
                {
                    return sub;
                }
            }
        }
        catch { }

        return root;
    }

    public static string? ResolvePhpMyAdminDirectory(string? runtimeBasePath = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(runtimeBasePath))
        {
            candidates.Add(Path.Combine(runtimeBasePath, "phpmyadmin"));
        }

        candidates.Add(Path.Combine(DraGonPaths.DefaultRuntimesDirectory, "phpmyadmin"));
        candidates.Add(Path.Combine(DraGonPaths.DefaultCustomApplicationDirectory, "runtimes", "phpmyadmin"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(candidate))
            {
                if (File.Exists(Path.Combine(candidate, "index.php")))
                {
                    return candidate;
                }

                try
                {
                    foreach (var sub in Directory.GetDirectories(candidate))
                    {
                        if (File.Exists(Path.Combine(sub, "index.php")))
                        {
                            return sub;
                        }
                    }
                }
                catch { }
            }
        }

        return null;
    }

    public static void EnsurePhpMyAdminConfigured(string phpMyAdminDir, int databasePort = 3306)
    {
        if (string.IsNullOrWhiteSpace(phpMyAdminDir) || !Directory.Exists(phpMyAdminDir))
        {
            return;
        }

        try
        {
            var tmpDir = Path.Combine(phpMyAdminDir, "tmp");
            Directory.CreateDirectory(tmpDir);

            var normalizedTmp = NormalizeSlash(tmpDir);
            var configPath = Path.Combine(phpMyAdminDir, "config.inc.php");
            var configContent = $$"""
                <?php
                declare(strict_types=1);
                // Auto-generated by DraGon Local Development Environment (.dra)
                error_reporting(E_ALL & ~E_DEPRECATED & ~E_STRICT & ~E_NOTICE);
                @ini_set('display_errors', '0');

                $cfg['blowfish_secret'] = 'DraGonLocalDevSecretKey2026PhpMyAdmin!';

                $i = 1;
                $cfg['Servers'][$i]['auth_type'] = 'cookie';
                $cfg['Servers'][$i]['host'] = '127.0.0.1';
                $cfg['Servers'][$i]['port'] = '{{databasePort}}';
                $cfg['Servers'][$i]['compress'] = false;
                $cfg['Servers'][$i]['AllowNoPassword'] = true;

                $cfg['UploadDir'] = '';
                $cfg['SaveDir'] = '';
                $cfg['TempDir'] = '{{normalizedTmp}}';
                $cfg['SendErrorReports'] = 'never';
                $cfg['ShowPhpInfo'] = true;
                """;

            File.WriteAllText(configPath, configContent, new UTF8Encoding(false));
        }
        catch
        {
            // Ignore if directory is read-only
        }
    }

    private void EnsureBuiltInWebTools(AppSettings settings, List<ProjectItem> projects, string? phpMyAdminDir)
    {
        try
        {
            Directory.CreateDirectory(DraGonPaths.WebToolsDirectory);
            const string phpInfoScript = """
                <?php
                // Auto-generated by DraGon — PHP Information Page
                phpinfo();
                """;

            File.WriteAllText(DraGonPaths.PhpInfoFilePath, phpInfoScript, new UTF8Encoding(false));

            if (!string.IsNullOrWhiteSpace(settings.WorkspacePath))
            {
                Directory.CreateDirectory(settings.WorkspacePath);
                var workspacePhpInfo = Path.Combine(settings.WorkspacePath, "phpinfo.php");
                if (!File.Exists(workspacePhpInfo))
                {
                    File.WriteAllText(workspacePhpInfo, phpInfoScript, new UTF8Encoding(false));
                }

                var welcomeHtml = BuildLocalPortalPhpContent(settings, projects);
                var welcomeDir = Path.Combine(settings.WorkspacePath, "welcome");
                Directory.CreateDirectory(welcomeDir);
                File.WriteAllText(Path.Combine(welcomeDir, "index.php"), welcomeHtml, new UTF8Encoding(false));

                var rootIndex = Path.Combine(settings.WorkspacePath, "index.php");
                if (!File.Exists(rootIndex))
                {
                    File.WriteAllText(rootIndex, welcomeHtml, new UTF8Encoding(false));
                }
            }

            if (!string.IsNullOrWhiteSpace(phpMyAdminDir))
            {
                EnsurePhpMyAdminConfigured(phpMyAdminDir, settings.DatabasePort);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("WebTools", $"Peringatan saat menyiapkan skrip phpinfo/phpmyadmin: {ex.Message}");
        }
    }

    private static string BuildLocalPortalPhpContent(AppSettings settings, List<ProjectItem> projects)
    {
        var suffix = ValidationService.NormalizeHostnameSuffix(settings.HostnameSuffix);
        var projectLinks = new StringBuilder();
        foreach (var p in projects)
        {
            var url = p.PrimaryUrlDisplay;
            projectLinks.AppendLine($"            <a class=\"btn\" href=\"{url}\">🌐 {p.Name} ({p.Domain})</a>");
        }

        return $$"""
            <?php
            header('Content-Type: text/html; charset=utf-8');
            if (isset($_GET['q']) && $_GET['q'] === 'info') {
                phpinfo();
                exit;
            }
            ?>
            <!DOCTYPE html>
            <html lang="id">
            <head>
                <meta charset="utf-8" />
                <title>DraGon Local Development Environment ({{suffix}})</title>
                <style>
                    body { background:#0B0F17; color:#F8FAFC; font-family:Segoe UI, sans-serif; padding:42px 20px; text-align:center; margin:0; }
                    .card { max-width:740px; margin:0 auto; background:#161F30; border:1px solid #283548; border-radius:14px; padding:32px; box-shadow:0 12px 32px rgba(0,0,0,0.35); }
                    h1 { color:#FF6B35; margin-top:0; margin-bottom:8px; }
                    .badge { display:inline-block; background:#0F2922; color:#10B981; padding:6px 14px; border-radius:999px; font-weight:bold; font-size:13px; }
                    .actions { display:flex; flex-wrap:wrap; gap:12px; justify-content:center; margin-top:22px; }
                    .btn { display:inline-block; background:#1E293B; color:#F8FAFC; text-decoration:none; padding:10px 18px; border-radius:8px; border:1px solid #334155; font-weight:600; font-size:14px; transition:all .15s; }
                    .btn:hover { background:#FF6B35; border-color:#FF6B35; color:#fff; }
                    .btn-primary { background:#FF6B35; border-color:#FF6B35; color:#fff; }
                    .section-title { color:#94A3B8; font-size:12px; font-weight:bold; text-transform:uppercase; letter-spacing:1px; margin-top:28px; margin-bottom:10px; }
                </style>
            </head>
            <body>
                <div class="card">
                    <h1>DraGon ({{suffix}}) Aktif!</h1>
                    <p class="badge">PHP <?= PHP_VERSION ?> • <?= $_SERVER['SERVER_SOFTWARE'] ?? 'DraGon Web Server' ?></p>
                    <p style="color:#CBD5E1;">Lingkungan development lokal DraGon sudah berjalan dan siap melayani proyek PHP, Laravel, serta database MySQL/MariaDB Anda.</p>

                    <div class="section-title">Alat &amp; Utilitas Bawaan</div>
                    <div class="actions">
                        <a class="btn btn-primary" href="/phpmyadmin/">🗄️ Buka phpMyAdmin (Database)</a>
                        <a class="btn btn-primary" href="/phpinfo.php">🐘 Buka PHP Info (phpinfo)</a>
                    </div>

                    <?php if ({{(projects.Count > 0 ? "true" : "false")}}): ?>
                    <div class="section-title">Proyek Lokal ({{suffix}})</div>
                    <div class="actions">
            {{projectLinks}}        </div>
                    <?php endif; ?>
                </div>
            </body>
            </html>
            """;
    }

    private void TryEnsureUtilityDomainCertificates(string? hostnameSuffix = null)
    {
        var suffix = ValidationService.NormalizeHostnameSuffix(hostnameSuffix);
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost.dra",
            "phpmyadmin.dra",
            "phpinfo.dra",
            $"localhost{suffix}",
            $"phpmyadmin{suffix}",
            $"phpinfo{suffix}"
        };

        foreach (var domain in domains)
        {
            try
            {
                _sslService.EnsureDomainCertificate(domain);
            }
            catch
            {
                // Ignore if cert creation fails
            }
        }
    }

    private void GenerateNginxSiteFiles(AppSettings settings, List<ProjectItem> projects, string? nginxRuntimeDir, string? phpMyAdminDir)
    {
        foreach (var existing in Directory.GetFiles(NginxSitesDirectory, "*.conf"))
        {
            File.Delete(existing);
        }

        var errorLog = NormalizeSlash(Path.Combine(DraGonPaths.LogsDirectory, "nginx-error.log"));
        var accessLog = NormalizeSlash(Path.Combine(DraGonPaths.LogsDirectory, "nginx-access.log"));
        var pidPath = NormalizeSlash(Path.Combine(NginxGeneratedRoot, "logs", "nginx.pid"));
        var sitesGlob = NormalizeSlash(Path.Combine(NginxSitesDirectory, "*.conf"));
        var webToolsRoot = NormalizeSlash(DraGonPaths.WebToolsDirectory);
        var pmaRoot = !string.IsNullOrWhiteSpace(phpMyAdminDir) ? NormalizeSlash(phpMyAdminDir) : null;

        var clientBodyTemp = NormalizeSlash(Path.Combine(NginxGeneratedRoot, "temp", "client_body_temp"));
        var proxyTemp = NormalizeSlash(Path.Combine(NginxGeneratedRoot, "temp", "proxy_temp"));
        var fastcgiTemp = NormalizeSlash(Path.Combine(NginxGeneratedRoot, "temp", "fastcgi_temp"));
        var uwsgiTemp = NormalizeSlash(Path.Combine(NginxGeneratedRoot, "temp", "uwsgi_temp"));
        var scgiTemp = NormalizeSlash(Path.Combine(NginxGeneratedRoot, "temp", "scgi_temp"));

        foreach (var project in projects)
        {
            var domain = project.Domain.Trim().ToLowerInvariant();
            var docRoot = NormalizeSlash(string.IsNullOrWhiteSpace(project.DocumentRoot) ? project.FolderPath : project.DocumentRoot);
            var siteConfPath = Path.Combine(NginxSitesDirectory, $"{domain}.conf");

            var useSsl = settings.EnableLocalHttps && _sslService.HasDomainCertificate(domain);
            var certPaths = _sslService.GetDomainCertificatePaths(domain);

            var siteBuilder = new StringBuilder();
            siteBuilder.AppendLine($"# Auto-generated by DraGon for {project.Name} ({domain})");
            siteBuilder.AppendLine("server {");
            siteBuilder.AppendLine($"    listen 127.0.0.1:{settings.WebServerPort};");

            if (useSsl)
            {
                siteBuilder.AppendLine($"    listen 127.0.0.1:{settings.WebServerHttpsPort} ssl;");
                siteBuilder.AppendLine($"    ssl_certificate \"{NormalizeSlash(certPaths.CertificatePemPath)}\";");
                siteBuilder.AppendLine($"    ssl_certificate_key \"{NormalizeSlash(certPaths.PrivateKeyPemPath)}\";");
                siteBuilder.AppendLine("    ssl_protocols TLSv1.2 TLSv1.3;");
            }

            siteBuilder.AppendLine($"    server_name {domain};");
            siteBuilder.AppendLine($"    root \"{docRoot}\";");
            siteBuilder.AppendLine("    index index.php index.html index.htm;");
            siteBuilder.AppendLine("    charset utf-8;");
            siteBuilder.AppendLine();
            siteBuilder.AppendLine("    location = /phpinfo.php {");
            siteBuilder.AppendLine($"        root \"{webToolsRoot}\";");
            siteBuilder.AppendLine($"        fastcgi_pass 127.0.0.1:{settings.PhpCgiPort};");
            siteBuilder.AppendLine("        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
            siteBuilder.AppendLine("        fastcgi_param QUERY_STRING $query_string;");
            siteBuilder.AppendLine("        fastcgi_param REQUEST_METHOD $request_method;");
            siteBuilder.AppendLine("    }");
            siteBuilder.AppendLine();
            siteBuilder.AppendLine("    location / {");
            siteBuilder.AppendLine("        try_files $uri $uri/ /index.php?$query_string;");
            siteBuilder.AppendLine("    }");
            siteBuilder.AppendLine();
            siteBuilder.AppendLine("    location ~ \\.php$ {");
            siteBuilder.AppendLine($"        fastcgi_pass 127.0.0.1:{settings.PhpCgiPort};");
            siteBuilder.AppendLine("        fastcgi_index index.php;");
            siteBuilder.AppendLine("        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
            siteBuilder.AppendLine("        fastcgi_param QUERY_STRING $query_string;");
            siteBuilder.AppendLine("        fastcgi_param REQUEST_METHOD $request_method;");
            siteBuilder.AppendLine("        fastcgi_param CONTENT_TYPE $content_type;");
            siteBuilder.AppendLine("        fastcgi_param CONTENT_LENGTH $content_length;");
            siteBuilder.AppendLine("        fastcgi_param SCRIPT_NAME $fastcgi_script_name;");
            siteBuilder.AppendLine("        fastcgi_param REQUEST_URI $request_uri;");
            siteBuilder.AppendLine("        fastcgi_param DOCUMENT_URI $document_uri;");
            siteBuilder.AppendLine("        fastcgi_param DOCUMENT_ROOT $document_root;");
            siteBuilder.AppendLine("        fastcgi_param SERVER_PROTOCOL $server_protocol;");
            siteBuilder.AppendLine("        fastcgi_param HTTPS $https if_not_empty;");
            siteBuilder.AppendLine("        fastcgi_param GATEWAY_INTERFACE CGI/1.1;");
            siteBuilder.AppendLine("        fastcgi_param SERVER_SOFTWARE nginx/$nginx_version;");
            siteBuilder.AppendLine("        fastcgi_param REMOTE_ADDR $remote_addr;");
            siteBuilder.AppendLine("        fastcgi_param REMOTE_PORT $remote_port;");
            siteBuilder.AppendLine("        fastcgi_param SERVER_ADDR $server_addr;");
            siteBuilder.AppendLine("        fastcgi_param SERVER_PORT $server_port;");
            siteBuilder.AppendLine("        fastcgi_param SERVER_NAME $server_name;");
            siteBuilder.AppendLine("    }");
            siteBuilder.AppendLine("}");

            File.WriteAllText(siteConfPath, siteBuilder.ToString(), new UTF8Encoding(false));
        }

        // Utility Nginx site for phpinfo
        var suffix = ValidationService.NormalizeHostnameSuffix(settings.HostnameSuffix);
        var phpInfoHost = suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase)
            ? "phpinfo.dra"
            : $"phpinfo{suffix} phpinfo.dra";
        var pmaHost = suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase)
            ? "phpmyadmin.dra"
            : $"phpmyadmin{suffix} phpmyadmin.dra";
        var localhostHost = suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase)
            ? "localhost 127.0.0.1 localhost.dra"
            : $"localhost 127.0.0.1 localhost.dra localhost{suffix}";

        {
            var certDomain = _sslService.HasDomainCertificate($"phpinfo{suffix}") ? $"phpinfo{suffix}" : "phpinfo.dra";
            var useSsl = settings.EnableLocalHttps && _sslService.HasDomainCertificate(certDomain);
            var certPaths = _sslService.GetDomainCertificatePaths(certDomain);
            var phpInfoSite = new StringBuilder();
            phpInfoSite.AppendLine("server {");
            phpInfoSite.AppendLine($"    listen 127.0.0.1:{settings.WebServerPort};");
            if (useSsl)
            {
                phpInfoSite.AppendLine($"    listen 127.0.0.1:{settings.WebServerHttpsPort} ssl;");
                phpInfoSite.AppendLine($"    ssl_certificate \"{NormalizeSlash(certPaths.CertificatePemPath)}\";");
                phpInfoSite.AppendLine($"    ssl_certificate_key \"{NormalizeSlash(certPaths.PrivateKeyPemPath)}\";");
            }
            phpInfoSite.AppendLine($"    server_name {phpInfoHost};");
            phpInfoSite.AppendLine($"    root \"{webToolsRoot}\";");
            phpInfoSite.AppendLine("    index phpinfo.php;");
            phpInfoSite.AppendLine("    location / { try_files $uri /phpinfo.php?$query_string; }");
            phpInfoSite.AppendLine("    location ~ \\.php$ {");
            phpInfoSite.AppendLine($"        fastcgi_pass 127.0.0.1:{settings.PhpCgiPort};");
            phpInfoSite.AppendLine("        fastcgi_index phpinfo.php;");
            phpInfoSite.AppendLine("        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
            phpInfoSite.AppendLine("        fastcgi_param QUERY_STRING $query_string;");
            phpInfoSite.AppendLine("        fastcgi_param REQUEST_METHOD $request_method;");
            phpInfoSite.AppendLine("    }");
            phpInfoSite.AppendLine("}");
            File.WriteAllText(Path.Combine(NginxSitesDirectory, "phpinfo.dra.conf"), phpInfoSite.ToString(), new UTF8Encoding(false));
        }

        // Utility Nginx site for phpmyadmin
        if (!string.IsNullOrWhiteSpace(pmaRoot))
        {
            var certDomain = _sslService.HasDomainCertificate($"phpmyadmin{suffix}") ? $"phpmyadmin{suffix}" : "phpmyadmin.dra";
            var useSsl = settings.EnableLocalHttps && _sslService.HasDomainCertificate(certDomain);
            var certPaths = _sslService.GetDomainCertificatePaths(certDomain);
            var pmaSite = new StringBuilder();
            pmaSite.AppendLine("server {");
            pmaSite.AppendLine($"    listen 127.0.0.1:{settings.WebServerPort};");
            if (useSsl)
            {
                pmaSite.AppendLine($"    listen 127.0.0.1:{settings.WebServerHttpsPort} ssl;");
                pmaSite.AppendLine($"    ssl_certificate \"{NormalizeSlash(certPaths.CertificatePemPath)}\";");
                pmaSite.AppendLine($"    ssl_certificate_key \"{NormalizeSlash(certPaths.PrivateKeyPemPath)}\";");
            }
            pmaSite.AppendLine($"    server_name {pmaHost};");
            pmaSite.AppendLine($"    root \"{pmaRoot}\";");
            pmaSite.AppendLine("    index index.php index.html;");
            pmaSite.AppendLine("    location / { try_files $uri $uri/ /index.php?$query_string; }");
            pmaSite.AppendLine("    location ~ \\.php$ {");
            pmaSite.AppendLine($"        fastcgi_pass 127.0.0.1:{settings.PhpCgiPort};");
            pmaSite.AppendLine("        fastcgi_index index.php;");
            pmaSite.AppendLine("        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
            pmaSite.AppendLine("        fastcgi_param QUERY_STRING $query_string;");
            pmaSite.AppendLine("        fastcgi_param REQUEST_METHOD $request_method;");
            pmaSite.AppendLine("        fastcgi_param CONTENT_TYPE $content_type;");
            pmaSite.AppendLine("        fastcgi_param CONTENT_LENGTH $content_length;");
            pmaSite.AppendLine("        fastcgi_param SCRIPT_NAME $fastcgi_script_name;");
            pmaSite.AppendLine("        fastcgi_param REQUEST_URI $request_uri;");
            pmaSite.AppendLine("        fastcgi_param DOCUMENT_URI $document_uri;");
            pmaSite.AppendLine("        fastcgi_param DOCUMENT_ROOT $document_root;");
            pmaSite.AppendLine("        fastcgi_param SERVER_PROTOCOL $server_protocol;");
            pmaSite.AppendLine("        fastcgi_param GATEWAY_INTERFACE CGI/1.1;");
            pmaSite.AppendLine("        fastcgi_param REMOTE_ADDR $remote_addr;");
            pmaSite.AppendLine("        fastcgi_param SERVER_ADDR $server_addr;");
            pmaSite.AppendLine("        fastcgi_param SERVER_PORT $server_port;");
            pmaSite.AppendLine("        fastcgi_param SERVER_NAME $server_name;");
            pmaSite.AppendLine("    }");
            pmaSite.AppendLine("}");
            File.WriteAllText(Path.Combine(NginxSitesDirectory, "phpmyadmin.dra.conf"), pmaSite.ToString(), new UTF8Encoding(false));
        }

        var defaultWorkspace = NormalizeSlash(settings.WorkspacePath);
        var mainBuilder = new StringBuilder();
        mainBuilder.AppendLine("# Konfigurasi Nginx Lokal yang Dikelola DraGon");
        mainBuilder.AppendLine("worker_processes 1;");
        mainBuilder.AppendLine($"error_log \"{errorLog}\";");
        mainBuilder.AppendLine($"pid \"{pidPath}\";");
        mainBuilder.AppendLine();
        mainBuilder.AppendLine("events {");
        mainBuilder.AppendLine("    worker_connections 1024;");
        mainBuilder.AppendLine("}");
        mainBuilder.AppendLine();
        mainBuilder.AppendLine("http {");
        if (!string.IsNullOrWhiteSpace(nginxRuntimeDir))
        {
            var mimePath = Path.Combine(nginxRuntimeDir, "conf", "mime.types");
            if (File.Exists(mimePath))
            {
                mainBuilder.AppendLine($"    include \"{NormalizeSlash(mimePath)}\";");
            }
        }
        mainBuilder.AppendLine("    default_type application/octet-stream;");
        mainBuilder.AppendLine($"    access_log \"{accessLog}\";");
        mainBuilder.AppendLine($"    client_body_temp_path \"{clientBodyTemp}\";");
        mainBuilder.AppendLine($"    proxy_temp_path \"{proxyTemp}\";");
        mainBuilder.AppendLine($"    fastcgi_temp_path \"{fastcgiTemp}\";");
        mainBuilder.AppendLine($"    uwsgi_temp_path \"{uwsgiTemp}\";");
        mainBuilder.AppendLine($"    scgi_temp_path \"{scgiTemp}\";");
        mainBuilder.AppendLine("    sendfile on;");
        mainBuilder.AppendLine("    keepalive_timeout 65;");
        mainBuilder.AppendLine();
        mainBuilder.AppendLine("    server {");
        mainBuilder.AppendLine($"        listen 127.0.0.1:{settings.WebServerPort} default_server;");
        mainBuilder.AppendLine($"        server_name {localhostHost};");
        mainBuilder.AppendLine($"        root \"{defaultWorkspace}\";");
        mainBuilder.AppendLine("        index index.php index.html index.htm;");
        mainBuilder.AppendLine("        autoindex on;");
        mainBuilder.AppendLine();
        mainBuilder.AppendLine("        location = /phpinfo.php {");
        mainBuilder.AppendLine($"            root \"{webToolsRoot}\";");
        mainBuilder.AppendLine($"            fastcgi_pass 127.0.0.1:{settings.PhpCgiPort};");
        mainBuilder.AppendLine("            fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
        mainBuilder.AppendLine("            fastcgi_param QUERY_STRING $query_string;");
        mainBuilder.AppendLine("            fastcgi_param REQUEST_METHOD $request_method;");
        mainBuilder.AppendLine("        }");
        if (!string.IsNullOrWhiteSpace(pmaRoot))
        {
            mainBuilder.AppendLine();
            mainBuilder.AppendLine("        location ^~ /phpmyadmin/ {");
            mainBuilder.AppendLine($"            alias \"{pmaRoot.TrimEnd('/')}/\";");
            mainBuilder.AppendLine("            index index.php index.html;");
            mainBuilder.AppendLine("            location ~ ^/phpmyadmin/(.+\\.php)$ {");
            mainBuilder.AppendLine($"                alias \"{pmaRoot.TrimEnd('/')}/$1\";");
            mainBuilder.AppendLine($"                fastcgi_pass 127.0.0.1:{settings.PhpCgiPort};");
            mainBuilder.AppendLine("                fastcgi_index index.php;");
            mainBuilder.AppendLine("                fastcgi_param SCRIPT_FILENAME $request_filename;");
            mainBuilder.AppendLine("                fastcgi_param QUERY_STRING $query_string;");
            mainBuilder.AppendLine("                fastcgi_param REQUEST_METHOD $request_method;");
            mainBuilder.AppendLine("                fastcgi_param CONTENT_TYPE $content_type;");
            mainBuilder.AppendLine("                fastcgi_param CONTENT_LENGTH $content_length;");
            mainBuilder.AppendLine("                fastcgi_param SCRIPT_NAME /phpmyadmin/$1;");
            mainBuilder.AppendLine("                fastcgi_param REQUEST_URI $request_uri;");
            mainBuilder.AppendLine("                fastcgi_param DOCUMENT_URI $document_uri;");
            mainBuilder.AppendLine($"                fastcgi_param DOCUMENT_ROOT \"{pmaRoot.TrimEnd('/')}\";");
            mainBuilder.AppendLine("                fastcgi_param SERVER_PROTOCOL $server_protocol;");
            mainBuilder.AppendLine("                fastcgi_param GATEWAY_INTERFACE CGI/1.1;");
            mainBuilder.AppendLine("                fastcgi_param REMOTE_ADDR $remote_addr;");
            mainBuilder.AppendLine("                fastcgi_param SERVER_ADDR $server_addr;");
            mainBuilder.AppendLine("                fastcgi_param SERVER_PORT $server_port;");
            mainBuilder.AppendLine("                fastcgi_param SERVER_NAME $server_name;");
            mainBuilder.AppendLine("            }");
            mainBuilder.AppendLine("        }");
            mainBuilder.AppendLine("        location = /phpmyadmin { return 301 /phpmyadmin/; }");
        }
        mainBuilder.AppendLine();
        mainBuilder.AppendLine("        location ~ \\.php$ {");
        mainBuilder.AppendLine($"            fastcgi_pass 127.0.0.1:{settings.PhpCgiPort};");
        mainBuilder.AppendLine("            fastcgi_index index.php;");
        mainBuilder.AppendLine("            fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
        mainBuilder.AppendLine("            fastcgi_param QUERY_STRING $query_string;");
        mainBuilder.AppendLine("            fastcgi_param REQUEST_METHOD $request_method;");
        mainBuilder.AppendLine("            fastcgi_param CONTENT_TYPE $content_type;");
        mainBuilder.AppendLine("            fastcgi_param CONTENT_LENGTH $content_length;");
        mainBuilder.AppendLine("            fastcgi_param SCRIPT_NAME $fastcgi_script_name;");
        mainBuilder.AppendLine("            fastcgi_param REQUEST_URI $request_uri;");
        mainBuilder.AppendLine("            fastcgi_param DOCUMENT_URI $document_uri;");
        mainBuilder.AppendLine("            fastcgi_param DOCUMENT_ROOT $document_root;");
        mainBuilder.AppendLine("            fastcgi_param SERVER_PROTOCOL $server_protocol;");
        mainBuilder.AppendLine("            fastcgi_param GATEWAY_INTERFACE CGI/1.1;");
        mainBuilder.AppendLine("            fastcgi_param SERVER_SOFTWARE nginx/$nginx_version;");
        mainBuilder.AppendLine("            fastcgi_param REMOTE_ADDR $remote_addr;");
        mainBuilder.AppendLine("            fastcgi_param REMOTE_PORT $remote_port;");
        mainBuilder.AppendLine("            fastcgi_param SERVER_ADDR $server_addr;");
        mainBuilder.AppendLine("            fastcgi_param SERVER_PORT $server_port;");
        mainBuilder.AppendLine("            fastcgi_param SERVER_NAME $server_name;");
        mainBuilder.AppendLine("        }");
        mainBuilder.AppendLine("    }");
        mainBuilder.AppendLine();
        mainBuilder.AppendLine($"    include \"{sitesGlob}\";");
        mainBuilder.AppendLine("}");

        File.WriteAllText(NginxMainConfigPath, mainBuilder.ToString(), new UTF8Encoding(false));
    }

    private void GenerateApacheSiteFiles(AppSettings settings, List<ProjectItem> projects, string? apacheRuntimeDir, string? phpMyAdminDir)
    {
        var suffix = ValidationService.NormalizeHostnameSuffix(settings.HostnameSuffix);
        foreach (var existing in Directory.GetFiles(ApacheSitesDirectory, "*.conf"))
        {
            File.Delete(existing);
        }

        var aggregateBuilder = new StringBuilder();
        aggregateBuilder.AppendLine("# Konfigurasi VirtualHost Apache Lokal yang Dikelola DraGon");
        var hasAnySslVhost = false;

        foreach (var project in projects)
        {
            var domain = project.Domain.Trim().ToLowerInvariant();
            var docRoot = NormalizeSlash(string.IsNullOrWhiteSpace(project.DocumentRoot) ? project.FolderPath : project.DocumentRoot);
            var siteConfPath = Path.Combine(ApacheSitesDirectory, $"{domain}.conf");
            var useSsl = settings.EnableLocalHttps && _sslService.HasDomainCertificate(domain);
            var certPaths = _sslService.GetDomainCertificatePaths(domain);

            var vhostBuilder = new StringBuilder();
            vhostBuilder.AppendLine($"# Auto-generated by DraGon for {project.Name} ({domain})");
            vhostBuilder.AppendLine($"<VirtualHost *:{settings.ApachePort}>");
            vhostBuilder.AppendLine($"    ServerName {domain}");
            vhostBuilder.AppendLine($"    DocumentRoot \"{docRoot}\"");
            vhostBuilder.AppendLine("    DirectoryIndex index.php index.html index.htm");
            vhostBuilder.AppendLine($"    <Directory \"{docRoot}\">");
            vhostBuilder.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
            vhostBuilder.AppendLine("        AllowOverride All");
            vhostBuilder.AppendLine("        Require all granted");
            vhostBuilder.AppendLine("    </Directory>");
            vhostBuilder.AppendLine("    <FilesMatch \"\\.php$\">");
            vhostBuilder.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
            vhostBuilder.AppendLine("    </FilesMatch>");
            vhostBuilder.AppendLine("</VirtualHost>");

            if (useSsl)
            {
                hasAnySslVhost = true;
                vhostBuilder.AppendLine();
                vhostBuilder.AppendLine($"<VirtualHost *:{settings.ApacheHttpsPort}>");
                vhostBuilder.AppendLine($"    ServerName {domain}");
                vhostBuilder.AppendLine($"    DocumentRoot \"{docRoot}\"");
                vhostBuilder.AppendLine("    DirectoryIndex index.php index.html index.htm");
                vhostBuilder.AppendLine("    SSLEngine on");
                vhostBuilder.AppendLine($"    SSLCertificateFile \"{NormalizeSlash(certPaths.CertificatePemPath)}\"");
                vhostBuilder.AppendLine($"    SSLCertificateKeyFile \"{NormalizeSlash(certPaths.PrivateKeyPemPath)}\"");
                vhostBuilder.AppendLine($"    <Directory \"{docRoot}\">");
                vhostBuilder.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
                vhostBuilder.AppendLine("        AllowOverride All");
                vhostBuilder.AppendLine("        Require all granted");
                vhostBuilder.AppendLine("    </Directory>");
                vhostBuilder.AppendLine("    <FilesMatch \"\\.php$\">");
                vhostBuilder.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
                vhostBuilder.AppendLine("    </FilesMatch>");
                vhostBuilder.AppendLine("</VirtualHost>");
            }

            var content = vhostBuilder.ToString();
            File.WriteAllText(siteConfPath, content, new UTF8Encoding(false));
            aggregateBuilder.AppendLine(content);
        }

        // VirtualHost khusus untuk phpinfo
        {
            var webToolsRoot = NormalizeSlash(DraGonPaths.WebToolsDirectory);
            var phpInfoDomain = $"phpinfo{suffix}";
            var certDomain = _sslService.HasDomainCertificate(phpInfoDomain) ? phpInfoDomain : "phpinfo.dra";
            var useSsl = settings.EnableLocalHttps && _sslService.HasDomainCertificate(certDomain);
            var certPaths = _sslService.GetDomainCertificatePaths(certDomain);

            var phpInfoVhost = new StringBuilder();
            phpInfoVhost.AppendLine($"# Auto-generated by DraGon for PHP Info ({phpInfoDomain})");
            phpInfoVhost.AppendLine($"<VirtualHost *:{settings.ApachePort}>");
            phpInfoVhost.AppendLine($"    ServerName {phpInfoDomain}");
            if (!suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase))
            {
                phpInfoVhost.AppendLine("    ServerAlias phpinfo.dra");
            }
            phpInfoVhost.AppendLine($"    DocumentRoot \"{webToolsRoot}\"");
            phpInfoVhost.AppendLine("    DirectoryIndex phpinfo.php index.php");
            phpInfoVhost.AppendLine($"    <Directory \"{webToolsRoot}\">");
            phpInfoVhost.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
            phpInfoVhost.AppendLine("        AllowOverride All");
            phpInfoVhost.AppendLine("        Require all granted");
            phpInfoVhost.AppendLine("    </Directory>");
            phpInfoVhost.AppendLine("    <FilesMatch \"\\.php$\">");
            phpInfoVhost.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
            phpInfoVhost.AppendLine("    </FilesMatch>");
            phpInfoVhost.AppendLine("</VirtualHost>");

            if (useSsl)
            {
                hasAnySslVhost = true;
                phpInfoVhost.AppendLine();
                phpInfoVhost.AppendLine($"<VirtualHost *:{settings.ApacheHttpsPort}>");
                phpInfoVhost.AppendLine($"    ServerName {phpInfoDomain}");
                if (!suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase))
                {
                    phpInfoVhost.AppendLine("    ServerAlias phpinfo.dra");
                }
                phpInfoVhost.AppendLine($"    DocumentRoot \"{webToolsRoot}\"");
                phpInfoVhost.AppendLine("    DirectoryIndex phpinfo.php index.php");
                phpInfoVhost.AppendLine("    SSLEngine on");
                phpInfoVhost.AppendLine($"    SSLCertificateFile \"{NormalizeSlash(certPaths.CertificatePemPath)}\"");
                phpInfoVhost.AppendLine($"    SSLCertificateKeyFile \"{NormalizeSlash(certPaths.PrivateKeyPemPath)}\"");
                phpInfoVhost.AppendLine($"    <Directory \"{webToolsRoot}\">");
                phpInfoVhost.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
                phpInfoVhost.AppendLine("        AllowOverride All");
                phpInfoVhost.AppendLine("        Require all granted");
                phpInfoVhost.AppendLine("    </Directory>");
                phpInfoVhost.AppendLine("    <FilesMatch \"\\.php$\">");
                phpInfoVhost.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
                phpInfoVhost.AppendLine("    </FilesMatch>");
                phpInfoVhost.AppendLine("</VirtualHost>");
            }

            aggregateBuilder.AppendLine(phpInfoVhost.ToString());
        }

        // VirtualHost khusus untuk phpmyadmin
        if (!string.IsNullOrWhiteSpace(phpMyAdminDir))
        {
            var pmaRoot = NormalizeSlash(phpMyAdminDir).TrimEnd('/');
            var pmaDomain = $"phpmyadmin{suffix}";
            var certDomain = _sslService.HasDomainCertificate(pmaDomain) ? pmaDomain : "phpmyadmin.dra";
            var useSsl = settings.EnableLocalHttps && _sslService.HasDomainCertificate(certDomain);
            var certPaths = _sslService.GetDomainCertificatePaths(certDomain);

            var pmaVhost = new StringBuilder();
            pmaVhost.AppendLine($"# Auto-generated by DraGon for phpMyAdmin ({pmaDomain})");
            pmaVhost.AppendLine($"<VirtualHost *:{settings.ApachePort}>");
            pmaVhost.AppendLine($"    ServerName {pmaDomain}");
            if (!suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase))
            {
                pmaVhost.AppendLine("    ServerAlias phpmyadmin.dra");
            }
            pmaVhost.AppendLine($"    DocumentRoot \"{pmaRoot}\"");
            pmaVhost.AppendLine("    DirectoryIndex index.php index.html");
            pmaVhost.AppendLine($"    <Directory \"{pmaRoot}\">");
            pmaVhost.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
            pmaVhost.AppendLine("        AllowOverride All");
            pmaVhost.AppendLine("        Require all granted");
            pmaVhost.AppendLine("    </Directory>");
            pmaVhost.AppendLine("    <FilesMatch \"\\.php$\">");
            pmaVhost.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
            pmaVhost.AppendLine("    </FilesMatch>");
            pmaVhost.AppendLine("</VirtualHost>");

            if (useSsl)
            {
                hasAnySslVhost = true;
                pmaVhost.AppendLine();
                pmaVhost.AppendLine($"<VirtualHost *:{settings.ApacheHttpsPort}>");
                pmaVhost.AppendLine($"    ServerName {pmaDomain}");
                if (!suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase))
                {
                    pmaVhost.AppendLine("    ServerAlias phpmyadmin.dra");
                }
                pmaVhost.AppendLine($"    DocumentRoot \"{pmaRoot}\"");
                pmaVhost.AppendLine("    DirectoryIndex index.php index.html");
                pmaVhost.AppendLine("    SSLEngine on");
                pmaVhost.AppendLine($"    SSLCertificateFile \"{NormalizeSlash(certPaths.CertificatePemPath)}\"");
                pmaVhost.AppendLine($"    SSLCertificateKeyFile \"{NormalizeSlash(certPaths.PrivateKeyPemPath)}\"");
                pmaVhost.AppendLine($"    <Directory \"{pmaRoot}\">");
                pmaVhost.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
                pmaVhost.AppendLine("        AllowOverride All");
                pmaVhost.AppendLine("        Require all granted");
                pmaVhost.AppendLine("    </Directory>");
                pmaVhost.AppendLine("    <FilesMatch \"\\.php$\">");
                pmaVhost.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
                pmaVhost.AppendLine("    </FilesMatch>");
                pmaVhost.AppendLine("</VirtualHost>");
            }

            aggregateBuilder.AppendLine(pmaVhost.ToString());
        }

        File.WriteAllText(ApacheVhostsIncludePath, aggregateBuilder.ToString(), new UTF8Encoding(false));

        // Generate self-contained Apache httpd.conf managed by DraGon
        var srvRoot = NormalizeSlash(
            !string.IsNullOrWhiteSpace(apacheRuntimeDir)
                ? apacheRuntimeDir
                : Path.Combine(
                    string.IsNullOrWhiteSpace(settings.RuntimeBasePath) ? DraGonPaths.DefaultRuntimesDirectory : settings.RuntimeBasePath,
                    "apache")).TrimEnd('/');

        var defaultWorkspace = NormalizeSlash(settings.WorkspacePath).TrimEnd('/');
        Directory.CreateDirectory(settings.WorkspacePath);

        var errorLog = NormalizeSlash(Path.Combine(DraGonPaths.LogsDirectory, "apache-error.log"));
        var accessLog = NormalizeSlash(Path.Combine(DraGonPaths.LogsDirectory, "apache-access.log"));
        var pidPath = NormalizeSlash(Path.Combine(ApacheGeneratedRoot, "logs", "httpd.pid"));
        var vhostsInclude = NormalizeSlash(ApacheVhostsIncludePath);

        SslCertificatePaths? defaultSslCert = null;
        if (settings.EnableLocalHttps)
        {
            try
            {
                var defaultCertDomain = _sslService.HasDomainCertificate($"localhost{suffix}") ? $"localhost{suffix}" : "localhost.dra";
                defaultSslCert = _sslService.EnsureDomainCertificate(defaultCertDomain);
                hasAnySslVhost = true;
            }
            catch
            {
                // Ignore if default cert cannot be created
            }
        }

        var httpdBuilder = new StringBuilder();
        httpdBuilder.AppendLine("# Konfigurasi Utama Apache 2.4 Lokal yang Dikelola DraGon");
        httpdBuilder.AppendLine($"Define SRVROOT \"{srvRoot}\"");
        httpdBuilder.AppendLine("ServerRoot \"${SRVROOT}\"");
        httpdBuilder.AppendLine($"PidFile \"{pidPath}\"");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine($"Listen {settings.ApachePort}");
        if (hasAnySslVhost)
        {
            httpdBuilder.AppendLine($"Listen {settings.ApacheHttpsPort}");
        }
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("# Modul Inti Apache 2.4 untuk PHP FastCGI, Laravel (.htaccess), dan SSL");
        httpdBuilder.AppendLine("LoadModule access_compat_module modules/mod_access_compat.so");
        httpdBuilder.AppendLine("LoadModule actions_module modules/mod_actions.so");
        httpdBuilder.AppendLine("LoadModule alias_module modules/mod_alias.so");
        httpdBuilder.AppendLine("LoadModule allowmethods_module modules/mod_allowmethods.so");
        httpdBuilder.AppendLine("LoadModule asis_module modules/mod_asis.so");
        httpdBuilder.AppendLine("LoadModule auth_basic_module modules/mod_auth_basic.so");
        httpdBuilder.AppendLine("LoadModule authn_core_module modules/mod_authn_core.so");
        httpdBuilder.AppendLine("LoadModule authn_file_module modules/mod_authn_file.so");
        httpdBuilder.AppendLine("LoadModule authz_core_module modules/mod_authz_core.so");
        httpdBuilder.AppendLine("LoadModule authz_groupfile_module modules/mod_authz_groupfile.so");
        httpdBuilder.AppendLine("LoadModule authz_host_module modules/mod_authz_host.so");
        httpdBuilder.AppendLine("LoadModule authz_user_module modules/mod_authz_user.so");
        httpdBuilder.AppendLine("LoadModule autoindex_module modules/mod_autoindex.so");
        httpdBuilder.AppendLine("LoadModule cgi_module modules/mod_cgi.so");
        httpdBuilder.AppendLine("LoadModule deflate_module modules/mod_deflate.so");
        httpdBuilder.AppendLine("LoadModule dir_module modules/mod_dir.so");
        httpdBuilder.AppendLine("LoadModule env_module modules/mod_env.so");
        httpdBuilder.AppendLine("LoadModule expires_module modules/mod_expires.so");
        httpdBuilder.AppendLine("LoadModule filter_module modules/mod_filter.so");
        httpdBuilder.AppendLine("LoadModule headers_module modules/mod_headers.so");
        httpdBuilder.AppendLine("LoadModule include_module modules/mod_include.so");
        httpdBuilder.AppendLine("LoadModule isapi_module modules/mod_isapi.so");
        httpdBuilder.AppendLine("LoadModule log_config_module modules/mod_log_config.so");
        httpdBuilder.AppendLine("LoadModule mime_module modules/mod_mime.so");
        httpdBuilder.AppendLine("LoadModule negotiation_module modules/mod_negotiation.so");
        httpdBuilder.AppendLine("LoadModule proxy_module modules/mod_proxy.so");
        httpdBuilder.AppendLine("LoadModule proxy_fcgi_module modules/mod_proxy_fcgi.so");
        httpdBuilder.AppendLine("LoadModule proxy_http_module modules/mod_proxy_http.so");
        httpdBuilder.AppendLine("LoadModule reqtimeout_module modules/mod_reqtimeout.so");
        httpdBuilder.AppendLine("LoadModule rewrite_module modules/mod_rewrite.so");
        httpdBuilder.AppendLine("LoadModule setenvif_module modules/mod_setenvif.so");
        httpdBuilder.AppendLine("LoadModule socache_shmcb_module modules/mod_socache_shmcb.so");
        httpdBuilder.AppendLine("LoadModule ssl_module modules/mod_ssl.so");
        httpdBuilder.AppendLine("LoadModule version_module modules/mod_version.so");
        httpdBuilder.AppendLine("LoadModule vhost_alias_module modules/mod_vhost_alias.so");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("ServerAdmin admin@localhost");
        httpdBuilder.AppendLine($"ServerName localhost:{settings.ApachePort}");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("<Directory />");
        httpdBuilder.AppendLine("    AllowOverride none");
        httpdBuilder.AppendLine("    Require all denied");
        httpdBuilder.AppendLine("</Directory>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine($"DocumentRoot \"{defaultWorkspace}\"");
        httpdBuilder.AppendLine($"<Directory \"{defaultWorkspace}\">");
        httpdBuilder.AppendLine("    Options Indexes FollowSymLinks Includes ExecCGI");
        httpdBuilder.AppendLine("    AllowOverride All");
        httpdBuilder.AppendLine("    Require all granted");
        httpdBuilder.AppendLine("</Directory>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("<IfModule dir_module>");
        httpdBuilder.AppendLine("    DirectoryIndex index.php index.html index.htm");
        httpdBuilder.AppendLine("</IfModule>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("<Files \".ht*\">");
        httpdBuilder.AppendLine("    Require all denied");
        httpdBuilder.AppendLine("</Files>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine($"ErrorLog \"{errorLog}\"");
        httpdBuilder.AppendLine("LogLevel warn");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("<IfModule log_config_module>");
        httpdBuilder.AppendLine("    LogFormat \"%h %l %u %t \\\"%r\\\" %>s %b\" common");
        httpdBuilder.AppendLine($"    CustomLog \"{accessLog}\" common");
        httpdBuilder.AppendLine("</IfModule>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("<IfModule headers_module>");
        httpdBuilder.AppendLine("    RequestHeader unset Proxy early");
        httpdBuilder.AppendLine("</IfModule>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("<IfModule mime_module>");
        httpdBuilder.AppendLine("    TypesConfig conf/mime.types");
        httpdBuilder.AppendLine("    AddType application/x-compress .Z");
        httpdBuilder.AppendLine("    AddType application/x-gzip .gz .tgz");
        httpdBuilder.AppendLine("</IfModule>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("# Integrasi PHP FastCGI (php-cgi.exe) untuk Windows");
        httpdBuilder.AppendLine($"<Proxy \"fcgi://127.0.0.1:{settings.PhpCgiPort}/\" enablereuse=off max=10>");
        httpdBuilder.AppendLine("    ProxySet timeout=300");
        httpdBuilder.AppendLine("</Proxy>");
        httpdBuilder.AppendLine("ProxyFCGISetEnvIf \"reqenv('SCRIPT_FILENAME') =~ m#^proxy:fcgi://[^/]+/(.*)$#\" SCRIPT_FILENAME \"$1\"");
        httpdBuilder.AppendLine("<FilesMatch \"\\.php$\">");
        httpdBuilder.AppendLine($"    SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
        httpdBuilder.AppendLine("</FilesMatch>");
        httpdBuilder.AppendLine();

        // Global Alias for PHP Info & phpMyAdmin so they work from localhost, 127.0.0.1, or any .dra domain
        var webToolsNorm = NormalizeSlash(DraGonPaths.WebToolsDirectory).TrimEnd('/');
        var phpInfoNorm = NormalizeSlash(DraGonPaths.PhpInfoFilePath);
        httpdBuilder.AppendLine("# Alias Global untuk PHP Info & phpMyAdmin");
        httpdBuilder.AppendLine($"Alias /phpinfo.php \"{phpInfoNorm}\"");
        httpdBuilder.AppendLine($"Alias /phpinfo \"{phpInfoNorm}\"");
        httpdBuilder.AppendLine($"<Directory \"{webToolsNorm}\">");
        httpdBuilder.AppendLine("    Options Indexes FollowSymLinks Includes ExecCGI");
        httpdBuilder.AppendLine("    AllowOverride All");
        httpdBuilder.AppendLine("    Require all granted");
        httpdBuilder.AppendLine("</Directory>");

        if (!string.IsNullOrWhiteSpace(phpMyAdminDir))
        {
            var pmaNorm = NormalizeSlash(phpMyAdminDir).TrimEnd('/');
            httpdBuilder.AppendLine();
            httpdBuilder.AppendLine($"Alias /phpmyadmin \"{pmaNorm}\"");
            httpdBuilder.AppendLine($"<Directory \"{pmaNorm}\">");
            httpdBuilder.AppendLine("    Options Indexes FollowSymLinks Includes ExecCGI");
            httpdBuilder.AppendLine("    AllowOverride All");
            httpdBuilder.AppendLine("    DirectoryIndex index.php index.html");
            httpdBuilder.AppendLine("    Require all granted");
            httpdBuilder.AppendLine("</Directory>");
        }

        var apacheLocalhostAlias = suffix.Equals(".dra", StringComparison.OrdinalIgnoreCase)
            ? "127.0.0.1 ::1 localhost.dra"
            : $"127.0.0.1 ::1 localhost.dra localhost{suffix}";

        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("# Default VirtualHost untuk localhost / 127.0.0.1");
        httpdBuilder.AppendLine($"<VirtualHost *:{settings.ApachePort}>");
        httpdBuilder.AppendLine("    ServerName localhost");
        httpdBuilder.AppendLine($"    ServerAlias {apacheLocalhostAlias}");
        httpdBuilder.AppendLine($"    DocumentRoot \"{defaultWorkspace}\"");
        httpdBuilder.AppendLine("    DirectoryIndex index.php index.html index.htm");
        httpdBuilder.AppendLine($"    <Directory \"{defaultWorkspace}\">");
        httpdBuilder.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
        httpdBuilder.AppendLine("        AllowOverride All");
        httpdBuilder.AppendLine("        Require all granted");
        httpdBuilder.AppendLine("    </Directory>");
        httpdBuilder.AppendLine("    <FilesMatch \"\\.php$\">");
        httpdBuilder.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
        httpdBuilder.AppendLine("    </FilesMatch>");
        httpdBuilder.AppendLine("</VirtualHost>");
        if (defaultSslCert is not null)
        {
            httpdBuilder.AppendLine();
            httpdBuilder.AppendLine($"<VirtualHost *:{settings.ApacheHttpsPort}>");
            httpdBuilder.AppendLine("    ServerName localhost");
            httpdBuilder.AppendLine($"    ServerAlias {apacheLocalhostAlias}");
            httpdBuilder.AppendLine($"    DocumentRoot \"{defaultWorkspace}\"");
            httpdBuilder.AppendLine("    DirectoryIndex index.php index.html index.htm");
            httpdBuilder.AppendLine("    SSLEngine on");
            httpdBuilder.AppendLine($"    SSLCertificateFile \"{NormalizeSlash(defaultSslCert.CertificatePemPath)}\"");
            httpdBuilder.AppendLine($"    SSLCertificateKeyFile \"{NormalizeSlash(defaultSslCert.PrivateKeyPemPath)}\"");
            httpdBuilder.AppendLine($"    <Directory \"{defaultWorkspace}\">");
            httpdBuilder.AppendLine("        Options Indexes FollowSymLinks Includes ExecCGI");
            httpdBuilder.AppendLine("        AllowOverride All");
            httpdBuilder.AppendLine("        Require all granted");
            httpdBuilder.AppendLine("    </Directory>");
            httpdBuilder.AppendLine("    <FilesMatch \"\\.php$\">");
            httpdBuilder.AppendLine($"        SetHandler \"proxy:fcgi://127.0.0.1:{settings.PhpCgiPort}/\"");
            httpdBuilder.AppendLine("    </FilesMatch>");
            httpdBuilder.AppendLine("</VirtualHost>");
        }
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine("<IfModule ssl_module>");
        httpdBuilder.AppendLine("    SSLRandomSeed startup builtin");
        httpdBuilder.AppendLine("    SSLRandomSeed connect builtin");
        httpdBuilder.AppendLine("</IfModule>");
        httpdBuilder.AppendLine();
        httpdBuilder.AppendLine($"Include \"{vhostsInclude}\"");

        File.WriteAllText(ApacheMainConfigPath, httpdBuilder.ToString(), new UTF8Encoding(false));
    }

    private static string NormalizeSlash(string path) => path.Replace('\\', '/');
}
