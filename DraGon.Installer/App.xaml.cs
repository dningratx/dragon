using System.IO;
using System.Windows;

namespace DraGon.Installer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            var confirm = MessageBox.Show(
                "Apakah Anda yakin ingin menghapus instalasi aplikasi DraGon dari komputer ini?\n\n" +
                "(Catatan: Folder proyek dan konfigurasi di %LocalAppData%\\DraGon tidak akan dihapus.)",
                "Uninstall DraGon",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                var (_, msg) = InstallerEngine.RunUninstall();
                MessageBox.Show(msg, "Uninstall DraGon", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Shutdown(0);
            return;
        }

        if (e.Args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
        {
            var targetDir = InstallerEngine.DefaultInstallDirectory;
            for (var i = 0; i < e.Args.Length - 1; i++)
            {
                if (string.Equals(e.Args[i], "--dir", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(e.Args[i + 1]))
                {
                    targetDir = e.Args[i + 1];
                }
            }

            var options = new InstallOptions(
                TargetDirectory: targetDir,
                WorkspaceDirectory: Path.Combine(targetDir, "www"),
                CreateDesktopShortcut: true,
                CreateStartMenuShortcut: true,
                InitializeWorkspaceAndDataDirs: true,
                LaunchAfterInstall: false);

            var (ok, _, _) = await InstallerEngine.RunInstallAsync(options);
            Shutdown(ok ? 0 : 1);
            return;
        }

        var window = new SetupWindow();
        MainWindow = window;
        window.Show();
    }
}
