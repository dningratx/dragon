using System.Diagnostics;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public sealed class RuntimePackageCatalogService
{
    public IReadOnlyList<RuntimePackageInfo> CreateCatalog()
    {
        var catalog = new List<RuntimePackageInfo>
        {
            new RuntimePackageInfo
            {
                Id = "nginx",
                Name = "Nginx Web Server",
                Category = "Web Server (HTTP & HTTPS)",
                RecommendedVersion = "1.26.3 Stable (LTS)",
                License = "BSD 2-Clause License",
                OfficialWebsiteUrl = "https://nginx.org/",
                OfficialDownloadUrl = "https://nginx.org/en/download.html",
                DirectArchiveDownloadUrl = "https://nginx.org/download/nginx-1.26.3.zip",
                ExpectedRelativeDir = "nginx",
                ExecutableFileName = "nginx.exe",
                InstallInstructions =
                    "Klik 'Unduh & Pasang' untuk mengunduh langsung dari nginx.org, atau pilih versi lain dari dropdown di bawah."
            },
            new RuntimePackageInfo
            {
                Id = "php",
                Name = "PHP FastCGI Runtime (Multi-Versi)",
                Category = "Bahasa Pemrograman / CGI",
                RecommendedVersion = "8.3 LTS (x64)",
                License = "PHP License v3.01",
                OfficialWebsiteUrl = "https://www.php.net/",
                OfficialDownloadUrl = "https://windows.php.net/download/",
                DirectArchiveDownloadUrl = "https://windows.php.net/downloads/releases/php-8.3.16-Win32-vs16-x64.zip",
                ExpectedRelativeDir = "php",
                ExecutableFileName = "php-cgi.exe",
                InstallInstructions =
                    "Mendukung multi-versi PHP berdampingan. Rilis rekomendasi adalah PHP 8.3 LTS aktif."
            },
            new RuntimePackageInfo
            {
                Id = "mariadb",
                Name = "MariaDB Server (atau MySQL)",
                Category = "Database Relasional",
                RecommendedVersion = "11.4.13 LTS",
                License = "GNU GPL v2",
                OfficialWebsiteUrl = "https://mariadb.org/",
                OfficialDownloadUrl = "https://mariadb.org/download/",
                DirectArchiveDownloadUrl = "https://downloads.mariadb.org/rest-api/mariadb/11.4.13/mariadb-11.4.13-winx64.zip",
                ExpectedRelativeDir = "mariadb",
                ExecutableFileName = @"bin\mysqld.exe",
                InstallInstructions =
                    "Server database SQL lokal cepat dan terintegrasi siap pakai. Mendukung pilihan rilis MariaDB 11.4 LTS atau 10.11 LTS."
            },
            new RuntimePackageInfo
            {
                Id = "postgresql",
                Name = "PostgreSQL Database Server",
                Category = "Database Relasional",
                RecommendedVersion = "17.4 LTS / 16.8 (x64)",
                License = "PostgreSQL License",
                OfficialWebsiteUrl = "https://www.postgresql.org/",
                OfficialDownloadUrl = "https://www.enterprisedb.com/download-postgresql-binaries",
                DirectArchiveDownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-17.4-1-windows-x64-binaries.zip",
                ExpectedRelativeDir = "postgresql",
                ExecutableFileName = @"bin\postgres.exe",
                InstallInstructions =
                    "Mendukung database PostgreSQL (postgres & psql). Pilih versi 17.4 LTS atau 16.8 LTS untuk diunduh dan dipasang."
            },
            new RuntimePackageInfo
            {
                Id = "apache",
                Name = "Apache HTTP Server",
                Category = "Web Server (Alternatif)",
                RecommendedVersion = "2.4.70 Win64",
                License = "Apache License 2.0",
                OfficialWebsiteUrl = "https://httpd.apache.org/",
                OfficialDownloadUrl = "https://www.apachelounge.com/download/",
                DirectArchiveDownloadUrl = "https://www.apachelounge.com/download/VS18/binaries/httpd-2.4.70-261106-Win64-VS18.zip",
                ExpectedRelativeDir = "apache",
                ExecutableFileName = @"bin\httpd.exe",
                InstallInstructions =
                    "Web server Apache dengan mod_rewrite dan .htaccess. Tersedia versi Apache 2.4.70 (VS18) dan 2.4.63 (VS17)."
            },
            new RuntimePackageInfo
            {
                Id = "composer",
                Name = "Composer Dependency Manager",
                Category = "Manajer Paket PHP",
                RecommendedVersion = "2.8.x Stable",
                License = "MIT License",
                OfficialWebsiteUrl = "https://getcomposer.org/",
                OfficialDownloadUrl = "https://getcomposer.org/download/",
                DirectArchiveDownloadUrl = "https://getcomposer.org/download/latest-stable/composer.phar",
                ExpectedRelativeDir = "composer",
                ExecutableFileName = "composer.bat",
                InstallInstructions =
                    "Manajer paket dependensi PHP & Laravel resmi. Tersedia rilis terbaru 2.8.x, 2.7.x, dan 2.2 LTS."
            },
            new RuntimePackageInfo
            {
                Id = "nodejs",
                Name = "Node.js & NPM Runtime",
                Category = "Frontend & Dev Server (Vite)",
                RecommendedVersion = "22.12.0 LTS",
                License = "MIT License",
                OfficialWebsiteUrl = "https://nodejs.org/",
                OfficialDownloadUrl = "https://nodejs.org/en/download",
                DirectArchiveDownloadUrl = "https://nodejs.org/dist/v22.12.0/node-v22.12.0-win-x64.zip",
                ExpectedRelativeDir = "nodejs",
                ExecutableFileName = "node.exe",
                InstallInstructions =
                    "Runtime Node.js dan NPM untuk tooling frontend seperti Vite, React, Vue, Tailwind CSS, dan Next.js."
            },
            new RuntimePackageInfo
            {
                Id = "git",
                Name = "Git & GitHub CLI",
                Category = "Version Control & GitHub",
                RecommendedVersion = "2.48.1 (Git) / 2.67.0 (gh)",
                License = "GNU GPL v2 & MIT License",
                OfficialWebsiteUrl = "https://github.com/cli/cli",
                OfficialDownloadUrl = "https://git-scm.com/download/win",
                DirectArchiveDownloadUrl = "https://github.com/cli/cli/releases/download/v2.67.0/gh_2.67.0_windows_amd64.zip",
                ExpectedRelativeDir = "git",
                ExecutableFileName = @"bin\git.exe",
                InstallInstructions =
                    "Mendukung Git dan GitHub CLI (gh) untuk manajemen repositori langsung dari lingkungan DraGon."
            },
            new RuntimePackageInfo
            {
                Id = "phpmyadmin",
                Name = "phpMyAdmin (Manajer Database Web)",
                Category = "Antarmuka Database MySQL / MariaDB",
                RecommendedVersion = "5.2.3 All-Languages",
                License = "GNU GPL v2",
                OfficialWebsiteUrl = "https://www.phpmyadmin.net/",
                OfficialDownloadUrl = "https://www.phpmyadmin.net/downloads/",
                DirectArchiveDownloadUrl = "https://files.phpmyadmin.net/phpMyAdmin/5.2.3/phpMyAdmin-5.2.3-all-languages.zip",
                ExpectedRelativeDir = "phpmyadmin",
                ExecutableFileName = "index.php",
                InstallInstructions =
                    "Antarmuka web manajemen database MySQL / MariaDB lokal. Dapat diakses via tombol 'phpMyAdmin' atau https://phpmyadmin.dra."
            },
            new RuntimePackageInfo
            {
                Id = "python",
                Name = "Python Runtime (Embeddable & Pip)",
                Category = "Bahasa Pemrograman / AI & Backend",
                RecommendedVersion = "3.12.9 (x64)",
                License = "Python Software Foundation License (PSF)",
                OfficialWebsiteUrl = "https://www.python.org/",
                OfficialDownloadUrl = "https://www.python.org/downloads/windows/",
                DirectArchiveDownloadUrl = "https://www.python.org/ftp/python/3.12.9/python-3.12.9-embed-amd64.zip",
                ExpectedRelativeDir = "python",
                ExecutableFileName = "python.exe",
                InstallInstructions =
                    "Distribusi portabel Python 64-bit untuk eksekusi skrip, otomasi, AI/ML, dan backend. Otomatis terintegrasi ke terminal DraGon."
            },
            new RuntimePackageInfo
            {
                Id = "heidisql",
                Name = "HeidiSQL Database Manager (GUI)",
                Category = "Manajer Database Desktop GUI",
                RecommendedVersion = "12.10 Portable",
                License = "GNU GPL v2",
                OfficialWebsiteUrl = "https://www.heidisql.com/",
                OfficialDownloadUrl = "https://www.heidisql.com/download.php",
                DirectArchiveDownloadUrl = "https://www.heidisql.com/downloads/releases/HeidiSQL_12.10_64_Portable.zip",
                ExpectedRelativeDir = "heidisql",
                ExecutableFileName = "heidisql.exe",
                InstallInstructions =
                    "Aplikasi desktop GUI populer untuk mengelola MariaDB, MySQL, PostgreSQL, SQLite, dan MS SQL dengan cepat (seperti pada Laragon)."
            }
        };

        PopulateVersionPresets(catalog);
        return catalog;
    }

    private static void PopulateVersionPresets(List<RuntimePackageInfo> catalog)
    {
        foreach (var pkg in catalog)
        {
            pkg.AvailableVersions.Clear();

            switch (pkg.Id)
            {
                case "nginx":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "1.26.3 Stable (LTS - Rekomendasi)",
                        VersionNumber = "1.26.3",
                        DownloadUrl = "https://nginx.org/download/nginx-1.26.3.zip",
                        TargetSubFolder = "nginx-1.26.3",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "1.26.2 Stable (LTS)",
                        VersionNumber = "1.26.2",
                        DownloadUrl = "https://nginx.org/download/nginx-1.26.2.zip",
                        TargetSubFolder = "nginx-1.26.2"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "1.27.4 Mainline (Terbaru)",
                        VersionNumber = "1.27.4",
                        DownloadUrl = "https://nginx.org/download/nginx-1.27.4.zip",
                        TargetSubFolder = "nginx-1.27.4"
                    });
                    break;

                case "php":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "8.3 LTS (VS16 x64 - Rekomendasi)",
                        VersionNumber = "8.3",
                        DownloadUrl = "https://windows.php.net/downloads/releases/php-8.3.16-Win32-vs16-x64.zip",
                        TargetSubFolder = "php-8.3",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "8.4 (VS17 x64 - Terbaru)",
                        VersionNumber = "8.4",
                        DownloadUrl = "https://windows.php.net/downloads/releases/php-8.4.27-1-Win32-vs17-x64.zip",
                        TargetSubFolder = "php-8.4"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "8.2 (VS16 x64 - LTS)",
                        VersionNumber = "8.2",
                        DownloadUrl = "https://windows.php.net/downloads/releases/php-8.2.27-Win32-vs16-x64.zip",
                        TargetSubFolder = "php-8.2"
                    });
                    break;

                case "mariadb":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "11.4 LTS (Rekomendasi)",
                        VersionNumber = "11.4",
                        DownloadUrl = "https://downloads.mariadb.org/rest-api/mariadb/11.4.13/mariadb-11.4.13-winx64.zip",
                        TargetSubFolder = "mariadb-11.4",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "10.11 LTS (LTS Lama)",
                        VersionNumber = "10.11",
                        DownloadUrl = "https://downloads.mariadb.org/rest-api/mariadb/10.11.10/mariadb-10.11.10-winx64.zip",
                        TargetSubFolder = "mariadb-10.11"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "11.8 (Rolling Terbaru)",
                        VersionNumber = "11.8",
                        DownloadUrl = "https://downloads.mariadb.org/rest-api/mariadb/11.8.1/mariadb-11.8.1-winx64.zip",
                        TargetSubFolder = "mariadb-11.8"
                    });
                    break;

                case "postgresql":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "17.4 LTS (Rekomendasi)",
                        VersionNumber = "17.4",
                        DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-17.4-1-windows-x64-binaries.zip",
                        TargetSubFolder = "postgresql-17.4",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "16.8 LTS",
                        VersionNumber = "16.8",
                        DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-16.8-1-windows-x64-binaries.zip",
                        TargetSubFolder = "postgresql-16.8"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "15.12 LTS",
                        VersionNumber = "15.12",
                        DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-15.12-1-windows-x64-binaries.zip",
                        TargetSubFolder = "postgresql-15.12"
                    });
                    break;

                case "apache":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "2.4.70 Win64 VS18 (Rekomendasi)",
                        VersionNumber = "2.4.70",
                        DownloadUrl = "https://www.apachelounge.com/download/VS18/binaries/httpd-2.4.70-261106-Win64-VS18.zip",
                        TargetSubFolder = "httpd-2.4.70",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "2.4.63 Win64 VS17",
                        VersionNumber = "2.4.63",
                        DownloadUrl = "https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.63-250207-Win64-VS17.zip",
                        TargetSubFolder = "httpd-2.4.63"
                    });
                    break;

                case "composer":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "2.8.x Latest Stable (Rekomendasi)",
                        VersionNumber = "2.8",
                        DownloadUrl = "https://getcomposer.org/download/latest-stable/composer.phar",
                        TargetSubFolder = "composer-2.8.6",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "2.7.x Stable",
                        VersionNumber = "2.7",
                        DownloadUrl = "https://getcomposer.org/download/latest-2.7.x/composer.phar",
                        TargetSubFolder = "composer-2.7.9"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "2.2.x LTS (PHP 5.3-7.x)",
                        VersionNumber = "2.2",
                        DownloadUrl = "https://getcomposer.org/download/latest-2.2.x/composer.phar",
                        TargetSubFolder = "composer-2.2.24"
                    });
                    break;

                case "nodejs":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "22.14.0 LTS (Rekomendasi)",
                        VersionNumber = "22.14.0",
                        DownloadUrl = "https://nodejs.org/dist/v22.14.0/node-v22.14.0-win-x64.zip",
                        TargetSubFolder = "node-v22.14.0",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "20.18.3 LTS (Maintenance)",
                        VersionNumber = "20.18.3",
                        DownloadUrl = "https://nodejs.org/dist/v20.18.3/node-v20.18.3-win-x64.zip",
                        TargetSubFolder = "node-v20.18.3"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "23.9.0 Current (Terbaru)",
                        VersionNumber = "23.9.0",
                        DownloadUrl = "https://nodejs.org/dist/v23.9.0/node-v23.9.0-win-x64.zip",
                        TargetSubFolder = "node-v23.9.0"
                    });
                    break;

                case "git":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "gh 2.67.0 + Git (Rekomendasi)",
                        VersionNumber = "2.67.0",
                        DownloadUrl = "https://github.com/cli/cli/releases/download/v2.67.0/gh_2.67.0_windows_amd64.zip",
                        TargetSubFolder = "git-2.48.1",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "gh 2.65.0",
                        VersionNumber = "2.65.0",
                        DownloadUrl = "https://github.com/cli/cli/releases/download/v2.65.0/gh_2.65.0_windows_amd64.zip",
                        TargetSubFolder = "git-2.47.0"
                    });
                    break;

                case "phpmyadmin":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "5.2.3 All-Languages (Rekomendasi)",
                        VersionNumber = "5.2.3",
                        DownloadUrl = "https://files.phpmyadmin.net/phpMyAdmin/5.2.3/phpMyAdmin-5.2.3-all-languages.zip",
                        TargetSubFolder = "phpmyadmin-5.2.3",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "5.2.2 All-Languages",
                        VersionNumber = "5.2.2",
                        DownloadUrl = "https://files.phpmyadmin.net/phpMyAdmin/5.2.2/phpMyAdmin-5.2.2-all-languages.zip",
                        TargetSubFolder = "phpmyadmin-5.2.2"
                    });
                    break;

                case "python":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "3.12.9 (Rekomendasi)",
                        VersionNumber = "3.12.9",
                        DownloadUrl = "https://www.python.org/ftp/python/3.12.9/python-3.12.9-embed-amd64.zip",
                        TargetSubFolder = "python-3.12.9",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "3.11.9 LTS",
                        VersionNumber = "3.11.9",
                        DownloadUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip",
                        TargetSubFolder = "python-3.11.9"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "3.13.2 Terbaru",
                        VersionNumber = "3.13.2",
                        DownloadUrl = "https://www.python.org/ftp/python/3.13.2/python-3.13.2-embed-amd64.zip",
                        TargetSubFolder = "python-3.13.2"
                    });
                    break;

                case "heidisql":
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "12.10 Portable (Rekomendasi)",
                        VersionNumber = "12.10",
                        DownloadUrl = "https://www.heidisql.com/downloads/releases/HeidiSQL_12.10_64_Portable.zip",
                        TargetSubFolder = "heidisql-12.10",
                        IsRecommended = true
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "12.8 Portable",
                        VersionNumber = "12.8",
                        DownloadUrl = "https://www.heidisql.com/downloads/releases/HeidiSQL_12.8_64_Portable.zip",
                        TargetSubFolder = "heidisql-12.8"
                    });
                    pkg.AvailableVersions.Add(new PackageVersionOption
                    {
                        DisplayName = "12.6 Portable",
                        VersionNumber = "12.6",
                        DownloadUrl = "https://www.heidisql.com/downloads/releases/HeidiSQL_12.6_64_Portable.zip",
                        TargetSubFolder = "heidisql-12.6"
                    });
                    break;
            }

            if (pkg.AvailableVersions.Count > 0)
            {
                pkg.SelectedDownloadVersion = pkg.AvailableVersions.FirstOrDefault(v => v.IsRecommended) ?? pkg.AvailableVersions[0];
            }
        }
    }

    public void RefreshCatalogStatus(IEnumerable<RuntimePackageInfo> packages, string runtimeBasePath)
    {
        foreach (var pkg in packages)
        {
            var targetFolder = Path.Combine(runtimeBasePath, pkg.ExpectedRelativeDir);
            pkg.TargetRuntimeFolder = targetFolder;

            string? foundInRuntime = null;
            if (string.Equals(pkg.Id, "git", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var relPath in new[] { @"cmd\git.exe", @"bin\git.exe", "git.exe", @"bin\gh.exe", "gh.exe" })
                {
                    foundInRuntime = FindExecutableInRuntimeFolder(targetFolder, relPath);
                    if (foundInRuntime is not null) break;
                }
            }
            else if (string.Equals(pkg.Id, "postgresql", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var relPath in new[] { @"bin\postgres.exe", @"bin\pg_ctl.exe", @"bin\psql.exe" })
                {
                    foundInRuntime = FindExecutableInRuntimeFolder(targetFolder, relPath);
                    if (foundInRuntime is not null) break;
                }
            }
            else if (string.Equals(pkg.Id, "python", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var relPath in new[] { "python.exe", @"Scripts\python.exe" })
                {
                    foundInRuntime = FindExecutableInRuntimeFolder(targetFolder, relPath);
                    if (foundInRuntime is not null) break;
                }
            }
            else if (string.Equals(pkg.Id, "heidisql", StringComparison.OrdinalIgnoreCase))
            {
                foundInRuntime = FindExecutableInRuntimeFolder(targetFolder, "heidisql.exe");
            }
            else
            {
                foundInRuntime = FindExecutableInRuntimeFolder(targetFolder, pkg.ExecutableFileName);
            }

            if (foundInRuntime is not null)
            {
                pkg.IsAvailable = true;
                pkg.IsFromSystemPath = false;
                pkg.DetectedExecutablePath = foundInRuntime;
                pkg.DetectedVersion = TryGetFileVersion(foundInRuntime);

                // Auto-select opsi yang cocok dengan versi yang terpasang di komputer
                if (pkg.AvailableVersions.Count > 0)
                {
                    var matched = pkg.AvailableVersions.FirstOrDefault(opt => IsVersionOptionMatch(pkg, opt));
                    if (matched is not null)
                    {
                        pkg.SelectedDownloadVersion = matched;
                    }
                }

                UpdatePackageVersionSelectionState(pkg);
                continue;
            }

            string? foundInPath = null;
            if (string.Equals(pkg.Id, "git", StringComparison.OrdinalIgnoreCase))
            {
                foundInPath = FindInSystemPath("git.exe") ?? FindInSystemPath("gh.exe");
            }
            else if (string.Equals(pkg.Id, "postgresql", StringComparison.OrdinalIgnoreCase))
            {
                foundInPath = FindInSystemPath("postgres.exe") ?? FindInSystemPath("pg_ctl.exe") ?? FindInSystemPath("psql.exe");
            }
            else if (string.Equals(pkg.Id, "python", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = FindInSystemPath("python.exe");
                // Ignore the Windows Store alias if it points to WindowsApps stub without actual python
                if (!string.IsNullOrWhiteSpace(candidate) && !candidate.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                {
                    foundInPath = candidate;
                }
            }
            else if (string.Equals(pkg.Id, "heidisql", StringComparison.OrdinalIgnoreCase))
            {
                foundInPath = FindInSystemPath("heidisql.exe")
                    ?? (File.Exists(@"C:\Program Files\HeidiSQL\heidisql.exe") ? @"C:\Program Files\HeidiSQL\heidisql.exe" : null);
            }
            else
            {
                var exeOnlyName = Path.GetFileName(pkg.ExecutableFileName);
                foundInPath = FindInSystemPath(exeOnlyName);
            }

            if (foundInPath is not null)
            {
                pkg.IsAvailable = true;
                pkg.IsFromSystemPath = true;
                pkg.DetectedExecutablePath = foundInPath;
                pkg.DetectedVersion = TryGetFileVersion(foundInPath);

                if (pkg.AvailableVersions.Count > 0)
                {
                    var matched = pkg.AvailableVersions.FirstOrDefault(opt => IsVersionOptionMatch(pkg, opt));
                    if (matched is not null)
                    {
                        pkg.SelectedDownloadVersion = matched;
                    }
                }
            }
            else
            {
                pkg.IsAvailable = false;
                pkg.IsFromSystemPath = false;
                pkg.DetectedExecutablePath = null;
                pkg.DetectedVersion = null;
            }

            UpdatePackageVersionSelectionState(pkg);
        }
    }

    public static bool IsVersionOptionMatch(RuntimePackageInfo pkg, PackageVersionOption opt)
    {
        if (!string.IsNullOrWhiteSpace(pkg.TargetRuntimeFolder) && Directory.Exists(pkg.TargetRuntimeFolder))
        {
            // 1a. Cek target subfolder spesifik
            if (!string.IsNullOrWhiteSpace(opt.TargetSubFolder))
            {
                var subPath = Path.Combine(pkg.TargetRuntimeFolder, opt.TargetSubFolder);
                if (Directory.Exists(subPath))
                {
                    var candidate = FindExecutableInRuntimeFolder(subPath, Path.GetFileName(pkg.ExecutableFileName));
                    if (candidate is not null && File.Exists(candidate))
                    {
                        return true;
                    }
                }
            }

            // 1b. Cek subdirektori apapun yang mengandung target subfolder atau version number
            var targetKeys = new[] { opt.TargetSubFolder, opt.VersionNumber }
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct();

            try
            {
                foreach (var subDir in Directory.GetDirectories(pkg.TargetRuntimeFolder))
                {
                    var dirName = Path.GetFileName(subDir);
                    foreach (var key in targetKeys)
                    {
                        if (dirName.Contains(key!, StringComparison.OrdinalIgnoreCase))
                        {
                            var candidate = FindExecutableInRuntimeFolder(subDir, Path.GetFileName(pkg.ExecutableFileName));
                            if (candidate is not null && File.Exists(candidate))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        // 2. Cek apakah versi yang terdeteksi aktif cocok dengan VersionNumber
        if (!string.IsNullOrWhiteSpace(pkg.DetectedVersion) && !string.IsNullOrWhiteSpace(opt.VersionNumber))
        {
            var cleanDetected = pkg.DetectedVersion.TrimStart('v', 'V');
            var cleanTarget = opt.VersionNumber.TrimStart('v', 'V');

            // 2a. StartsWith / Contains
            if (cleanDetected.StartsWith(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                cleanTarget.StartsWith(cleanDetected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2b. Major.Minor match (contoh: target 8.3 cocok dengan 8.3.36 atau 8.3.16)
            var detParts = cleanDetected.Split('.');
            var tgtParts = cleanTarget.Split('.');
            if (detParts.Length >= 2 && tgtParts.Length >= 2)
            {
                if (string.Equals(detParts[0], tgtParts[0], StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(detParts[1], tgtParts[1], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // 3. Cek apakah DetectedExecutablePath mengandung TargetSubFolder atau VersionNumber
        if (!string.IsNullOrWhiteSpace(pkg.DetectedExecutablePath))
        {
            if (!string.IsNullOrWhiteSpace(opt.TargetSubFolder) &&
                pkg.DetectedExecutablePath.Contains(opt.TargetSubFolder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(opt.VersionNumber) &&
                pkg.DetectedExecutablePath.Contains(opt.VersionNumber, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void UpdatePackageVersionSelectionState(RuntimePackageInfo pkg)
    {
        if (pkg.SelectedDownloadVersion is null && pkg.AvailableVersions.Count > 0)
        {
            pkg.SelectedDownloadVersion = pkg.AvailableVersions.FirstOrDefault(v => v.IsRecommended) ?? pkg.AvailableVersions[0];
        }

        if (!pkg.IsAvailable)
        {
            pkg.IsSelectedVersionInstalled = false;
            return;
        }

        if (pkg.SelectedDownloadVersion is null)
        {
            pkg.IsSelectedVersionInstalled = true;
            return;
        }

        if (IsVersionOptionMatch(pkg, pkg.SelectedDownloadVersion))
        {
            pkg.IsSelectedVersionInstalled = true;
            return;
        }

        // 3. Hanya jika paket adalah single-version (AvailableVersions <= 1) dan terpasang
        if (pkg.AvailableVersions.Count <= 1 && pkg.IsAvailable)
        {
            pkg.IsSelectedVersionInstalled = true;
            return;
        }

        pkg.IsSelectedVersionInstalled = false;
    }

    public static string? FindExecutableInRuntimeFolder(string packageFolder, string relativeExePath)
    {
        if (!Directory.Exists(packageFolder))
        {
            return null;
        }

        var directCandidate = Path.Combine(packageFolder, relativeExePath);
        if (File.Exists(directCandidate))
        {
            return directCandidate;
        }

        try
        {
            foreach (var subDir in Directory.GetDirectories(packageFolder))
            {
                var nestedCandidate = Path.Combine(subDir, relativeExePath);
                if (File.Exists(nestedCandidate))
                {
                    return nestedCandidate;
                }

                foreach (var secondLevel in Directory.GetDirectories(subDir))
                {
                    var deepCandidate = Path.Combine(secondLevel, relativeExePath);
                    if (File.Exists(deepCandidate))
                    {
                        return deepCandidate;
                    }
                }
            }
        }
        catch
        {
            // Ignore directory access error
        }

        return null;
    }

    public static string? FindInSystemPath(string executableName)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return null;
        }

        var dirs = pathVariable.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var dir in dirs)
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                var candidate = Path.Combine(dir, executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore path access error
            }
        }

        return null;
    }

    private static string? TryGetFileVersion(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.ProductVersion))
            {
                return info.ProductVersion.Trim();
            }

            if (!string.IsNullOrWhiteSpace(info.FileVersion))
            {
                return info.FileVersion.Trim();
            }
        }
        catch
        {
            // Ignore file version read error
        }

        return null;
    }
}
