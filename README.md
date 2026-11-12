# DraGon — Lingkungan Development Lokal untuk Windows (`.dra`)

**DraGon** adalah aplikasi desktop Windows modern untuk mengelola lingkungan pengembangan web lokal (khususnya proyek **PHP**, **Laravel**, dan **Node.js**) dengan dukungan domain lokal berakhiran **`.dra`** (seperti `tokoku.dra`, `iklas.dra`, `api.dralogy.dra`) baik melalui protokol **HTTP** maupun **HTTPS Lokal**.

> **Catatan Penting Domain `.dra`:**  
> Seluruh domain berakhiran `.dra` hanya dipetakan ke alamat loopback lokal (`127.0.0.1`) pada komputer Anda sendiri. Domain `.dra` **tidak** dapat diakses secara publik melalui internet.

---

## 1. Keputusan Teknis & Arsitektur

Aplikasi dibangun menggunakan **.NET 10 (LTS)**, **C#**, **WPF**, dan pola arsitektur **MVVM (`CommunityToolkit.Mvvm`)**:

```text
DRAGON/
├── DraGon.slnx                 # Solution file (.NET 10)
├── DraGon.Core/                # Logika bisnis, layanan proses, SSL lokal, Multi-PHP, & auto-update
│   ├── Models/                 # AppSettings, ProjectItem, ServiceState, RuntimePackageInfo, UpdateInfo
│   └── Services/               # DraGonPaths, ValidationService, AppLogger, ConfigurationService,
│                               # ProjectDetectorService, HostsFileService, LocalSslService,
│                               # PhpVersionManagerService, RuntimeInstallerService, AppUpdateService,
│                               # ProjectDevProcessManager, PortUtility, ServerConfigGenerator,
│                               # RuntimePackageCatalogService, ServiceProcessManager, ExternalLauncherService
├── DraGon.Elevator/            # Helper CLI ringan untuk meminta elevasi UAC (runas) hanya saat
│                               # memperbarui entri # [DraGon-Managed] pada file hosts Windows
├── DraGon.App/                 # Aplikasi Desktop WPF (MVVM, Dark & Light Theme, Auto-Updater)
│   ├── Converters/             # Value converter status layanan, domain, notifikasi, & navigasi
│   ├── Themes/                 # ThemeManager (Tema Gelap default & Tema Terang)
│   ├── ViewModels/             # MainViewModel
│   └── MainWindow.xaml         # Antarmuka Dashboard, Projects, Services, Packages, Settings (Card Update)
├── DraGon.Installer/           # Wizard Installer Mandiri GUI (DraGon-Setup.exe)
├── DraGon.Tests/               # Pengujian unit otomatis 53 skenario (xUnit)
└── scripts/
    ├── publish-release.ps1     # Skrip pembuat paket distribusi rilis portabel (ZIP)
    ├── build-installer.ps1     # Skrip pembuat standalone installer GUI (DraGon-Setup.exe)
    └── publish-patch.ps1       # Skrip pembuat paket patch inkremental (Delta Update ZIP & Manifest)
```

---

## 2. Fitur Lengkap yang Sudah Berfungsi

### A. Fondasi MVP
1. **Dashboard**: Status layanan Web Server (Nginx/Apache), PHP FastCGI, dan Database (MariaDB/MySQL), tombol **Start All** / **Stop All**, kontrol per-layanan, informasi port aktif, daftar proyek terbaru, dan tombol cepat membuka folder, terminal, serta browser.
2. **Pengelolaan Proyek & Deteksi Laravel/Node.js**: Tambah proyek melalui dialog pilih folder, pindai otomatis folder workspace, ubah nama proyek & domain `.dra`, hapus proyek dari daftar tanpa menghapus folder sumber di disk, deteksi proyek Laravel (`artisan` + `composer.json`) dan Node.js (`package.json`), serta tombol pintasan `php artisan serve`, `composer install`, dan `npm install`.
3. **Domain Lokal `.dra` & Helper UAC**: Validasi domain `.dra`, pemeriksaan konflik terhadap proyek lain dan entri eksternal di `hosts`, pembuatan salinan cadangan otomatis di `%LocalAppData%\DraGon\backups\`, penandaan baris `# [DraGon-Managed]`, serta elevasi UAC on-demand melalui `DraGon.Elevator.exe`.

