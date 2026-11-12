using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            ApplyPackageModeBanner();
            UpdateEstimatedSize();

            Topmost = true;
            Activate();
            Focus();
            Topmost = false;
        };
    }

    private void ApplyPackageModeBanner()
    {
        var hasOffline = InstallerEngine.HasEmbeddedRuntimes();
        if (hasOffline)
        {
            SetupModeBannerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F2922"));
            SetupModeBannerBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            SetupModeTitleTextBlock.Text = "✓ Mode Offline (Paket Terbundel Lengkap)";
            SetupModeTitleTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            SetupModeDescTextBlock.Text = "Paket runtime esensial sudah disertakan di dalam installer. Anda tetap dapat memilih komponen mana yang ingin dipasang.";
            SetupModeDescTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7F3D0"));
        }
        else
        {
            SetupModeBannerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0C2340"));
            SetupModeBannerBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
            SetupModeTitleTextBlock.Text = "🌐 Mode Net Installer (Ukuran Ramping)";
            SetupModeTitleTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
            SetupModeDescTextBlock.Text = "Installer awal tidak memuat file component sehingga ukuran installer sangat kecil. Komponen yang Anda centang di bawah akan otomatis diunduh langsung dari server resmi saat proses instalasi.";
            SetupModeDescTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BAE6FD"));
        }
    }

    private void OnComponentCheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateEstimatedSize();
    }

    private void UpdateEstimatedSize()
    {
        long totalMb = 0;
        if (ChkApache?.IsChecked == true) totalMb += 15;
        if (ChkNginx?.IsChecked == true) totalMb += 2;
        if (ChkPhp83?.IsChecked == true) totalMb += 35;
        if (ChkPhp84?.IsChecked == true) totalMb += 35;
        if (ChkComposer?.IsChecked == true) totalMb += 4;
        if (ChkNodeJs?.IsChecked == true) totalMb += 35;
        if (ChkPython?.IsChecked == true) totalMb += 11;
        if (ChkMariaDb?.IsChecked == true) totalMb += 95;
        if (ChkPhpMyAdmin?.IsChecked == true) totalMb += 16;
        if (ChkPostgreSql?.IsChecked == true) totalMb += 315;

        if (EstimatedSizeTextBlock != null)
        {
            EstimatedSizeTextBlock.Text = totalMb > 0
                ? $"Total estimasi unduhan: ~{totalMb} MB"
                : "Hanya inti DraGon (Tanpa unduhan runtime)";
        }
    }

    private void PresetStandardButton_OnClick(object sender, RoutedEventArgs e)
    {
        ChkApache.IsChecked = true;
        ChkNginx.IsChecked = false;
        ChkPhp83.IsChecked = true;
        ChkPhp84.IsChecked = false;
        ChkComposer.IsChecked = true;
        ChkNodeJs.IsChecked = true;
        ChkPython.IsChecked = false;
        ChkMariaDb.IsChecked = true;
        ChkPhpMyAdmin.IsChecked = true;
        ChkPostgreSql.IsChecked = false;
        UpdateEstimatedSize();
    }

    private void PresetNginxButton_OnClick(object sender, RoutedEventArgs e)
    {
        ChkApache.IsChecked = false;
        ChkNginx.IsChecked = true;
        ChkPhp83.IsChecked = true;
        ChkPhp84.IsChecked = false;
        ChkComposer.IsChecked = true;
        ChkNodeJs.IsChecked = true;
        ChkPython.IsChecked = false;
        ChkMariaDb.IsChecked = true;
        ChkPhpMyAdmin.IsChecked = true;
        ChkPostgreSql.IsChecked = false;
        UpdateEstimatedSize();
    }

    private void PresetAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        ChkApache.IsChecked = true;
        ChkNginx.IsChecked = true;
        ChkPhp83.IsChecked = true;
        ChkPhp84.IsChecked = true;
        ChkComposer.IsChecked = true;
        ChkNodeJs.IsChecked = true;
        ChkPython.IsChecked = true;
        ChkMariaDb.IsChecked = true;
        ChkPhpMyAdmin.IsChecked = true;
        ChkPostgreSql.IsChecked = true;
        UpdateEstimatedSize();
    }

    private void PresetMinimalButton_OnClick(object sender, RoutedEventArgs e)
    {
        ChkApache.IsChecked = false;
        ChkNginx.IsChecked = false;
        ChkPhp83.IsChecked = false;
        ChkPhp84.IsChecked = false;
        ChkComposer.IsChecked = false;
        ChkNodeJs.IsChecked = false;
        ChkPython.IsChecked = false;
        ChkMariaDb.IsChecked = false;
        ChkPhpMyAdmin.IsChecked = false;
        ChkPostgreSql.IsChecked = false;
        UpdateEstimatedSize();
    }

    private List<string> GetSelectedComponents()
    {
        var list = new List<string>();
        if (ChkApache?.IsChecked == true) list.Add("apache");
        if (ChkNginx?.IsChecked == true) list.Add("nginx");
        if (ChkPhp83?.IsChecked == true) list.Add("php83");
        if (ChkPhp84?.IsChecked == true) list.Add("php84");
        if (ChkComposer?.IsChecked == true) list.Add("composer");
        if (ChkNodeJs?.IsChecked == true) list.Add("nodejs");
        if (ChkPython?.IsChecked == true) list.Add("python");
        if (ChkMariaDb?.IsChecked == true) list.Add("mariadb");
        if (ChkPhpMyAdmin?.IsChecked == true) list.Add("phpmyadmin");
        if (ChkPostgreSql?.IsChecked == true) list.Add("postgresql");
        return list;
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

        var selectedComponents = GetSelectedComponents();

        var options = new InstallOptions(
            TargetDirectory: targetDir,
            WorkspaceDirectory: workspaceDir,
            CreateDesktopShortcut: DesktopShortcutCheckBox.IsChecked == true,
            CreateStartMenuShortcut: StartMenuShortcutCheckBox.IsChecked == true,
            InitializeWorkspaceAndDataDirs: true,
            LaunchAfterInstall: LaunchAfterInstallCheckBox.IsChecked == true,
            SelectedComponents: selectedComponents,
            AddToWindowsPath: AddToPathCheckBox.IsChecked == true);

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

        var componentsSummary = selectedComponents.Count > 0
            ? string.Join(", ", selectedComponents.Select(InstallerEngine.GetFriendlyComponentName))
            : "Hanya Inti DraGon (Tanpa runtime bawaan)";

        MessageBox.Show(
            this,
            $"{message}\n\n" +
            $"• Folder Aplikasi Custom: {targetDir}\n" +
            $"• Folder Workspace Proyek: {workspaceDir}\n" +
            $"• Komponen Terpasang: {componentsSummary}\n" +
            $"• Pintasan Desktop: {(options.CreateDesktopShortcut ? "Dibuat" : "Dilewati")}\n" +
            $"• Pintasan Start Menu: {(options.CreateStartMenuShortcut ? "Dibuat" : "Dilewati")}\n\n" +
            "Seluruh konfigurasi siap! Saat aplikasi DraGon terbuka, Anda cukup klik tombol '▶ Start All'.",
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
        AddToPathCheckBox.IsEnabled = enabled;
        LaunchAfterInstallCheckBox.IsEnabled = enabled;

        PresetStandardButton.IsEnabled = enabled;
        PresetNginxButton.IsEnabled = enabled;
        PresetAllButton.IsEnabled = enabled;
        PresetMinimalButton.IsEnabled = enabled;

        ChkApache.IsEnabled = enabled;
        ChkNginx.IsEnabled = enabled;
        ChkPhp83.IsEnabled = enabled;
        ChkPhp84.IsEnabled = enabled;
        ChkComposer.IsEnabled = enabled;
        ChkNodeJs.IsEnabled = enabled;
        ChkPython.IsEnabled = enabled;
        ChkMariaDb.IsEnabled = enabled;
        ChkPhpMyAdmin.IsEnabled = enabled;
        ChkPostgreSql.IsEnabled = enabled;
    }
}
