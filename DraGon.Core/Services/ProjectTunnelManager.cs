using System.Diagnostics;
using System.Text.RegularExpressions;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed partial class ProjectTunnelManager : IDisposable
{
    private sealed record ActiveTunnel(Process Process, TunnelReverseProxy Proxy);

    private readonly AppLogger _logger;
    private readonly Dictionary<Guid, ActiveTunnel> _activeTunnels = [];
    private readonly Lock _lock = new();
    private Action<Action>? _uiDispatcher;

    [GeneratedRegex(@"https://[a-zA-Z0-9-]+\.trycloudflare\.com", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TryCloudflareUrlRegex();

    public ProjectTunnelManager(AppLogger logger)
    {
        _logger = logger;
    }

    public void SetUiDispatcher(Action<Action> dispatcher)
    {
        _uiDispatcher = dispatcher;
    }

    public bool HasRunningTunnels
    {
        get
        {
            lock (_lock)
            {
                return _activeTunnels.Values.Any(t =>
                {
                    try
                    {
                        return !t.Process.HasExited;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
        }
    }

    public static string? ExtractTryCloudflareUrl(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var match = TryCloudflareUrlRegex().Match(line);
        return match.Success ? match.Value : null;
    }

    public static string? ResolveCloudflaredExecutable(string? runtimeBasePath)
    {
        if (!string.IsNullOrWhiteSpace(runtimeBasePath))
        {
            var direct = Path.Combine(runtimeBasePath, "cloudflared", "cloudflared.exe");
            if (File.Exists(direct))
            {
                return direct;
            }

            var recursive = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                Path.Combine(runtimeBasePath, "cloudflared"),
                "cloudflared.exe");
            if (!string.IsNullOrWhiteSpace(recursive))
            {
                return recursive;
            }
        }

        return RuntimePackageCatalogService.FindInSystemPath("cloudflared.exe");
    }

    public async Task<(bool Success, string Message, string? PublicUrl)> StartTunnelAsync(
        ProjectItem project,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning(project.Id, out _))
        {
            return (true, $"Tunnel untuk '{project.Name}' sudah aktif.", project.PublicTunnelUrl);
        }

        var cloudflaredExe = ResolveCloudflaredExecutable(settings.RuntimeBasePath);
        if (string.IsNullOrWhiteSpace(cloudflaredExe) || !File.Exists(cloudflaredExe))
        {
            return (false, "cloudflared.exe belum terpasang.", null);
        }

        var localPort = settings.PreferredWebServer == WebServerType.Apache
            ? settings.ApachePort
            : settings.WebServerPort;

        if (localPort <= 0)
        {
            localPort = 80;
        }

        var hostHeader = string.IsNullOrWhiteSpace(project.Domain) ? "localhost" : project.Domain.Trim();
        TunnelReverseProxy? reverseProxy = null;

        try
        {
            reverseProxy = new TunnelReverseProxy(hostHeader, localPort, _logger);
        }
        catch (Exception ex)
        {
            _logger.Error("Tunnel", $"Gagal memulai reverse proxy lokal untuk {project.Domain}", ex);
            return (false, $"Gagal memulai reverse proxy lokal: {ex.Message}", null);
        }

        var targetOrigin = $"http://127.0.0.1:{reverseProxy.ListenPort}";

        DispatchToUi(() =>
        {
            project.IsTunnelStarting = true;
            project.IsTunnelRunning = false;
            project.PublicTunnelUrl = null;
            project.TunnelStatusText = "Menghubungkan ke Cloudflare Tunnel...";
        });

        var psi = new ProcessStartInfo
        {
            FileName = cloudflaredExe,
            WorkingDirectory = Path.GetDirectoryName(cloudflaredExe) ?? DraGonPaths.AppDataRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("tunnel");
        psi.ArgumentList.Add("--no-autoupdate");
        psi.ArgumentList.Add("--url");
        psi.ArgumentList.Add(targetOrigin);

        var process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        var urlTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastErrorLine = string.Empty;

        void HandleLine(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            _logger.Info("Tunnel", $"[{project.Domain}] {data}");
            var url = ExtractTryCloudflareUrl(data);
            if (!string.IsNullOrWhiteSpace(url))
            {
                reverseProxy.SetPublicBaseUrl(url);
                urlTcs.TrySetResult(url);
            }
            else if (data.Contains("ERR", StringComparison.OrdinalIgnoreCase) ||
                     data.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                lastErrorLine = data;
            }
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);

        process.Exited += (_, _) =>
        {
            lock (_lock)
            {
                if (_activeTunnels.TryGetValue(project.Id, out var tracked) && ReferenceEquals(tracked.Process, process))
                {
                    _activeTunnels.Remove(project.Id);
                }
            }

            urlTcs.TrySetException(new InvalidOperationException(
                string.IsNullOrWhiteSpace(lastErrorLine)
                    ? "Proses cloudflared berhenti sebelum URL tunnel dibuat."
                    : lastErrorLine));

            DispatchToUi(() =>
            {
                project.IsTunnelStarting = false;
                project.IsTunnelRunning = false;
                project.PublicTunnelUrl = null;
                project.TunnelStatusText = "Tunnel nonaktif";
            });

            reverseProxy.Dispose();
            process.Dispose();
        };

        try
        {
            if (!process.Start())
            {
                ResetProjectTunnelState(project, "Gagal memulai cloudflared.");
                reverseProxy.Dispose();
                process.Dispose();
                return (false, "Gagal memulai proses cloudflared.exe.", null);
            }

            lock (_lock)
            {
                _activeTunnels[project.Id] = new ActiveTunnel(process, reverseProxy);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));

            var completed = await Task.WhenAny(urlTcs.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
            if (completed != urlTcs.Task)
            {
                await StopTunnelAsync(project);
                return (false, "Timeout menunggu URL publik dari Cloudflare Tunnel (25 detik). Periksa koneksi internet Anda.", null);
            }

            var publicUrl = await urlTcs.Task;
            reverseProxy.SetPublicBaseUrl(publicUrl);
            DispatchToUi(() =>
            {
                project.IsTunnelStarting = false;
                project.IsTunnelRunning = true;
                project.PublicTunnelUrl = publicUrl;
                project.TunnelStatusText = "Tunnel aktif";
            });

            _logger.Info("Tunnel", $"Public tunnel aktif untuk {project.Domain} -> {publicUrl} (via proxy 127.0.0.1:{reverseProxy.ListenPort} -> 127.0.0.1:{localPort})");
            return (true, $"Public tunnel aktif: {publicUrl}", publicUrl);
        }
        catch (Exception ex)
        {
            _logger.Error("Tunnel", $"Gagal menjalankan tunnel untuk {project.Domain}", ex);
            await StopTunnelAsync(project);
            reverseProxy.Dispose();
            return (false, $"Gagal menjalankan tunnel: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message)> StopTunnelAsync(ProjectItem project)
    {
        ActiveTunnel? tunnel;
        lock (_lock)
        {
            _activeTunnels.TryGetValue(project.Id, out tunnel);
            _activeTunnels.Remove(project.Id);
        }

        if (tunnel is null)
        {
            ResetProjectTunnelState(project, "Tunnel nonaktif");
            return (true, "Tunnel sudah nonaktif.");
        }

        try
        {
            tunnel.Proxy.Dispose();
            if (!tunnel.Process.HasExited)
            {
                tunnel.Process.Kill(entireProcessTree: true);
                await tunnel.Process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("Tunnel", $"Error menghentikan proses tunnel {project.Domain}: {ex.Message}");
        }
        finally
        {
            ResetProjectTunnelState(project, "Tunnel nonaktif");
        }

        return (true, $"Public tunnel untuk '{project.Name}' dihentikan.");
    }

    public async Task StopAllTunnelsAsync()
    {
        List<ActiveTunnel> tunnels;
        lock (_lock)
        {
            tunnels = [.. _activeTunnels.Values];
            _activeTunnels.Clear();
        }

        foreach (var tunnel in tunnels)
        {
            try
            {
                tunnel.Proxy.Dispose();
                if (!tunnel.Process.HasExited)
                {
                    tunnel.Process.Kill(entireProcessTree: true);
                    await tunnel.Process.WaitForExitAsync();
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    public bool IsRunning(Guid projectId, out int? pid)
    {
        lock (_lock)
        {
            if (_activeTunnels.TryGetValue(projectId, out var tunnel))
            {
                try
                {
                    if (!tunnel.Process.HasExited)
                    {
                        pid = tunnel.Process.Id;
                        return true;
                    }
                }
                catch
                {
                    // Process already exited
                }

                tunnel.Proxy.Dispose();
                _activeTunnels.Remove(projectId);
            }
        }

        pid = null;
        return false;
    }

    private void ResetProjectTunnelState(ProjectItem project, string statusText)
    {
        DispatchToUi(() =>
        {
            project.IsTunnelStarting = false;
            project.IsTunnelRunning = false;
            project.PublicTunnelUrl = null;
            project.TunnelStatusText = statusText;
        });
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
        List<ActiveTunnel> tunnels;
        lock (_lock)
        {
            tunnels = [.. _activeTunnels.Values];
            _activeTunnels.Clear();
        }

        foreach (var tunnel in tunnels)
        {
            try
            {
                tunnel.Proxy.Dispose();
                if (!tunnel.Process.HasExited)
                {
                    tunnel.Process.Kill(entireProcessTree: true);
                }
                tunnel.Process.Dispose();
            }
            catch
            {
                // Ignore during dispose
            }
        }
    }
}