### B. Fitur Tahap Lanjutan (Roadmap) yang Telah Diimplementasikan
1. **HTTPS Lokal Otomatis untuk Domain `.dra` ([`LocalSslService`](DraGon.Core/Services/LocalSslService.cs))**:
   - Membuat sertifikat **DraGon Local Development Root CA** secara native menggunakan kriptografi X.509 .NET 10 tanpa bergantung pada OpenSSL eksternal.
   - Menerbitkan sertifikat SSL leaf per-domain `.dra` (`*.crt` dan `*.key` dengan ekstensi SAN untuk `<domain>.dra`, `*.<domain>.dra`, dan `127.0.0.1`) di `%LocalAppData%\DraGon\certs\`.
   - Menyediakan tombol **"Percayai Root CA di Windows"** untuk mendaftarkan Root CA ke penyimpanan sertifikat pengguna (`CurrentUser\Root`) tanpa membutuhkan hak Administrator penuh.
   - Mengonfigurasi blok HTTPS (`listen 127.0.0.1:443 ssl` di Nginx dan `<VirtualHost 127.0.0.1:8443>` dengan `SSLEngine on` di Apache) secara otomatis.
2. **Dukungan Multi-Versi PHP ([`PhpVersionManagerService`](DraGon.Core/Services/PhpVersionManagerService.cs))**:
   - Memindai beberapa versi PHP secara berdampingan di dalam `%LocalAppData%\DraGon\runtimes\php\<versi>` maupun PATH sistem.
   - Menyediakan pemilih **PHP Version Switcher** di halaman **Services** untuk mengganti versi PHP aktif kapan saja.
   - Menyiapkan `php.ini` otomatis (dari `php.ini-development`) dengan mengaktifkan ekstensi penting untuk Laravel (`curl`, `fileinfo`, `mbstring`, `openssl`, `pdo_mysql`, `mysqli`, `zip`).
3. **Dukungan Node.js & Proses Dev Latar Belakang ([`ProjectDevProcessManager`](DraGon.Core/Services/ProjectDevProcessManager.cs))**:
   - Menjalankan dan menghentikan proses `npm run dev` (Vite / frontend) atau `php artisan serve` sebagai proses latar belakang terkelola langsung pada kartu proyek, lengkap dengan pemantauan PID, pencatatan log ke `%LocalAppData%\DraGon\logs\project-<domain>.log`, dan penghentian bersih saat aplikasi ditutup.
4. **Pengunduh & Importer Arsip Runtime Resmi + Skrip Distribusi ([`RuntimeInstallerService`](DraGon.Core/Services/RuntimeInstallerService.cs))**:
   - Tombol **"Unduh & Pasang Resmi"** untuk mengunduh langsung paket ZIP resmi (Nginx, MariaDB, Node.js LTS) lengkap dengan indikator persentase unduhan dan ekstraksi otomatis.
   - Tombol **"Impor File ZIP Resmi..."** untuk mengekstrak arsip `.zip` resmi apa pun (termasuk menambahkan versi PHP baru secara berdampingan) dan merapikan folder bersarang secara otomatis.
   - Tombol **"Inisialisasi Database MariaDB"** yang menjalankan `mysql_install_db.exe` secara otomatis jika folder `data` belum dibuat.
   - Skrip distribusi `scripts/publish-release.ps1` untuk membangun paket rilis `dist/DraGon-win-x64.zip`.
5. **Sistem Pembaruan Otomatis & Patch Inkremental ([`AppUpdateService`](file:///c:/Users/odan/Documents/DRAGON/DraGon.Core/Services/AppUpdateService.cs))**:
   - Mendukung **Pembaruan Patch / Delta Kecil**: Jika hanya ada update minor atau perbaikan bug, aplikasi hanya mengunduh berkas yang mengalami perubahan (biasanya hanya ratusan KB), tanpa mengunduh ulang seluruh aplikasi maupun runtime (PHP, MariaDB, Nginx, Node.js).
   - Tombol **"Periksa Pembaruan"** di menu Pengaturan dan Banner Peringatan otomatis jika versi rilis baru terdeteksi.
   - Tombol simulasi **"Demo Update Kecil"** untuk menguji alur unduh patch dan penggantian berkas tanpa ketergantungan server publik.
   - Skrip pembuat paket patch [`scripts/publish-patch.ps1`](file:///c:/Users/odan/Documents/DRAGON/scripts/publish-patch.ps1) yang otomatis menghasilkan berkas zip patch dan manifes `update-manifest.json` lengkap dengan hash SHA-256.

6. **Sistem Net Installer Modular & Komponen On-Demand ([`InstallerEngine`](DraGon.Installer/InstallerEngine.cs))**:
   - **Installer Awal Ramping Tanpa Runtime Bawaan:** File installer tidak lagi memuat file komponen berat di awal, sehingga ukuran download installer awal sangat kecil.
   - **Pemilihan Komponen Interaktif di Wizard:** Pengguna bebas memilih komponen yang ingin diinstal (Apache 2.4, Nginx 1.26, PHP 8.3 LTS, PHP 8.4, MariaDB 11.4 LTS, PostgreSQL 17, Composer, Node.js 22 LTS, phpMyAdmin, Python 3.12) dengan tombol preset 1-klik (*Standar Apache*, *Stack Nginx*, *Pilih Semua*, atau *Hanya Inti DraGon*).
   - **Estimasi Unduhan Real-Time:** Menghitung total MB perkiraan unduhan secara langsung saat checkbox dipilih atau diubah.
   - **Unduhan Otomatis & Konfigurasi Cerdas:** Mengunduh arsip langsung dari repositori resmi masing-masing vendor, mengekstrak, menginisialisasi database, mengonfigurasi ekstensi `php.ini`, dan otomatis menyesuaikan web server utama serta mendaftarkan path ke Windows PATH.
   - **Dukungan Build Online & Offline:** Script build mendukung pembuatan Net Installer (`-Mode Online`, default) maupun Offline Installer (`-Mode Offline`).

---

## 3. Lokasi Konfigurasi, Sertifikat SSL, dan Log

| Jenis Data | Lokasi Path Default |
| :--- | :--- |
| **Folder Data Utama** | `%LocalAppData%\DraGon` (`C:\Users\<User>\AppData\Local\DraGon`) |
| **File Pengaturan** | `%LocalAppData%\DraGon\config\settings.json` |
| **Daftar Proyek** | `%LocalAppData%\DraGon\config\projects.json` |
| **Virtual Host Otomatis** | `%LocalAppData%\DraGon\config\generated\nginx` & `apache` |
| **Sertifikat SSL Lokal (`.dra`)** | `%LocalAppData%\DraGon\certs\` (`DraGon-Local-RootCA.crt`, `<domain>.dra.crt`, `<domain>.dra.key`) |
| **Log Aplikasi & Layanan** | `%LocalAppData%\DraGon\logs\dragon.log`, `service-*.log`, `project-*.log` |
| **Cadangan File `hosts`** | `%LocalAppData%\DraGon\backups\hosts_yyyyMMdd_HHmmss_fff.bak` |
| **Folder Runtime Default** | `%LocalAppData%\DraGon\runtimes\` (`nginx`, `php`, `mariadb`, `apache`, `nodejs`, `composer`) |

---

## 4. Cara Menjalankan, Membangun Installer, & Memaketkan Rilis

```powershell
# 1. Membangun seluruh solution
dotnet build DraGon.slnx

