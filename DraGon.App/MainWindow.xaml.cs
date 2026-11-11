using System.ComponentModel;
using System.Drawing;
using System.Windows;
using DraGon.App.ViewModels;
using Forms = System.Windows.Forms;

namespace DraGon.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private Forms.NotifyIcon? _trayIcon;
    private bool _isExplicitExitRequested;
    private bool _hasShownTrayBalloonOnce;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        InitializeSystemTrayIcon();

        Loaded += (_, _) =>
        {
            Topmost = true;
            Activate();
            Focus();
            Topmost = false;
        };
    }

    private void InitializeSystemTrayIcon()
    {
        try
        {
            Icon? icon = null;
            var resourceStreamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/dragon-icon.ico"));
            if (resourceStreamInfo?.Stream is not null)
            {
                icon = new Icon(resourceStreamInfo.Stream);
            }
            else if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }

            var menu = new Forms.ContextMenuStrip();
            var showItem = new Forms.ToolStripMenuItem("Tampilkan Jendela DraGon")
            {
                Font = new Font(menu.Font, System.Drawing.FontStyle.Bold)
            };
            showItem.Click += (_, _) => Dispatcher.Invoke(RestoreFromTray);

            var startAllItem = new Forms.ToolStripMenuItem("▶ Start All Services");
            startAllItem.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                if (_viewModel.StartAllServicesCommand.CanExecute(null))
                {
                    _viewModel.StartAllServicesCommand.Execute(null);
                }
            });

            var stopAllItem = new Forms.ToolStripMenuItem("■ Stop All Services");
            stopAllItem.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                if (_viewModel.StopAllServicesCommand.CanExecute(null))
                {
                    _viewModel.StopAllServicesCommand.Execute(null);
                }
            });

            var reloadItem = new Forms.ToolStripMenuItem("🔄 Reload Services");
            reloadItem.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                if (_viewModel.ReloadServicesCommand.CanExecute(null))
                {
                    _viewModel.ReloadServicesCommand.Execute(null);
                }
            });

            var workspaceItem = new Forms.ToolStripMenuItem("Buka Folder Workspace");
            workspaceItem.Click += (_, _) => Dispatcher.Invoke(() =>
            {
                if (_viewModel.OpenWorkspaceFolderCommand.CanExecute(null))
                {
                    _viewModel.OpenWorkspaceFolderCommand.Execute(null);
                }
            });

            var exitItem = new Forms.ToolStripMenuItem("Keluar Sepenuhnya (Exit DraGon)");
            exitItem.Click += async (_, _) => await Dispatcher.InvokeAsync(ExitApplicationCompletelyAsync);

            menu.Opening += (_, _) =>
            {
                startAllItem.Enabled = _viewModel.CanStartAllServices;
                stopAllItem.Enabled = _viewModel.CanStopAllServices;
                reloadItem.Enabled = _viewModel.CanReloadServices;
            };

            menu.Items.Add(showItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(startAllItem);
            menu.Items.Add(stopAllItem);
            menu.Items.Add(reloadItem);
            menu.Items.Add(workspaceItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = icon ?? SystemIcons.Application,
                Text = "DraGon — Local Development Environment (.dra)",
                ContextMenuStrip = menu,
                Visible = true
            };

            _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreFromTray);
        }
        catch
        {
            // Ignore tray initialization error on headless environments
        }
    }

    private void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Focus();
        Topmost = false;
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExplicitExitRequested)
        {
            return;
        }

        // Directly minimize/hide to System Tray without any confirmation dialog
        e.Cancel = true;
        Hide();

        if (!_hasShownTrayBalloonOnce && _trayIcon is not null)
        {
            _hasShownTrayBalloonOnce = true;
            _trayIcon.ShowBalloonTip(
                2500,
                "DraGon Berjalan di Latar Belakang",
                "Aplikasi disembunyikan ke System Tray. Klik dua kali ikon DraGon di tray untuk membuka kembali, atau klik kanan untuk keluar.",
                Forms.ToolTipIcon.Info);
        }
    }

    private async void ExitApplicationButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExitApplicationCompletelyAsync();
    }

    private async Task ExitApplicationCompletelyAsync()
    {
        if (_isExplicitExitRequested)
        {
            return;
        }

        _isExplicitExitRequested = true;
        try
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            await _viewModel.StopAllServicesBeforeExitAsync();
            _viewModel.Dispose();
        }
        finally
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
