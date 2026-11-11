using System.IO.Compression;
using DraGon.Core.Models;
using DraGon.Core.Services;
using Xunit;

namespace DraGon.Tests;

public sealed class CoreServicesTests
{
    [Theory]
    [InlineData("tokoku.dra", true)]
    [InlineData("iklas.dra", true)]
    [InlineData("api.dralogy.dra", true)]
    [InlineData("sub.app-01.dra", true)]
    [InlineData("tokoku.test", false)]
    [InlineData("tokoku.com", false)]
    [InlineData("-invalid.dra", false)]
    [InlineData("spasi nama.dra", false)]
    [InlineData("injeksi;rm.dra", false)]
    public void ValidateDomain_EnforcesLocalDraFormat(string domain, bool expectedValid)
    {
        var result = ValidationService.ValidateDomain(domain);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("tokoku.test", ".test", true)]
    [InlineData("my-app.local", ".local", true)]
    [InlineData("api.v1.site.lan", ".lan", true)]
    [InlineData("tokoku.dra", ".test", false)]
    [InlineData("tokoku.test", ".dra", false)]
    [InlineData("-invalid.test", ".test", false)]
    [InlineData("spasi nama.test", ".test", false)]
    public void ValidateDomain_SupportsCustomHostnameSuffix(string domain, string suffix, bool expectedValid)
    {
        var result = ValidationService.ValidateDomain(domain, suffix);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(".dra", true)]
    [InlineData(".test", true)]
    [InlineData(".local", true)]
    [InlineData("test", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(".a", false)]
    [InlineData(".invalid_char", false)]
    public void ValidateHostnameSuffix_ValidatesCorrectly(string suffix, bool expectedValid)
    {
        var result = ValidationService.ValidateHostnameSuffix(suffix);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void SuggestDomainFromFolderOrName_UsesProvidedSuffix()
    {
        Assert.Equal("tokoku.test", ValidationService.SuggestDomainFromFolderOrName("tokoku", ".test"));
        Assert.Equal("tokoku.test", ValidationService.SuggestDomainFromFolderOrName("tokoku.dra", ".test"));
        Assert.Equal("warung-odan.local", ValidationService.SuggestDomainFromFolderOrName("Warung Odan", "local"));
        Assert.Equal("proyek.dra", ValidationService.SuggestDomainFromFolderOrName("", ".dra"));
    }

    [Fact]
    public void HostsFileService_PreservesUserEntriesAndBacksUpFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DraGonTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var fakeHosts = Path.Combine(tempRoot, "hosts");
            var backupDir = Path.Combine(tempRoot, "backups");

            var initialContent = """
                # Copyright (c) Microsoft Corp.
                127.0.0.1 localhost
                127.0.0.1 custom-user.local
                127.0.0.1 old-project.dra # [DraGon-Managed]
                """;
            File.WriteAllText(fakeHosts, initialContent);

            var logger = new AppLogger();
            var hostsService = new HostsFileService(logger);

            File.AppendAllText(fakeHosts, Environment.NewLine + "127.0.0.1 external-owned.dra");

            var externalConflict = hostsService.CheckDomainConflict("external-owned.dra", [], null, fakeHosts);
            Assert.False(externalConflict.IsValid);

            var validCheck = hostsService.CheckDomainConflict("tokoku.dra", [], null, fakeHosts);
            Assert.True(validCheck.IsValid);

            var backupPath = HostsFileService.ApplyManagedDomainsDirectly(
                fakeHosts,
                backupDir,
                ["tokoku.dra", "api.dralogy.dra"]);

            Assert.True(File.Exists(backupPath));

            var updated = File.ReadAllText(fakeHosts);
            Assert.Contains("127.0.0.1 custom-user.local", updated);
            Assert.Contains("127.0.0.1 external-owned.dra", updated);
            Assert.DoesNotContain("old-project.dra", updated);
            Assert.Contains("127.0.0.1 tokoku.dra # [DraGon-Managed]", updated);
            Assert.Contains("127.0.0.1 api.dralogy.dra # [DraGon-Managed]", updated);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ProjectDetectorService_DetectsLaravelAndNpmDevScript()
    {
        var tempProject = Path.Combine(Path.GetTempPath(), "DraGonLaravel_" + Guid.NewGuid().ToString("N"));
        var publicDir = Path.Combine(tempProject, "public");
        Directory.CreateDirectory(publicDir);

        try
        {
            File.WriteAllText(Path.Combine(tempProject, "artisan"), "#!/usr/bin/env php");
            File.WriteAllText(
                Path.Combine(tempProject, "composer.json"),
                """
                {
                  "name": "laravel/laravel",
                  "require": {
                    "php": "^8.2",
                    "laravel/framework": "^11.0"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(tempProject, "package.json"),
                """
                {
                  "scripts": {
                    "dev": "vite",
                    "build": "vite build"
                  }
                }
                """);
            File.WriteAllText(Path.Combine(publicDir, "index.php"), "<?php echo 'Hello';");

            var item = new ProjectItem
            {
                Name = "Tokoku",
                FolderPath = tempProject,
                Domain = "tokoku.dra"
            };

            ProjectDetectorService.AnalyzeProjectFolder(item);

            Assert.True(item.IsLaravel);
            Assert.True(item.HasArtisan);
            Assert.True(item.HasComposerJson);
            Assert.True(item.HasPackageJson);
            Assert.True(item.HasNpmDevScript);
            Assert.Equal("^11.0", item.DetectedLaravelVersion);
            Assert.Equal("^8.2", item.DetectedPhpConstraint);
            Assert.Equal(publicDir, item.DocumentRoot);
        }
        finally
        {
            if (Directory.Exists(tempProject))
            {
                Directory.Delete(tempProject, recursive: true);
            }
        }
    }

    [Fact]
    public void LocalSslService_GeneratesRootCaAndLeafCertificateForDraDomain()
    {
        var tempCertsDir = Path.Combine(Path.GetTempPath(), "DraGonCerts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempCertsDir);

        try
        {
            var logger = new AppLogger();
            var sslService = new LocalSslService(logger, tempCertsDir);

            using var rootCa = sslService.EnsureRootCaCertificate();
            Assert.NotNull(rootCa);
            Assert.True(File.Exists(sslService.RootCaPfxFilePath));
            Assert.True(File.Exists(sslService.RootCaCertPemFilePath));

            var domainPaths = sslService.EnsureDomainCertificate("tokoku.dra");
            Assert.True(File.Exists(domainPaths.CertificatePemPath));
            Assert.True(File.Exists(domainPaths.PrivateKeyPemPath));
            Assert.True(sslService.HasDomainCertificate("tokoku.dra"));

            var certPem = File.ReadAllText(domainPaths.CertificatePemPath);
            var keyPem = File.ReadAllText(domainPaths.PrivateKeyPemPath);
            Assert.Contains("BEGIN CERTIFICATE", certPem);
            Assert.Contains("BEGIN PRIVATE KEY", keyPem);
        }
        finally
        {
            if (Directory.Exists(tempCertsDir))
            {
                Directory.Delete(tempCertsDir, recursive: true);
            }
        }
    }

    [Fact]
    public void PhpVersionManagerService_DiscoversMultiplePhpVersionsAndConfiguresPhpIni()
    {
        var tempRuntime = Path.Combine(Path.GetTempPath(), "DraGonPhp_" + Guid.NewGuid().ToString("N"));
        var php82Dir = Path.Combine(tempRuntime, "php", "php-8.2");
        var php83Dir = Path.Combine(tempRuntime, "php", "php-8.3");
        Directory.CreateDirectory(php82Dir);
        Directory.CreateDirectory(php83Dir);

        try
        {
            File.WriteAllText(Path.Combine(php82Dir, "php.exe"), "stub");
            File.WriteAllText(Path.Combine(php82Dir, "php-cgi.exe"), "stub");
            File.WriteAllText(Path.Combine(php83Dir, "php.exe"), "stub");
            File.WriteAllText(Path.Combine(php83Dir, "php-cgi.exe"), "stub");
            File.WriteAllText(
                Path.Combine(php83Dir, "php.ini-development"),
                ";extension_dir = \"ext\"\n;extension=mbstring\n;extension=pdo_mysql");

            var logger = new AppLogger();
            var manager = new PhpVersionManagerService(logger);

            var versions = manager.DiscoverPhpVersions(tempRuntime);
            var localVersions = versions.Where(v => !v.IsFromSystemPath).ToList();
            Assert.Equal(2, localVersions.Count);

            var target83 = localVersions.First(v => v.DirectoryPath.Contains("php-8.3"));
            var (ok, _) = manager.EnsurePhpIniConfigured(target83);
            Assert.True(ok);

            var iniContent = File.ReadAllText(Path.Combine(php83Dir, "php.ini"));
            Assert.Contains("extension_dir = \"ext\"", iniContent);
            Assert.Contains("extension=mbstring", iniContent);
            Assert.Contains("extension=pdo_mysql", iniContent);

            var extensions = manager.GetExtensions(target83);
            Assert.NotEmpty(extensions);
            Assert.Contains(extensions, e => e.Name == "mbstring" && e.IsEnabled);

            var (toggleOffOk, _) = manager.SetExtensionEnabled(target83, "mbstring", enabled: false);
            Assert.True(toggleOffOk);
            Assert.DoesNotContain(manager.GetExtensions(target83), e => e.Name == "mbstring" && e.IsEnabled);

            var (toggleOnOk, _) = manager.SetExtensionEnabled(target83, "mbstring", enabled: true);
            Assert.True(toggleOnOk);
            Assert.Contains(manager.GetExtensions(target83), e => e.Name == "mbstring" && e.IsEnabled);
        }
        finally
        {
            if (Directory.Exists(tempRuntime))
            {
                Directory.Delete(tempRuntime, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimeInstallerService_ImportsAndFlattensOfficialZipArchive()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DraGonZip_" + Guid.NewGuid().ToString("N"));
        var zipSourceDir = Path.Combine(tempRoot, "source", "nginx-1.26.2");
        var zipFilePath = Path.Combine(tempRoot, "nginx-official.zip");
        var runtimeDir = Path.Combine(tempRoot, "runtimes");

        Directory.CreateDirectory(zipSourceDir);
        Directory.CreateDirectory(runtimeDir);

        try
        {
            File.WriteAllText(Path.Combine(zipSourceDir, "nginx.exe"), "binary-stub");
            ZipFile.CreateFromDirectory(Path.Combine(tempRoot, "source"), zipFilePath);

            var logger = new AppLogger();
            var installer = new RuntimeInstallerService(logger);
            var pkg = new RuntimePackageInfo
            {
                Id = "nginx",
                Name = "Nginx Web Server",
                ExpectedRelativeDir = "nginx",
                ExecutableFileName = "nginx.exe"
            };

            var (success, _) = await installer.ImportOfficialZipArchiveAsync(pkg, zipFilePath, runtimeDir);
            Assert.True(success);
            Assert.True(File.Exists(Path.Combine(runtimeDir, "nginx", "nginx.exe")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AllOfficialRuntimes_AreInstalledAndStartAllSucceeds()
    {
        var catalogService = new RuntimePackageCatalogService();
        var packages = catalogService.CreateCatalog();

        Assert.Equal(11, packages.Count);
        Assert.All(packages, p => Assert.True(p.HasDirectArchiveUrl, $"Package {p.Id} should have DirectArchiveDownloadUrl"));
        Assert.All(packages, p => Assert.True(p.AvailableVersions.Count > 0, $"Package {p.Id} should have AvailableVersions options"));

        var runtimeDir = DraGonPaths.ResolveBestRuntimeDirectory();
        if (Directory.Exists(runtimeDir))
        {
            catalogService.RefreshCatalogStatus(packages, runtimeDir);
            var bundledPackages = packages.Where(p => p.Id != "postgresql" && p.Id != "python" && p.Id != "heidisql");
            Assert.All(bundledPackages, p => Assert.True(p.IsAvailable, $"Package {p.Id} should be available in {runtimeDir}"));

            var logger = new AppLogger();
            var configService = new ConfigurationService(logger);
            var settings = configService.LoadSettings();
            var ssl = new LocalSslService(logger);
            var phpMgr = new PhpVersionManagerService(logger);
            var vhost = new ServerConfigGenerator(logger, ssl);
            var svcMgr = new ServiceProcessManager(logger, vhost, phpMgr);

            var (started, failed, summary) = await svcMgr.StartAllAsync(settings, configService.LoadProjects());
            Assert.Equal(3, started);
            Assert.Equal(0, failed);
            Assert.Contains("berhasil dijalankan", summary);
            Assert.True(File.Exists(DraGonPaths.PhpInfoFilePath));
        }
    }

    [Fact]
    public void RuntimePackageInfo_DownloadButtonState_ReflectsInstalledStatus()
    {
        var pkg = new RuntimePackageInfo
        {
            Id = "test",
            Name = "Test Package",
            DirectArchiveDownloadUrl = "https://example.com/test.zip"
        };

        // When not installed
        Assert.False(pkg.IsAvailable);
        Assert.True(pkg.CanDownloadAndInstall);
        Assert.Equal("⬇ Pasang", pkg.DownloadButtonText);
        Assert.Equal("🌐 Situs Resmi", pkg.WebsiteOrUpdateActionText);

        // When installed
        pkg.IsAvailable = true;
        Assert.False(pkg.CanDownloadAndInstall);
        Assert.Equal("✔ Terpasang", pkg.DownloadButtonText);
        Assert.Equal("🌐 Versi Terbaru", pkg.WebsiteOrUpdateActionText);

        // When installing
        pkg.IsAvailable = false;
        pkg.IsInstalling = true;
        Assert.False(pkg.CanDownloadAndInstall);
        Assert.False(pkg.CanInstallOrImport);
    }

    [Fact]
    public void RuntimePackageInfo_MultiVersionSelection_EnablesInstallWhenNotInstalled()
    {
        var pkg = new RuntimePackageInfo
        {
            Id = "php",
            Name = "PHP",
            ExecutableFileName = "php-cgi.exe"
        };

        var ver84 = new PackageVersionOption
        {
            DisplayName = "8.4.27",
            VersionNumber = "8.4.27",
            TargetSubFolder = "php-8.4.27",
            IsRecommended = true
        };
        var ver83 = new PackageVersionOption
        {
            DisplayName = "8.3.16",
            VersionNumber = "8.3.16",
            TargetSubFolder = "php-8.3.16"
        };

        pkg.AvailableVersions.Add(ver84);
        pkg.AvailableVersions.Add(ver83);

        // Simulasi PHP terpasang dengan versi 8.3.36 di sistem
        pkg.IsAvailable = true;
        pkg.DetectedVersion = "8.3.36";
        pkg.TargetRuntimeFolder = @"C:\DraGon\runtimes\php";

        // Saat versi 8.4.27 dipilih (belum terpasang di folder maupun terdeteksi)
        pkg.SelectedDownloadVersion = ver84;
        RuntimePackageCatalogService.UpdatePackageVersionSelectionState(pkg);

        Assert.False(pkg.IsSelectedVersionInstalled);
        Assert.True(pkg.CanDownloadAndInstall);
        Assert.Equal("⬇ Pasang", pkg.DownloadButtonText);
        Assert.Equal("○ Belum Terpasang", pkg.SelectedVersionStatusText);

        // Saat versi yang cocok terdeteksi (misal 8.3)
        pkg.SelectedDownloadVersion = ver83;
        pkg.DetectedVersion = "8.3.16";
        RuntimePackageCatalogService.UpdatePackageVersionSelectionState(pkg);

        Assert.True(pkg.IsSelectedVersionInstalled);
        Assert.False(pkg.CanDownloadAndInstall);
        Assert.Equal("✔ Terpasang", pkg.DownloadButtonText);
        Assert.Equal("✔ Terpasang", pkg.SelectedVersionStatusText);
    }

    [Fact]
    public void ServiceProcessManager_ContainsAllStartableServicesIncludingPostgreSql()
    {
        var logger = new AppLogger();
        var ssl = new LocalSslService(logger);
        var vhost = new ServerConfigGenerator(logger, ssl);
        var phpMgr = new PhpVersionManagerService(logger);
        var svcMgr = new ServiceProcessManager(logger, vhost, phpMgr);

        Assert.Equal(5, svcMgr.Services.Count);
        Assert.Contains(svcMgr.Services, s => s.Kind == ServiceKind.Apache);
        Assert.Contains(svcMgr.Services, s => s.Kind == ServiceKind.PhpCgi);
        Assert.Contains(svcMgr.Services, s => s.Kind == ServiceKind.MariaDb);
        Assert.Contains(svcMgr.Services, s => s.Kind == ServiceKind.PostgreSql && s.ConfiguredPort == 5432);
        Assert.Contains(svcMgr.Services, s => s.Kind == ServiceKind.Nginx);
    }

    [Fact]
    public void VersionedSubdirectory_ResolvesCorrectlyAcrossAllRuntimes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DraGonVer_" + Guid.NewGuid().ToString("N"));
        try
        {
            var apacheVerDir = Path.Combine(tempRoot, "apache", "httpd-2.4.70", "bin");
            var nginxVerDir = Path.Combine(tempRoot, "nginx", "nginx-1.26.2");
            var pmaVerDir = Path.Combine(tempRoot, "phpmyadmin", "phpmyadmin-5.2.3");
            var pgVerDir = Path.Combine(tempRoot, "postgresql", "postgresql-17.4", "bin");

            Directory.CreateDirectory(apacheVerDir);
            Directory.CreateDirectory(nginxVerDir);
            Directory.CreateDirectory(pmaVerDir);
            Directory.CreateDirectory(pgVerDir);

            File.WriteAllText(Path.Combine(apacheVerDir, "httpd.exe"), "dummy");
            File.WriteAllText(Path.Combine(nginxVerDir, "nginx.exe"), "dummy");
            File.WriteAllText(Path.Combine(pmaVerDir, "index.php"), "<?php");
            File.WriteAllText(Path.Combine(pgVerDir, "postgres.exe"), "dummy");

            Assert.Equal(Path.Combine(tempRoot, "apache", "httpd-2.4.70"), ServerConfigGenerator.ResolveApacheRuntimeDirectory(tempRoot));
            Assert.Equal(Path.Combine(tempRoot, "nginx", "nginx-1.26.2"), ServerConfigGenerator.ResolveNginxRuntimeDirectory(tempRoot));
            Assert.Equal(Path.Combine(tempRoot, "phpmyadmin", "phpmyadmin-5.2.3"), ServerConfigGenerator.ResolvePhpMyAdminDirectory(tempRoot));

            var foundPg = RuntimePackageCatalogService.FindExecutableInRuntimeFolder(
                Path.Combine(tempRoot, "postgresql"), @"bin\postgres.exe");
            Assert.Equal(Path.Combine(pgVerDir, "postgres.exe"), foundPg);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Catalog_ContainsPythonAndHeidiSql_WithMultipleDownloadVersions()
    {
        var catalogService = new RuntimePackageCatalogService();
        var packages = catalogService.CreateCatalog();

        var pythonPkg = packages.FirstOrDefault(p => p.Id == "python");
        Assert.NotNull(pythonPkg);
        Assert.Equal("python.exe", pythonPkg.ExecutableFileName);
        Assert.True(pythonPkg.AvailableVersions.Count >= 3);
        Assert.Contains(pythonPkg.AvailableVersions, v => v.VersionNumber.StartsWith("3.12"));

        var heidiPkg = packages.FirstOrDefault(p => p.Id == "heidisql");
        Assert.NotNull(heidiPkg);
        Assert.Equal("heidisql.exe", heidiPkg.ExecutableFileName);
        Assert.True(heidiPkg.AvailableVersions.Count >= 3);
        Assert.Contains(heidiPkg.AvailableVersions, v => v.VersionNumber.StartsWith("12.10"));

        // Test version selection update
        var phpPkg = packages.First(p => p.Id == "php");
        var originalUrl = phpPkg.EffectiveDownloadUrl;
        var altVersion = phpPkg.AvailableVersions.First(v => v.DownloadUrl != originalUrl);
        phpPkg.SelectedDownloadVersion = altVersion;
        Assert.NotEqual(originalUrl, phpPkg.EffectiveDownloadUrl);
        Assert.Equal(altVersion.DownloadUrl, phpPkg.EffectiveDownloadUrl);
    }

    [Fact]
    public void ServiceState_ActiveVersionDisplay_FormatsProperly()
    {
        var state = new ServiceState
        {
            Kind = ServiceKind.PhpCgi,
            DisplayName = "PHP FastCGI (php-cgi)",
            ActiveVersion = "8.3.36",
            Status = ServiceStatus.Running
        };

        Assert.True(state.HasActiveVersion);
        Assert.Equal("v8.3.36", state.ActiveVersionDisplay);
        Assert.Equal("Versi yang dijalankan: v8.3.36", state.ActiveVersionToolTip);

        state.ActiveVersion = "MariaDB 11.4.13";
        Assert.Equal("MariaDB 11.4.13", state.ActiveVersionDisplay);

        state.Status = ServiceStatus.Stopped;
        Assert.Equal("Versi terpasang: MariaDB 11.4.13", state.ActiveVersionToolTip);

        state.ActiveVersion = null;
        Assert.False(state.HasActiveVersion);
        Assert.Equal(string.Empty, state.ActiveVersionDisplay);
    }

    [Fact]
    public void EnvironmentPathService_ResolvesBinaryDirectories_Correctly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DraGonPathTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var phpDir = Path.Combine(tempRoot, "php", "php-8.3.36");
            Directory.CreateDirectory(phpDir);
            File.WriteAllText(Path.Combine(phpDir, "php.exe"), "stub");

            var composerDir = Path.Combine(tempRoot, "composer");
            Directory.CreateDirectory(composerDir);
            File.WriteAllText(Path.Combine(composerDir, "composer.bat"), "@echo off");

            var dbBinDir = Path.Combine(tempRoot, "mariadb", "mariadb-11.4.13", "bin");
            Directory.CreateDirectory(dbBinDir);
            File.WriteAllText(Path.Combine(dbBinDir, "mysql.exe"), "stub");

            var resolved = EnvironmentPathService.ResolveRuntimeBinaryDirectories(tempRoot);
            Assert.Contains(resolved, p => p.Contains("php-8.3.36"));
            Assert.Contains(resolved, p => p.EndsWith("composer"));
            Assert.Contains(resolved, p => p.Contains("mariadb") && p.EndsWith("bin"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ProjectScaffoldingService_CreateStandardProject_CreatesFolderAndIndexPhp()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "DraGonScaffoldTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new AppLogger();
            var service = new ProjectScaffoldingService(logger);

            var ok = service.CreateStandardProject(tempFolder, "Warung Odan", "warung-odan.dra", out var msg);
            Assert.True(ok);
            Assert.True(Directory.Exists(tempFolder));

            var indexPhp = Path.Combine(tempFolder, "index.php");
            Assert.True(File.Exists(indexPhp));
            var content = File.ReadAllText(indexPhp);
            Assert.Contains("Warung Odan", content);
            Assert.Contains("warung-odan.dra", content);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void ProjectDetector_DetectsWordPressProject_Properly()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "DraGonWpTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempFolder);
            File.WriteAllText(Path.Combine(tempFolder, "wp-config-sample.php"), "<?php // WordPress");
            Directory.CreateDirectory(Path.Combine(tempFolder, "wp-content"));

            var project = new ProjectItem { FolderPath = tempFolder };
            ProjectDetectorService.AnalyzeProjectFolder(project);

            Assert.Equal(ProjectFramework.WordPress, project.FrameworkType);
            Assert.True(project.IsWordPress);
            Assert.Equal("WordPress", project.FrameworkBadgeText);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void ExternalLauncherService_ResolvePowerShellPath_ReturnsValidPath()
    {
        var path = ExternalLauncherService.ResolvePowerShellPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(File.Exists(path) || path == "powershell.exe");
    }
}