# 2. Menjalankan seluruh pengujian unit otomatis (53 pengujian)
dotnet test DraGon.slnx

# 3. Menjalankan aplikasi desktop DraGon
dotnet run --project DraGon.App/DraGon.App.csproj

# 4. Membangun Net Installer Mandiri GUI Ramping (dist/DraGon-Setup.exe & dist/DraGon-WebSetup.exe)
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Mode Online

# (Opsional) Membangun Full Offline Installer Bundle
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Mode Offline

# 5. Membuat paket distribusi portabel ZIP (dist/DraGon-win-x64.zip)
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1

# 6. Membangun paket patch inkremental delta (dist/patch/DraGon-Patch-v1.2.0.zip & manifest)
powershell -ExecutionPolicy Bypass -File .\scripts\publish-patch.ps1 -Version "1.2.0" -Changelog "Pembaruan arsitektur Net Installer modular tanpa komponen bawaan dan pemilihan unduhan on-demand."
```

---

## 5. Dokumentasi Rilis & Manifes Pembaruan

Informasi lengkap mengenai rilis versi, catatan perubahan, dan aset distribusi dapat dibaca pada:
- [`RELEASE.md`](RELEASE.md) — Panduan rilis resmi v1.2.0 dan rincian paket distribusi.
- [`dist/patch/update-manifest.json`](dist/patch/update-manifest.json) — Format manifes pembaruan yang digunakan oleh layanan `AppUpdateService`.
