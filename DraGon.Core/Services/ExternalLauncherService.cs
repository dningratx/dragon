using System.Diagnostics;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public enum ProjectQuickCommand
{
    ArtisanServe,
    ComposerInstall,
    NpmInstall
}

public sealed class ExternalLauncherService
{
    private readonly AppLogger _logger;

    public ExternalLauncherService(AppLogger logger)
    {
        _logger = logger;
    }

    public (bool Success, string Message) OpenFolder(string folderPath)
    {
        var validation = ValidationService.ValidateDirectoryPath(folderPath, "Path folder", mustExist: true);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(fullPath);
            Process.Start(psi);
            return (true, $"Membuka folder: {fullPath}");
        }
        catch (Exception ex)
        {
            _logger.Error("Launcher", $"Gagal membuka folder {folderPath}.", ex);
            return (false, $"Gagal membuka folder: {ex.Message}");
        }
    }

    public (bool Success, string Message) OpenFileInDefaultViewer(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return (false, $"File belum tersedia: {filePath}");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "notepad.exe",
                UseShellExecute = false
            };
            psi.ArgumentList.Add(Path.GetFullPath(filePath));
            Process.Start(psi);
            return (true, $"Membuka file di Notepad: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _logger.Error("Launcher", $"Gagal membuka file {filePath}.", ex);
            return (false, $"Gagal membuka file: {ex.Message}");
        }
    }

    public (bool Success, string Message) OpenTerminalInFolder(string folderPath, AppSettings settings)
    {
        var validation = ValidationService.ValidateDirectoryPath(folderPath, "Path folder proyek", mustExist: false);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            var psExe = ResolvePowerShellPath();
            var psi = new ProcessStartInfo
            {
                FileName = psExe,
                WorkingDirectory = fullPath,
                UseShellExecute = false
            };

            InjectRuntimePathsToEnvironment(psi, settings.RuntimeBasePath);
            psi.ArgumentList.Add("-NoExit");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add("Write-Host '=== Terminal Proyek DraGon ===' -ForegroundColor Cyan; Write-Host ('Lokasi: ' + (Get-Location)) -ForegroundColor Gray");

            Process.Start(psi);
            _logger.Info("Terminal", $"Membuka terminal PowerShell pada: {fullPath}");
            return (true, $"Terminal dibuka pada folder '{Path.GetFileName(fullPath)}'.");
        }
        catch (Exception ex)
        {
            _logger.Error("Terminal", $"Gagal membuka terminal pada {folderPath}.", ex);
            try
            {
                var fullPath = Path.GetFullPath(folderPath);
                var cmdPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = fullPath,
                    UseShellExecute = false
                };
                InjectRuntimePathsToEnvironment(cmdPsi, settings.RuntimeBasePath);
                cmdPsi.ArgumentList.Add("/K");
                cmdPsi.ArgumentList.Add("echo === Terminal DraGon (cmd.exe) === & cd");
                Process.Start(cmdPsi);
                return (true, $"Terminal CMD dibuka pada folder '{Path.GetFileName(fullPath)}'.");
            }
            catch
            {
                return (false, $"Gagal membuka terminal: {ex.Message}");
            }
        }
    }

    public (bool Success, string Message) OpenTerminalWithCommand(string folderPath, string initialCommand, AppSettings settings)
    {
        var validation = ValidationService.ValidateDirectoryPath(folderPath, "Path folder proyek", mustExist: false);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            var psExe = ResolvePowerShellPath();
            var psi = new ProcessStartInfo
            {
                FileName = psExe,
                WorkingDirectory = fullPath,
                UseShellExecute = false
            };

            InjectRuntimePathsToEnvironment(psi, settings.RuntimeBasePath);
            psi.ArgumentList.Add("-NoExit");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add($"Write-Host '=== Terminal DraGon ===' -ForegroundColor Cyan; Write-Host ('Perintah: ' + '{initialCommand.Replace("'", "''")}') -ForegroundColor Yellow; {initialCommand}");

            Process.Start(psi);
            _logger.Info("Terminal", $"Membuka terminal dengan perintah '{initialCommand}' pada: {fullPath}");
            return (true, $"Terminal dibuka untuk menjalankan '{initialCommand}'.");
        }
        catch (Exception ex)
        {
            _logger.Error("Terminal", $"Gagal membuka terminal dengan perintah pada {folderPath}.", ex);
            try
            {
                var fullPath = Path.GetFullPath(folderPath);
                var cmdPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = fullPath,
                    UseShellExecute = false
                };
                InjectRuntimePathsToEnvironment(cmdPsi, settings.RuntimeBasePath);
                cmdPsi.ArgumentList.Add("/K");
                cmdPsi.ArgumentList.Add(initialCommand);
                Process.Start(cmdPsi);
                return (true, $"Terminal CMD dibuka untuk menjalankan '{initialCommand}'.");
            }
            catch
            {
                return (false, $"Gagal membuka terminal: {ex.Message}");
            }
        }
    }

    public (bool Success, string Message) OpenProjectInBrowser(ProjectItem project, AppSettings settings)
    {
        var domainValidation = ValidationService.ValidateDomain(project.Domain);
        if (!domainValidation.IsValid)
        {
            return (false, domainValidation.ErrorMessage);
        }

        var domain = project.Domain.Trim().ToLowerInvariant();
        if (settings.EnableLocalHttps && project.HttpsEnabled && project.HasSslCertificate)
        {
            var httpsPort = settings.PreferredWebServer == WebServerType.Apache
                ? settings.ApacheHttpsPort
                : settings.WebServerHttpsPort;

            var httpsUrl = httpsPort == 443
                ? $"https://{domain}/"
                : $"https://{domain}:{httpsPort}/";

            return OpenUrl(httpsUrl, settings);
        }

        var port = settings.PreferredWebServer == WebServerType.Apache
            ? settings.ApachePort
            : settings.WebServerPort;

        var url = port == 80
            ? $"http://{domain}/"
            : $"http://{domain}:{port}/";

        return OpenUrl(url, settings);
    }

    public (bool Success, string Message) OpenUrl(string url, AppSettings settings)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, "Alamat URL tidak valid.");
        }

        try
        {
            var browserExe = ResolveBrowserExecutable(settings);
            if (!string.IsNullOrWhiteSpace(browserExe))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = browserExe,
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(parsedUri.AbsoluteUri);
                Process.Start(psi);
                _logger.Info("Browser", $"Membuka {parsedUri.AbsoluteUri} menggunakan {Path.GetFileName(browserExe)}.");
                return (true, $"Membuka {parsedUri.AbsoluteUri} di browser.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = parsedUri.AbsoluteUri,
                UseShellExecute = true
            });

            _logger.Info("Browser", $"Membuka {parsedUri.AbsoluteUri} di browser bawaan sistem.");
            return (true, $"Membuka {parsedUri.AbsoluteUri} di browser bawaan.");
        }
        catch (Exception ex)
        {
            _logger.Error("Browser", $"Gagal membuka URL {url}.", ex);
            return (false, $"Gagal membuka browser: {ex.Message}");
        }
    }

    public (bool Success, string Message) LaunchHeidiSql(string runtimeBasePath)
    {
        var exePath = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
            Path.Combine(runtimeBasePath, "heidisql"), "heidisql.exe")
            ?? RuntimePackageCatalogService.FindInSystemPath("heidisql.exe")
            ?? (File.Exists(@"C:\Program Files\HeidiSQL\heidisql.exe") ? @"C:\Program Files\HeidiSQL\heidisql.exe" : null);

        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return (false, "Aplikasi HeidiSQL belum terpasang. Anda dapat mengunduh dan memasangnya melalui menu 'Packages & Installer'.");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? runtimeBasePath,
                UseShellExecute = false
            };
            Process.Start(psi);
            _logger.Info("HeidiSQL", $"Membuka HeidiSQL dari {exePath}");
            return (true, "Membuka HeidiSQL.");
        }
        catch (Exception ex)
        {
            _logger.Error("HeidiSQL", "Gagal membuka HeidiSQL.", ex);
            return (false, $"Gagal membuka HeidiSQL: {ex.Message}");
        }
    }

    public (bool Success, string Message) RunProjectQuickCommandInTerminal(
        ProjectItem project,
        ProjectQuickCommand command,
        AppSettings settings)
    {
        var dirCheck = ValidationService.ValidateDirectoryPath(project.FolderPath, "Folder proyek", mustExist: true);
        if (!dirCheck.IsValid)
        {
            return (false, dirCheck.ErrorMessage);
        }

        var workingDir = Path.GetFullPath(project.FolderPath);

        string exeToResolve;
        string[] fixedArguments;
        string displayCommand;

        switch (command)
        {
            case ProjectQuickCommand.ArtisanServe:
                if (!File.Exists(Path.Combine(workingDir, "artisan")))
                {
                    return (false, "File 'artisan' tidak ditemukan di folder proyek ini.");
                }
                exeToResolve = ResolveToolExecutable("php", "php.exe", settings.RuntimeBasePath) ?? "php";
                fixedArguments = ["artisan", "serve"];
                displayCommand = "php artisan serve";
                break;

            case ProjectQuickCommand.ComposerInstall:
                if (!File.Exists(Path.Combine(workingDir, "composer.json")))
                {
                    return (false, "File 'composer.json' tidak ditemukan di folder proyek ini.");
                }
                exeToResolve = ResolveToolExecutable("composer", "composer.bat", settings.RuntimeBasePath) ?? "composer";
                fixedArguments = ["install"];
                displayCommand = "composer install";
                break;

            case ProjectQuickCommand.NpmInstall:
                if (!File.Exists(Path.Combine(workingDir, "package.json")))
                {
                    return (false, "File 'package.json' tidak ditemukan di folder proyek ini.");
                }
                exeToResolve = ResolveToolExecutable("nodejs", "npm.cmd", settings.RuntimeBasePath) ?? "npm";
                fixedArguments = ["install"];
                displayCommand = "npm install";
                break;

            default:
                return (false, "Perintah tidak dikenali.");
        }

        // Verify tool availability before launching so user gets a clear message if PHP/Composer/NPM is missing
        if (!Path.IsPathRooted(exeToResolve) && RuntimePackageCatalogService.FindInSystemPath(exeToResolve + ".exe") is null &&
            RuntimePackageCatalogService.FindInSystemPath(exeToResolve + ".bat") is null &&
            RuntimePackageCatalogService.FindInSystemPath(exeToResolve + ".cmd") is null &&
            RuntimePackageCatalogService.FindInSystemPath(exeToResolve) is null)
        {
            var msg = $"Perintah '{displayCommand}' tidak dapat dijalankan karena runtime '{exeToResolve}' belum ditemukan di folder runtime DraGon maupun PATH Windows. Buka menu Packages untuk petunjuk pemasangan.";
            _logger.Warn("QuickCommand", msg);
            return (false, msg);
        }

        try
        {
            // Launch cmd.exe safely using ArgumentList with fixed predefined arguments (no user string concatenation)
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = workingDir,
                UseShellExecute = false
            };

            InjectRuntimePathsToEnvironment(psi, settings.RuntimeBasePath);
            psi.ArgumentList.Add("/K");
            psi.ArgumentList.Add(exeToResolve);
            foreach (var arg in fixedArguments)
            {
                psi.ArgumentList.Add(arg);
            }

            Process.Start(psi);
            _logger.Info("QuickCommand", $"Menjalankan pintasan '{displayCommand}' pada proyek '{project.Name}' ({workingDir}).");
            return (true, $"Menjalankan '{displayCommand}' di terminal untuk proyek '{project.Name}'.");
        }
        catch (Exception ex)
        {
            _logger.Error("QuickCommand", $"Gagal menjalankan '{displayCommand}'.", ex);
            return (false, $"Gagal menjalankan perintah '{displayCommand}': {ex.Message}");
        }
    }

    private static string? ResolveToolExecutable(string packageSubfolder, string exeName, string runtimeBasePath)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var inRuntime = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
            Path.Combine(baseDir, packageSubfolder),
            exeName);

        if (!string.IsNullOrWhiteSpace(inRuntime))
        {
            return inRuntime;
        }

        return RuntimePackageCatalogService.FindInSystemPath(exeName);
    }

    public static string ResolvePowerShellPath()
    {
        var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var stdPs = Path.Combine(sysDir, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(stdPs))
        {
            return stdPs;
        }

        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var sysNativePs = Path.Combine(winDir, "Sysnative", "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(sysNativePs))
        {
            return sysNativePs;
        }

        var pwshCandidate = RuntimePackageCatalogService.FindInSystemPath("pwsh.exe");
        if (!string.IsNullOrWhiteSpace(pwshCandidate) && File.Exists(pwshCandidate))
        {
            return pwshCandidate;
        }

        var inPath = RuntimePackageCatalogService.FindInSystemPath("powershell.exe");
        if (!string.IsNullOrWhiteSpace(inPath) && File.Exists(inPath))
        {
            return inPath;
        }

        return "powershell.exe";
    }

    private static void InjectRuntimePathsToEnvironment(ProcessStartInfo psi, string runtimeBasePath)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var extraDirs = new List<string>();

        void AddCandidates(string packageSubfolder, params string[] relativeSubPaths)
        {
            var root = Path.Combine(baseDir, packageSubfolder);
            if (!Directory.Exists(root)) return;

            foreach (var sub in relativeSubPaths)
            {
                var candidate = string.IsNullOrEmpty(sub) ? root : Path.Combine(root, sub);
                if (Directory.Exists(candidate)) extraDirs.Add(candidate);
            }

            try
            {
                foreach (var versionDir in Directory.GetDirectories(root))
                {
                    foreach (var sub in relativeSubPaths)
                    {
                        var candidate = string.IsNullOrEmpty(sub) ? versionDir : Path.Combine(versionDir, sub);
                        if (Directory.Exists(candidate)) extraDirs.Add(candidate);
                    }
                }
            }
            catch { }
        }

        AddCandidates("php", "");
        AddCandidates("composer", "");
        AddCandidates("nodejs", "");
        AddCandidates("mariadb", "bin", "");
        AddCandidates("mysql", "bin", "");
        AddCandidates("postgresql", "bin", "");
        AddCandidates("git", "cmd", "bin", "");
        AddCandidates("nginx", "");
        AddCandidates("apache", "bin", "");
        AddCandidates("python", "Scripts", "");
        AddCandidates("heidisql", "");

        var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var psDir = Path.Combine(sys32, "WindowsPowerShell", "v1.0");
        if (Directory.Exists(sys32) && !extraDirs.Contains(sys32, StringComparer.OrdinalIgnoreCase))
            extraDirs.Add(sys32);
        if (Directory.Exists(psDir) && !extraDirs.Contains(psDir, StringComparer.OrdinalIgnoreCase))
            extraDirs.Add(psDir);

        if (extraDirs.Count == 0)
        {
            return;
        }

        var distinctDirs = extraDirs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        psi.Environment["PATH"] = string.Join(Path.PathSeparator, distinctDirs) + Path.PathSeparator + currentPath;
    }

    private static string? ResolveBrowserExecutable(AppSettings settings)
    {
        if (string.Equals(settings.PreferredBrowser, "Custom", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.CustomBrowserPath) &&
            File.Exists(settings.CustomBrowserPath))
        {
            return settings.CustomBrowserPath;
        }

        string[] candidates = settings.PreferredBrowser switch
        {
            "Chrome" =>
            [
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            ],
            "Edge" =>
            [
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            ],
            "Firefox" =>
            [
                @"C:\Program Files\Mozilla Firefox\firefox.exe",
                @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
            ],
            _ => []
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
