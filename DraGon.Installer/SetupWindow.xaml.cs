using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DraGon.Installer;

public partial class SetupWindow : Window
{
    private bool _workspaceManuallyEdited;
    private bool _suppressTextEvents;

    public SetupWindow()
    {
        InitializeComponent();
        _suppressTextEvents = true;
        InstallPathTextBox.Text = InstallerEngine.DefaultInstallDirectory;
        WorkspacePathTextBox.Text = Path.Combine(InstallerEngine.DefaultInstallDirectory, "www");
        _suppressTextEvents = false;

        WorkspacePathTextBox.TextChanged += (_, _) =>
        {
            if (!_suppressTextEvents)
            {
                _workspaceManuallyEdited = true;
            }
        };

        Loaded += (_, _) =>
        {
            Topmost = true;
            Activate();
            Focus();
            Topmost = false;
        };
    }

    private void InstallPathTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents || WorkspacePathTextBox is null || _workspaceManuallyEdited)
        {
            return;
        }

        var currentAppDir = InstallPathTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(currentAppDir))
        {
            _suppressTextEvents = true;
            try
            {
                WorkspacePathTextBox.Text = Path.Combine(currentAppDir, "www");
            }
            catch
            {
                // Ignore invalid path characters while typing
            }
            finally
            {
                _suppressTextEvents = false;
            }
        }
    }

    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        var current = InstallPathTextBox.Text?.Trim();
        var initialDir = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current)
            ? current
            : @"C:\";

        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Lokasi Folder Aplikasi DraGon (Custom)",
            InitialDirectory = initialDir
        };

        if (dialog.ShowDialog() == true)
        {
            var selected = dialog.FolderName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // If user selected drive root (e.g. C: or D:), append DraGon; otherwise use exact custom folder selected
            if (selected.Length <= 3 && selected.EndsWith(':'))
            {
                InstallPathTextBox.Text = Path.Combine(selected + Path.DirectorySeparatorChar, "DraGon");
            }
            else
            {
                InstallPathTextBox.Text = dialog.FolderName;
            }
        }
    }

    private void BrowseWorkspaceButton_OnClick(object sender, RoutedEventArgs e)
    {
        var current = WorkspacePathTextBox.Text?.Trim();
        var initialDir = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current)
            ? current
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Lokasi Folder Workspace Proyek (Custom)",
            InitialDirectory = initialDir
        };

        if (dialog.ShowDialog() == true)
        {
            _workspaceManuallyEdited = true;
            WorkspacePathTextBox.Text = dialog.FolderName;
        }
    }

    private async void InstallButton_OnClick(object sender, RoutedEventArgs e)
    {
        var targetDir = InstallPathTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show(this, "Lokasi folder aplikasi tidak boleh kosong.", "Validasi Lokasi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var workspaceDir = WorkspacePathTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(workspaceDir))
        {
            workspaceDir = Path.Combine(targetDir, "www");
        }

        SetControlsEnabled(false);

        var options = new InstallOptions(
            TargetDirectory: targetDir,
            WorkspaceDirectory: workspaceDir,
            CreateDesktopShortcut: DesktopShortcutCheckBox.IsChecked == true,
            CreateStartMenuShortcut: StartMenuShortcutCheckBox.IsChecked == true,
            InitializeWorkspaceAndDataDirs: InitWorkspaceCheckBox.IsChecked == true,
            LaunchAfterInstall: LaunchAfterInstallCheckBox.IsChecked == true);

        var progress = new Progress<(int Percent, string Status)>(report =>
        {
            InstallProgressBar.Value = report.Percent;
            StatusTextBlock.Text = report.Status;
            PercentTextBlock.Text = $"{report.Percent}%";
        });

        var (success, message, installedExePath) = await InstallerEngine.RunInstallAsync(options, progress);
        SetControlsEnabled(true);

        if (!success)
        {
            MessageBox.Show(this, message, "Instalasi Gagal", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            this,
            $"{message}\n\n" +
            $"• Folder Aplikasi Custom: {targetDir}\n" +
            $"• Folder Workspace Proyek: {workspaceDir}\n" +
            $"• Pintasan Desktop: {(options.CreateDesktopShortcut ? "Dibuat" : "Dilewati")}\n" +
            $"• Pintasan Start Menu: {(options.CreateStartMenuShortcut ? "Dibuat" : "Dilewati")}\n\n" +
            "Seluruh runtime sudah siap! Saat aplikasi terbuka, Anda cukup klik tombol '▶ Start All'.",
            "Instalasi DraGon Selesai",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        if (options.LaunchAfterInstall && File.Exists(installedExePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installedExePath,
                    WorkingDirectory = Path.GetDirectoryName(installedExePath) ?? targetDir,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore launch failure
            }

            Close();
        }
    }

    private void UninstallButton_OnClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            this,
            "Hapus instalasi DraGon beserta pintasan Desktop dan Start Menu?\n\n" +
            "(Folder proyek dan pengaturan di %LocalAppData%\\DraGon tetap disimpan dengan aman.)",
            "Konfirmasi Uninstall DraGon",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var (ok, msg) = InstallerEngine.RunUninstall();
        InstallProgressBar.Value = ok ? 100 : 0;
        StatusTextBlock.Text = msg;
        MessageBox.Show(this, msg, ok ? "Uninstall Berhasil" : "Uninstall Gagal", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetControlsEnabled(bool enabled)
    {
        InstallButton.IsEnabled = enabled;
        UninstallButton.IsEnabled = enabled;
        BrowseButton.IsEnabled = enabled;
        BrowseWorkspaceButton.IsEnabled = enabled;
        InstallPathTextBox.IsEnabled = enabled;
        WorkspacePathTextBox.IsEnabled = enabled;
        DesktopShortcutCheckBox.IsEnabled = enabled;
        StartMenuShortcutCheckBox.IsEnabled = enabled;
        InitWorkspaceCheckBox.IsEnabled = enabled;
        LaunchAfterInstallCheckBox.IsEnabled = enabled;
    }
}
