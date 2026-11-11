using System.Diagnostics;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public enum ManagedProjectTaskKind
{
    NpmRunDev,
    ArtisanServe
}

public sealed class ProjectDevProcessManager : IDisposable
{
    private readonly AppLogger _logger;
    private readonly PhpVersionManagerService _phpVersionManager;
    private readonly Dictionary<Guid, Process> _projectProcesses = [];
    private readonly Lock _lock = new();
    private Action<Action>? _uiDispatcher;

    public ProjectDevProcessManager(AppLogger logger, PhpVersionManagerService phpVersionManager)
    {
        _logger = logger;
        _phpVersionManager = phpVersionManager;
    }

    public void SetUiDispatcher(Action<Action> dispatcher)
    {
        _uiDispatcher = dispatcher;
    }

    public bool HasRunningProjectProcesses
    {
        get
        {
            lock (_lock)
            {
                return _projectProcesses.Values.Any(p =>
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

    public async Task<(bool Success, string Message)> StartProjectDevTaskAsync(
        ProjectItem project,
        ManagedProjectTaskKind taskKind,
        AppSettings settings)
    {
        if (!Directory.Exists(project.FolderPath))
        {
            return (false, "Folder sumber proyek tidak ditemukan.");
        }

        if (IsRunning(project.Id, out var existingPid))
        {
            return (true, $"Proses dev untuk '{project.Name}' sudah berjalan (PID {existingPid}).");
        }

        var workingDir = Path.GetFullPath(project.FolderPath);
        string executable;
        string[] args;
        string label;

        if (taskKind == ManagedProjectTaskKind.NpmRunDev)
        {
            if (!File.Exists(Path.Combine(workingDir, "package.json")))
            {
                return (false, "File package.json tidak ditemukan di proyek ini.");
            }

            var npmPath = ResolveNodeOrNpmExecutable("npm.cmd", settings.RuntimeBasePath);
            if (string.IsNullOrWhiteSpace(npmPath))
            {
                return (false, "Runtime Node.js / npm belum ditemukan di folder runtime DraGon maupun PATH sistem. Pasang Node.js melalui menu Packages.");
            }

            executable = npmPath;
            args = ["run", "dev"];
            label = "npm run dev";
        }
        else
        {
            if (!File.Exists(Path.Combine(workingDir, "artisan")))
            {
                return (false, "File artisan tidak ditemukan di proyek ini.");
            }

            var activePhp = _phpVersionManager.GetActivePhpVersion(settings);
            var phpExe = activePhp?.PhpExePath;
            if (string.IsNullOrWhiteSpace(phpExe) || !File.Exists(phpExe))
            {
                phpExe = RuntimePackageCatalogService.FindInSystemPath("php.exe");
            }

            if (string.IsNullOrWhiteSpace(phpExe))
            {
                return (false, "Executable php.exe belum ditemukan di folder runtime DraGon maupun PATH sistem.");
            }

            executable = phpExe;
            args = ["artisan", "serve"];
            label = "php artisan serve";
        }

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        InjectRuntimeEnvironment(psi, settings);

        var projectLogPath = Path.Combine(DraGonPaths.LogsDirectory, $"project-{project.Domain}.log");

        try
        {
            var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.Write("DEV", $"{project.Name} ({label})", e.Data, projectLogPath);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.Write("DEV-ERR", $"{project.Name} ({label})", e.Data, projectLogPath);
                }
            };

            process.Exited += (_, _) =>
            {
                lock (_lock)
                {
                    _projectProcesses.Remove(project.Id);
                }

                DispatchToUi(() =>
                {
                    project.IsDevProcessRunning = false;
                    project.DevProcessPid = null;
                    project.DevProcessStatusText = $"Proses '{label}' telah berhenti.";
                });
            };

            if (!process.Start())
            {
                return (false, $"Gagal memulai proses '{label}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_lock)
            {
                _projectProcesses[project.Id] = process;
            }

            await Task.Delay(350);

            if (process.HasExited)
            {
                lock (_lock)
                {
                    _projectProcesses.Remove(project.Id);
                }
                return (false, $"Proses '{label}' langsung berhenti (Exit Code {process.ExitCode}). Periksa log untuk detail.");
            }

            var pid = process.Id;
            DispatchToUi(() =>
            {
                project.IsDevProcessRunning = true;
                project.DevProcessPid = pid;
                project.DevProcessStatusText = $"Berjalan: {label} (PID {pid})";
            });

            _logger.Info("DevProcess", $"Berhasil memulai '{label}' untuk proyek '{project.Name}' (PID {pid}).");
            return (true, $"Proses latar belakang '{label}' berjalan untuk proyek '{project.Name}' (PID {pid}).");
        }
        catch (Exception ex)
        {
            _logger.Error("DevProcess", $"Gagal memulai '{label}' pada proyek '{project.Name}'.", ex);
            return (false, $"Gagal memulai '{label}': {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> StopProjectDevTaskAsync(ProjectItem project)
    {
        Process? proc;
        lock (_lock)
        {
            _projectProcesses.TryGetValue(project.Id, out proc);
        }

        if (proc is null)
        {
            DispatchToUi(() =>
            {
                project.IsDevProcessRunning = false;
                project.DevProcessPid = null;
                project.DevProcessStatusText = "Tidak ada proses dev latar belakang";
            });
            return (true, "Tidak ada proses dev yang sedang berjalan pada proyek ini.");
        }

        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            lock (_lock)
            {
                _projectProcesses.Remove(project.Id);
            }
            proc.Dispose();
        }

        DispatchToUi(() =>
        {
            project.IsDevProcessRunning = false;
            project.DevProcessPid = null;
            project.DevProcessStatusText = "Proses dev dihentikan oleh pengguna.";
        });

        _logger.Info("DevProcess", $"Proses dev proyek '{project.Name}' berhasil dihentikan.");
        return (true, $"Proses dev untuk '{project.Name}' berhasil dihentikan.");
    }

    public async Task StopAllProjectDevTasksAsync()
    {
        List<Process> list;
        lock (_lock)
        {
            list = _projectProcesses.Values.ToList();
            _projectProcesses.Clear();
        }

        foreach (var proc in list)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
            }
            catch
            {
                // Ignore
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    private bool IsRunning(Guid projectId, out int? pid)
    {
        pid = null;
        lock (_lock)
        {
            if (_projectProcesses.TryGetValue(projectId, out var proc))
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
                    // Ignore
                }

                _projectProcesses.Remove(projectId);
            }
        }
        return false;
    }

    private static string? ResolveNodeOrNpmExecutable(string exeName, string runtimeBasePath)
    {
        var baseDir = string.IsNullOrWhiteSpace(runtimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : runtimeBasePath;

        var inRuntime = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
            Path.Combine(baseDir, "nodejs"),
            exeName);

        return !string.IsNullOrWhiteSpace(inRuntime)
            ? inRuntime
            : RuntimePackageCatalogService.FindInSystemPath(exeName);
    }

    private void InjectRuntimeEnvironment(ProcessStartInfo psi, AppSettings settings)
    {
        var baseDir = string.IsNullOrWhiteSpace(settings.RuntimeBasePath)
            ? DraGonPaths.DefaultRuntimesDirectory
            : settings.RuntimeBasePath;

        var activePhp = _phpVersionManager.GetActivePhpVersion(settings);
        var extraDirs = new List<string>();

        if (activePhp is not null && Directory.Exists(activePhp.DirectoryPath))
        {
            extraDirs.Add(activePhp.DirectoryPath);
        }

        extraDirs.AddRange(new[]
        {
            Path.Combine(baseDir, "nodejs"),
            Path.Combine(baseDir, "composer"),
            Path.Combine(baseDir, "php")
        }.Where(Directory.Exists));

        if (extraDirs.Count > 0)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            psi.Environment["PATH"] = string.Join(Path.PathSeparator, extraDirs) + Path.PathSeparator + currentPath;
        }
    }

    private void DispatchToUi(Action action)
    {
        if (_uiDispatcher is not null)
        {
            _uiDispatcher(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var proc in _projectProcesses.Values)
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
                    // Ignore
                }
                finally
                {
                    proc.Dispose();
                }
            }
            _projectProcesses.Clear();
        }
    }
}
