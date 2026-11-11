using System.Diagnostics;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed class ServiceProcessManager : IDisposable
{
    private readonly AppLogger _logger;
    private readonly ServerConfigGenerator _configGenerator;
    private readonly PhpVersionManagerService _phpVersionManager;
    private readonly Dictionary<ServiceKind, Process> _runningProcesses = [];
    private readonly Lock _processLock = new();
    private Action<Action>? _uiThreadDispatcher;

    public IReadOnlyList<ServiceState> Services { get; }

    public ServiceProcessManager(
        AppLogger logger,
        ServerConfigGenerator configGenerator,
        PhpVersionManagerService? phpVersionManager = null)
    {
        _logger = logger;
        _configGenerator = configGenerator;
        _phpVersionManager = phpVersionManager ?? new PhpVersionManagerService(logger);

        Services =
        [
            new ServiceState
            {
                Kind = ServiceKind.Apache,
                DisplayName = "Apache HTTP Server",
                Category = "Web Server Utama (Default)",
                Description = "Server HTTP/HTTPS utama bawaan (seperti Laragon) dengan dukungan .htaccess dan routing domain .dra.",
                ConfiguredPort = 80,
                LogFilePath = Path.Combine(DraGonPaths.LogsDirectory, "service-apache.log"),
                InstallationGuide = "Ekstrak distribusi resmi Win64 Apache 2.4 ke folder runtime 'apache' (sehingga terdapat bin\\httpd.exe)."
            },
            new ServiceState
            {
                Kind = ServiceKind.PhpCgi,
                DisplayName = "PHP FastCGI (php-cgi)",
                Category = "Pemroses Skrip PHP",
                Description = "Menjalankan skrip PHP dan proyek Laravel melalui FastCGI lokal.",
                ConfiguredPort = 9000,
                LogFilePath = Path.Combine(DraGonPaths.LogsDirectory, "service-phpcgi.log"),
                InstallationGuide = "Ekstrak paket ZIP resmi PHP untuk Windows dari https://windows.php.net/download/ ke folder runtime 'php' (sehingga terdapat php-cgi.exe)."
            },
            new ServiceState
            {
                Kind = ServiceKind.MariaDb,
                DisplayName = "MariaDB / MySQL Database",
                Category = "Server Database",
                Description = "Layanan database SQL lokal untuk penyimpanan data aplikasi.",
                ConfiguredPort = 3306,
                LogFilePath = Path.Combine(DraGonPaths.LogsDirectory, "service-mariadb.log"),
                InstallationGuide = "Ekstrak paket ZIP resmi MariaDB Windows x64 dari https://mariadb.org/download/ ke folder runtime 'mariadb' (sehingga terdapat bin\\mysqld.exe)."
            },
            new ServiceState
            {
                Kind = ServiceKind.PostgreSql,
                DisplayName = "PostgreSQL Database",
                Category = "Server Database (Relasional)",
                Description = "Layanan database PostgreSQL lokal (port 5432) untuk aplikasi modern.",
                ConfiguredPort = 5432,
                LogFilePath = Path.Combine(DraGonPaths.LogsDirectory, "service-postgresql.log"),
                InstallationGuide = "Ekstrak paket resmi PostgreSQL binaries ke folder runtime 'postgresql' (sehingga terdapat bin\\postgres.exe)."
            },
            new ServiceState
            {
                Kind = ServiceKind.Nginx,
                DisplayName = "Nginx Web Server",
                Category = "Layanan Opsional (On-Demand)",
                Description = "Layanan web server tambahan yang dapat diaktifkan sewaktu-waktu sesuai kebutuhan.",
                ConfiguredPort = 8080,
                LogFilePath = Path.Combine(DraGonPaths.LogsDirectory, "service-nginx.log"),
                InstallationGuide = "Ekstrak paket resmi nginx/Windows dari https://nginx.org/en/download.html ke folder runtime 'nginx' (sehingga terdapat nginx.exe)."
            }
        ];
    }

    public void SetUiDispatcher(Action<Action> dispatcher)
    {
        _uiThreadDispatcher = dispatcher;
    }

    public bool HasRunningServices
    {
        get
        {
            lock (_processLock)
            {
                return _runningProcesses.Values.Any(p =>
                {
                    try
                    {
                        return !p.HasExited;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
        }
    }

    public void RefreshServiceStates(AppSettings settings)
    {
        var activePorts = PortUtility.GetActiveListeningPorts();

        foreach (var service in Services)
        {
            var configuredPort = service.Kind switch
            {
                ServiceKind.Nginx => settings.WebServerPort,
                ServiceKind.Apache => settings.ApachePort,
                ServiceKind.PhpCgi => settings.PhpCgiPort,
                ServiceKind.MariaDb => settings.DatabasePort,
                _ => service.ConfiguredPort
            };

            var exePath = ResolveServiceExecutable(service.Kind, settings);
            var isManagedRunning = IsManagedProcessRunning(service.Kind, out var pid);
            var portBusy = activePorts.Contains(configuredPort);
            var detectedVersion = DetectServiceVersion(service.Kind, exePath, settings);

            DispatchToUi(() =>
            {
                service.ConfiguredPort = configuredPort;
                service.ExecutablePath = exePath;
                service.ActiveVersion = detectedVersion;
                service.PortOccupiedByExternalProcess = portBusy && !isManagedRunning;

                if (isManagedRunning)
                {
                    service.ProcessId = pid;
                    service.Status = ServiceStatus.Running;
                    service.StatusMessage = $"Berjalan normal pada 127.0.0.1:{configuredPort} (PID {pid})";
                }
                else
                {
                    service.ProcessId = null;
                    if (string.IsNullOrWhiteSpace(exePath))
                    {
                        service.Status = ServiceStatus.MissingRuntime;
                        service.StatusMessage = "Executable runtime belum ditemukan di folder runtime DraGon.";
                    }
                    else if (portBusy && service.Status != ServiceStatus.Error)
                    {
                        service.Status = ServiceStatus.PortConflict;
                        service.StatusMessage = $"Port {configuredPort} sedang digunakan oleh proses lain di luar DraGon.";
                    }
                    else if (service.Status is ServiceStatus.MissingRuntime or ServiceStatus.Running or ServiceStatus.Starting or ServiceStatus.Stopping)
                    {
                        service.Status = ServiceStatus.Stopped;
                        service.StatusMessage = $"Siap dijalankan pada port {configuredPort}.";
                    }
                }
            });
        }
    }

    public async Task<(bool Success, string Message)> StartServiceAsync(
        ServiceKind kind,
        AppSettings settings,
        IEnumerable<ProjectItem> projects)
    {
        var state = Services.First(s => s.Kind == kind);

        if (IsManagedProcessRunning(kind, out var existingPid))
        {
            return (true, $"{state.DisplayName} sudah berjalan (PID {existingPid}).");
        }

        var exePath = ResolveServiceExecutable(kind, settings);
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            DispatchToUi(() =>
            {
                state.ExecutablePath = null;
                state.Status = ServiceStatus.MissingRuntime;
                state.StatusMessage = "Gagal memulai: Executable runtime tidak ditemukan.";
            });

            var missingMsg = $"{state.DisplayName} belum dapat dijalankan karena file executable belum tersedia. {state.InstallationGuide}";
            _logger.Warn(state.DisplayName, missingMsg);
            return (false, missingMsg);
        }

        var port = kind switch
        {
            ServiceKind.Nginx => settings.WebServerPort,
            ServiceKind.Apache => settings.ApachePort,
            ServiceKind.PhpCgi => settings.PhpCgiPort,
            ServiceKind.MariaDb => settings.DatabasePort,
            _ => state.ConfiguredPort
        };

        if (PortUtility.IsTcpPortInUse(port))
        {
            var conflictMsg = $"Port {port} sudah digunakan oleh proses lain di sistem. Silakan hentikan proses tersebut atau ubah port di menu Settings.";
            DispatchToUi(() =>
            {
                state.Status = ServiceStatus.PortConflict;
                state.PortOccupiedByExternalProcess = true;
                state.StatusMessage = conflictMsg;
                AppendServiceLog(state, $"[ERROR] {conflictMsg}");
            });

            _logger.Warn(state.DisplayName, conflictMsg);
            return (false, conflictMsg);
        }

        var workingDir = ResolveWorkingDirectory(kind, exePath);
        _configGenerator.GenerateConfigs(
            settings,
            projects,
            kind == ServiceKind.Nginx ? workingDir : null,
            kind == ServiceKind.Apache ? workingDir : null);

        var startInfo = BuildSafeProcessStartInfo(kind, exePath, workingDir, settings, out var argsSummary, out var preflightError);
        if (startInfo is null)
        {
            DispatchToUi(() =>
            {
                state.Status = ServiceStatus.Error;
                state.StatusMessage = preflightError ?? "Konfigurasi runtime belum lengkap.";
                AppendServiceLog(state, $"[ERROR] {state.StatusMessage}");
            });
            _logger.Error(state.DisplayName, preflightError ?? "Konfigurasi runtime belum lengkap.");
            return (false, preflightError ?? "Konfigurasi runtime belum lengkap.");
        }

        DispatchToUi(() =>
        {
            state.Status = ServiceStatus.Starting;
            state.ExecutablePath = exePath;
            state.WorkingDirectory = workingDir;
            state.ArgumentsSummary = argsSummary;
            state.ConfiguredPort = port;
            state.StatusMessage = $"Memulai {state.DisplayName} pada port {port}...";
            AppendServiceLog(state, $"Memulai proses: \"{exePath}\" {argsSummary} (Dir: {workingDir})");
        });

        try
        {
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.Write("OUT", state.DisplayName, e.Data, state.LogFilePath);
                    DispatchToUi(() => AppendServiceLog(state, e.Data));
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.Write("ERR", state.DisplayName, e.Data, state.LogFilePath);
                    DispatchToUi(() => AppendServiceLog(state, $"[STDERR] {e.Data}"));
                }
            };

            process.Exited += (_, _) => OnManagedProcessExited(kind, process);

            if (!process.Start())
            {
                throw new InvalidOperationException("Sistem operasi gagal membuat proses layanan.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_processLock)
            {
                _runningProcesses[kind] = process;
            }

            // Wait briefly to verify the process did not immediately crash on startup
            await Task.Delay(450);

            if (process.HasExited)
            {
                var exitCode = process.ExitCode;
                lock (_processLock)
                {
                    _runningProcesses.Remove(kind);
                }

                var failMsg = $"{state.DisplayName} berhenti segera setelah dimulai (Exit Code: {exitCode}). Periksa log layanan untuk detail konfigurasi.";
                DispatchToUi(() =>
                {
                    state.ProcessId = null;
                    state.Status = ServiceStatus.Error;
                    state.StatusMessage = failMsg;
                    AppendServiceLog(state, $"[ERROR] {failMsg}");
                });

                _logger.Error(state.DisplayName, failMsg);
                return (false, failMsg);
            }

            var pid = process.Id;
            var now = DateTime.Now;
            var detectedVersion = DetectServiceVersion(kind, exePath, settings);
            DispatchToUi(() =>
            {
                state.ProcessId = pid;
                state.StartedAt = now;
                state.ActiveVersion = detectedVersion;
                state.Status = ServiceStatus.Running;
                state.PortOccupiedByExternalProcess = false;
                state.StatusMessage = $"Berjalan aktif pada 127.0.0.1:{port} (PID {pid})";
                AppendServiceLog(state, $"Layanan aktif dengan PID {pid} pada port {port}.");
            });

            _logger.Info(state.DisplayName, $"Berhasil dimulai (PID {pid}, Port {port}, Argumen: {argsSummary}).");
            return (true, $"{state.DisplayName} berhasil dijalankan pada port {port} (PID {pid}).");
        }
        catch (Exception ex)
        {
            DispatchToUi(() =>
            {
                state.ProcessId = null;
                state.Status = ServiceStatus.Error;
                state.StatusMessage = $"Gagal memulai proses: {ex.Message}";
                AppendServiceLog(state, $"[EXCEPTION] {ex.Message}");
            });

            _logger.Error(state.DisplayName, "Gagal menjalankan proses eksternal.", ex);
            return (false, $"Gagal memulai {state.DisplayName}: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> StopServiceAsync(ServiceKind kind)
    {
        var state = Services.First(s => s.Kind == kind);
        Process? proc;

        lock (_processLock)
        {
            _runningProcesses.TryGetValue(kind, out proc);
        }

        var procNames = GetProcessNamesForKind(kind);
        var osProcs = procNames.SelectMany(Process.GetProcessesByName).ToList();

        if (proc is null && osProcs.Count == 0)
        {
            DispatchToUi(() =>
            {
                if (state.Status != ServiceStatus.MissingRuntime)
                {
                    state.Status = ServiceStatus.Stopped;
                    state.ProcessId = null;
                    state.StatusMessage = "Berhenti.";
                }
            });
            return (true, $"{state.DisplayName} sudah dalam keadaan berhenti.");
        }

        DispatchToUi(() =>
        {
            state.Status = ServiceStatus.Stopping;
            state.StatusMessage = $"Menghentikan {state.DisplayName}...";
            AppendServiceLog(state, "Mengirim sinyal penghentian proses...");
        });

        try
        {
            await Task.Run(() =>
            {
                if (proc is not null)
                {
                    try
                    {
                        proc.CancelOutputRead();
                    }
                    catch
                    {
                        // Ignore if not reading
                    }

                    try
                    {
                        proc.CancelErrorRead();
                    }
                    catch
                    {
                        // Ignore if not reading
                    }

                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: true);
                            proc.WaitForExit(2000);
                        }
                    }
                    catch
                    {
                        // Ignore if already exited
                    }
                }

                foreach (var osProc in osProcs)
                {
                    try
                    {
                        if (!osProc.HasExited)
                        {
                            osProc.Kill(entireProcessTree: true);
                            osProc.WaitForExit(1500);
                        }
                    }
                    catch
                    {
                        // Ignore already exited
                    }
                    finally
                    {
                        osProc.Dispose();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Warn(state.DisplayName, $"Peringatan saat menghentikan proses: {ex.Message}");
        }
        finally
        {
            lock (_processLock)
            {
                _runningProcesses.Remove(kind);
            }
            proc?.Dispose();
        }

        DispatchToUi(() =>
        {
            state.ProcessId = null;
            state.StartedAt = null;
            state.Status = ServiceStatus.Stopped;
            state.StatusMessage = $"Dihentikan oleh pengguna.";
            AppendServiceLog(state, "Proses berhasil dihentikan.");
        });

        _logger.Info(state.DisplayName, "Layanan berhasil dihentikan.");
        return (true, $"{state.DisplayName} berhasil dihentikan.");
    }

    public async Task<(int StartedCount, int FailedCount, string Summary)> StartAllAsync(
        AppSettings settings,
        IEnumerable<ProjectItem> projects)
    {
        var primaryWebServer = settings.PreferredWebServer == WebServerType.Apache
            ? ServiceKind.Apache
            : ServiceKind.Nginx;

        var targetServices = new[]
        {
            primaryWebServer,
            ServiceKind.PhpCgi,
            ServiceKind.MariaDb
        };

        var started = 0;
        var failed = 0;
        var messages = new List<string>();
        var projectList = projects.ToList();

        foreach (var kind in targetServices)
        {
            var (success, msg) = await StartServiceAsync(kind, settings, projectList);
            if (success)
            {
                started++;
            }
            else
            {
                failed++;
                messages.Add(msg);
            }
        }

        if (started > 0 && failed == 0)
        {
            return (started, 0, $"Semua layanan utama ({started} layanan) berhasil dijalankan.");
        }

        if (started > 0)
        {
            return (started, failed, $"{started} layanan berjalan, {failed} layanan belum dapat dimulai. Periksa status runtime/port pada kartu layanan.");
        }

        return (0, failed, "Belum ada layanan yang dapat dimulai karena runtime belum terpasang atau terjadi konflik port. Buka tab Services atau Packages untuk petunjuk pemasangan.");
    }

    public async Task<string> StopAllAsync()
    {
        var activeKinds = new HashSet<ServiceKind>();
        lock (_processLock)
        {
            foreach (var k in _runningProcesses.Keys)
            {
                activeKinds.Add(k);
            }
        }

        foreach (var kind in Enum.GetValues<ServiceKind>())
        {
            if (IsManagedProcessRunning(kind, out _))
            {
                activeKinds.Add(kind);
            }
        }

        if (activeKinds.Count == 0)
        {
            RefreshStatesAfterStopAll();
            return "Tidak ada layanan yang sedang berjalan.";
        }

        foreach (var kind in activeKinds)
        {
            await StopServiceAsync(kind);
        }

        return $"Berhasil menghentikan {activeKinds.Count} layanan.";
    }

    private void RefreshStatesAfterStopAll()
    {
        foreach (var state in Services)
        {
            if (state.Status is ServiceStatus.Running or ServiceStatus.Starting or ServiceStatus.Stopping)
            {
                DispatchToUi(() =>
                {
                    state.ProcessId = null;
                    state.Status = ServiceStatus.Stopped;
                    state.StatusMessage = "Berhenti.";
                });
            }
        }
    }

    private ProcessStartInfo? BuildSafeProcessStartInfo(
        ServiceKind kind,
        string exePath,
        string workingDir,
        AppSettings settings,
        out string argsSummary,
        out string? preflightError)
    {
        preflightError = null;
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        switch (kind)
        {
            case ServiceKind.Nginx:
                psi.ArgumentList.Add("-p");
                psi.ArgumentList.Add(_configGenerator.NginxGeneratedRoot);
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(_configGenerator.NginxMainConfigPath);
                break;

            case ServiceKind.PhpCgi:
                var activePhp = _phpVersionManager.GetActivePhpVersion(settings);
                if (activePhp is not null)
                {
                    _phpVersionManager.EnsurePhpIniConfigured(activePhp);
                }
                psi.Environment["PHP_FCGI_MAX_REQUESTS"] = "0";
                psi.ArgumentList.Add("-b");
                psi.ArgumentList.Add($"127.0.0.1:{settings.PhpCgiPort}");
                var phpIni = Path.Combine(workingDir, "php.ini");
                if (File.Exists(phpIni))
                {
                    psi.ArgumentList.Add("-c");
                    psi.ArgumentList.Add(phpIni);
                }
                break;

            case ServiceKind.MariaDb:
                var dataDir = Path.Combine(workingDir, "data");
                if (!Directory.Exists(dataDir))
                {
                    preflightError = $"Folder data MariaDB/MySQL ('{dataDir}') belum diinisialisasi. Gunakan tombol 'Inisialisasi Database MariaDB' di menu Packages atau Services.";
                    argsSummary = string.Empty;
                    return null;
                }
                psi.ArgumentList.Add($"--port={settings.DatabasePort}");
                psi.ArgumentList.Add("--bind-address=127.0.0.1");
                psi.ArgumentList.Add($"--datadir={dataDir}");
                psi.ArgumentList.Add("--console");
                break;

            case ServiceKind.PostgreSql:
                var pgDataDir = Path.Combine(workingDir, "data");
                if (!Directory.Exists(pgDataDir) || !Directory.EnumerateFileSystemEntries(pgDataDir).Any())
                {
                    var initDbExe = Path.Combine(Path.GetDirectoryName(exePath) ?? workingDir, "initdb.exe");
                    if (File.Exists(initDbExe))
                    {
                        try
                        {
                            Directory.CreateDirectory(pgDataDir);
                            var initPsi = new ProcessStartInfo
                            {
                                FileName = initDbExe,
                                WorkingDirectory = workingDir,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            initPsi.ArgumentList.Add("-D");
                            initPsi.ArgumentList.Add(pgDataDir);
                            initPsi.ArgumentList.Add("-U");
                            initPsi.ArgumentList.Add("postgres");
                            initPsi.ArgumentList.Add("-A");
                            initPsi.ArgumentList.Add("trust");
                            initPsi.ArgumentList.Add("-E");
                            initPsi.ArgumentList.Add("UTF8");
                            using var initProc = Process.Start(initPsi);
                            initProc?.WaitForExit(30000);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn("PostgreSQL", $"Gagal auto-inisialisasi data PostgreSQL: {ex.Message}");
                        }
                    }
                }
                psi.ArgumentList.Add("-D");
                psi.ArgumentList.Add(pgDataDir);
                psi.ArgumentList.Add("-p");
                psi.ArgumentList.Add("5432");
                break;

            case ServiceKind.Apache:
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(workingDir);
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add(_configGenerator.ApacheMainConfigPath);
                psi.ArgumentList.Add("-DFOREGROUND");
                break;
        }

        argsSummary = string.Join(" ", psi.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        return psi;
    }

    public string? ResolveServiceExecutable(ServiceKind kind, AppSettings settings)
    {
        var runtimeBasePath = string.IsNullOrWhiteSpace(settings.RuntimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : settings.RuntimeBasePath;

        if (kind == ServiceKind.PhpCgi)
        {
            var activePhp = _phpVersionManager.GetActivePhpVersion(settings);
            if (activePhp is not null && !string.IsNullOrWhiteSpace(activePhp.PhpCgiExePath) && File.Exists(activePhp.PhpCgiExePath))
            {
                return activePhp.PhpCgiExePath;
            }
        }
        else if (kind == ServiceKind.MariaDb)
        {
            var activeDb = _phpVersionManager.GetActiveDatabaseVersion(settings);
            if (activeDb is not null && !string.IsNullOrWhiteSpace(activeDb.MysqldExePath) && File.Exists(activeDb.MysqldExePath))
            {
                return activeDb.MysqldExePath;
            }
        }

        return ResolveServiceExecutable(kind, runtimeBasePath);
    }

    public static string? ResolveServiceExecutable(ServiceKind kind, string runtimeBasePath)
    {
        if (string.IsNullOrWhiteSpace(runtimeBasePath))
        {
            runtimeBasePath = DraGonPaths.DefaultRuntimesDirectory;
        }

        return kind switch
        {
            ServiceKind.Nginx => RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                Path.Combine(runtimeBasePath, "nginx"), "nginx.exe"),
            ServiceKind.PhpCgi => RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                Path.Combine(runtimeBasePath, "php"), "php-cgi.exe"),
            ServiceKind.MariaDb => RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                Path.Combine(runtimeBasePath, "mariadb"), @"bin\mysqld.exe")
                ?? RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                    Path.Combine(runtimeBasePath, "mysql"), @"bin\mysqld.exe"),
            ServiceKind.Apache => RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                Path.Combine(runtimeBasePath, "apache"), @"bin\httpd.exe"),
            ServiceKind.PostgreSql => RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                Path.Combine(runtimeBasePath, "postgresql"), @"bin\postgres.exe")
                ?? RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                    Path.Combine(runtimeBasePath, "postgresql"), "postgres.exe"),
            _ => null
        };
    }

    private static string ResolveWorkingDirectory(ServiceKind kind, string exePath)
    {
        var exeDir = Path.GetDirectoryName(exePath) ?? DraGonPaths.DefaultRuntimesDirectory;
        if (kind is ServiceKind.MariaDb or ServiceKind.Apache or ServiceKind.PostgreSql)
        {
            var parent = Directory.GetParent(exeDir);
            if (parent is not null && string.Equals(Path.GetFileName(exeDir), "bin", StringComparison.OrdinalIgnoreCase))
            {
                return parent.FullName;
            }
        }

        return exeDir;
    }

    private bool IsManagedProcessRunning(ServiceKind kind, out int? pid)
    {
        pid = null;
        lock (_processLock)
        {
            if (_runningProcesses.TryGetValue(kind, out var proc))
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        pid = proc.Id;
                        return true;
                    }
                }
                catch
                {
                    // Process handle invalid
                }

                _runningProcesses.Remove(kind);
            }

            // Adopt already running process if started in an earlier DraGon session
            foreach (var procName in GetProcessNamesForKind(kind))
            {
                var found = Process.GetProcessesByName(procName);
                var alive = found.FirstOrDefault(p =>
                {
                    try
                    {
                        return !p.HasExited;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (alive is not null)
                {
                    _runningProcesses[kind] = alive;
                    pid = alive.Id;
                    foreach (var other in found.Where(p => !ReferenceEquals(p, alive)))
                    {
                        other.Dispose();
                    }
                    return true;
                }
            }
        }

        return false;
    }

    private static string[] GetProcessNamesForKind(ServiceKind kind) => kind switch
    {
        ServiceKind.Nginx => ["nginx"],
        ServiceKind.PhpCgi => ["php-cgi"],
        ServiceKind.MariaDb => ["mysqld", "mariadbd"],
        ServiceKind.Apache => ["httpd"],
        _ => []
    };

    private void OnManagedProcessExited(ServiceKind kind, Process process)
    {
        var exitCode = 0;
        try
        {
            exitCode = process.ExitCode;
        }
        catch
        {
            // Ignore
        }

        lock (_processLock)
        {
            if (_runningProcesses.TryGetValue(kind, out var current) && ReferenceEquals(current, process))
            {
                _runningProcesses.Remove(kind);
            }
        }

        var state = Services.First(s => s.Kind == kind);
        DispatchToUi(() =>
        {
            if (state.Status == ServiceStatus.Stopping)
            {
                return;
            }

            state.ProcessId = null;
            state.StartedAt = null;
            if (exitCode == 0)
            {
                state.Status = ServiceStatus.Stopped;
                state.StatusMessage = "Proses berhenti (Exit Code 0).";
            }
            else
            {
                state.Status = ServiceStatus.Error;
                state.StatusMessage = $"Proses berhenti tak terduga (Exit Code {exitCode}). Periksa log untuk detail.";
            }

            AppendServiceLog(state, $"Proses keluar dengan kode {exitCode}.");
        });

        _logger.Warn(state.DisplayName, $"Proses berhenti (Exit Code: {exitCode}).");
    }

    private static void AppendServiceLog(ServiceState state, string line)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {line}";
        state.RecentLogs.Add(timestamped);
        while (state.RecentLogs.Count > 200)
        {
            state.RecentLogs.RemoveAt(0);
        }
    }

    public string? DetectServiceVersion(ServiceKind kind, string? exePath, AppSettings settings)
    {
        try
        {
            if (kind == ServiceKind.PhpCgi)
            {
                var activePhp = _phpVersionManager.GetActivePhpVersion(settings);
                if (activePhp is not null && !string.IsNullOrWhiteSpace(activePhp.VersionNumber))
                {
                    return activePhp.VersionNumber;
                }
            }
            else if (kind == ServiceKind.MariaDb)
            {
                var activeDb = _phpVersionManager.GetActiveDatabaseVersion(settings);
                if (activeDb is not null && !string.IsNullOrWhiteSpace(activeDb.VersionNumber))
                {
                    var engine = string.IsNullOrWhiteSpace(activeDb.EngineType) ? "MariaDB" : activeDb.EngineType;
                    return $"{engine} {activeDb.VersionNumber}";
                }
            }

            if (!string.IsNullOrWhiteSpace(exePath))
            {
                return TryDetectBinaryVersion(exePath);
            }
        }
        catch
        {
            // Ignore error
        }

        return null;
    }

    public static string? TryDetectBinaryVersion(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.ProductVersion))
            {
                var clean = info.ProductVersion.Split(' ')[0].Trim();
                if (!clean.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                    System.Text.RegularExpressions.Regex.IsMatch(clean, @"\d+"))
                {
                    return clean;
                }
            }

            if (!string.IsNullOrWhiteSpace(info.FileVersion))
            {
                var clean = info.FileVersion.Split(' ')[0].Trim();
                if (!clean.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                    System.Text.RegularExpressions.Regex.IsMatch(clean, @"\d+"))
                {
                    return clean;
                }
            }

            // Fallback: periksa nama direktori
            var dir = Path.GetDirectoryName(exePath);
            while (!string.IsNullOrWhiteSpace(dir))
            {
                var folderName = Path.GetFileName(dir);
                if (string.Equals(folderName, "bin", StringComparison.OrdinalIgnoreCase))
                {
                    dir = Path.GetDirectoryName(dir);
                    continue;
                }

                var match = System.Text.RegularExpressions.Regex.Match(folderName, @"\d+(\.\d+)+");
                if (match.Success)
                {
                    return match.Value;
                }
                break;
            }
        }
        catch
        {
            // Ignore error
        }

        return null;
    }

    private void DispatchToUi(Action action)
    {
        if (_uiThreadDispatcher is not null)
        {
            _uiThreadDispatcher(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        lock (_processLock)
        {
            foreach (var proc in _runningProcesses.Values)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore on dispose
                }
                finally
                {
                    proc.Dispose();
                }
            }

            _runningProcesses.Clear();
        }
    }
}
