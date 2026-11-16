# DraGon v1.3.0 — Public Tunnel / Share 1-Klik (`cloudflared`) & Net Installer Modular

> **Tanggal Rilis:** 16 November 2026  
> **Versi:** `v1.3.0`  
> **Arsitektur:** Windows x64 (`win-x64`)  
> **Target Framework:** .NET 10 (LTS)

---

## 🌟 Sorotan Pembaruan v1.3.0

### Fitur Baru: Public Tunnel / Share 1-Klik (Cloudflare Tunnel)
- **Bagikan Situs `.dra` ke Internet Sementara:** Klik tombol **`🌐 Share`** di kartu proyek untuk mengekspos virtual host `.dra` lokal ke URL publik `https://*.trycloudflare.com` tanpa perlu akun Cloudflare.
- **Integrasi On-Demand `cloudflared.exe`:** Paket `cloudflared` tersedia di menu **Packages** dan dapat diunduh otomatis (~35 MB) saat Anda menekan **`🌐 Share`** untuk pertama kali.
- **Otomatisasi Alur Kerja:** Otomatis menyalakan Web Server (Nginx/Apache) & PHP-CGI jika belum aktif, menampilkan bar URL publik di kartu proyek lengkap dengan tombol **`📋 Salin`** dan **`↗ Buka`**, serta menyalin URL langsung ke clipboard.
- **Aman & Terkelola:** Proses tunnel berjalan di latar belakang per proyek dan otomatis dibersihkan saat menekan **`⏹ Stop Share`**, **Stop All Services**, atau saat aplikasi ditutup.

### Perbaikan & Fitur Sebelumnya (v1.2.x)
- **Sinkronisasi Portal `localhost` (`index.php` Root Workspace):** Memperbaiki sinkronisasi daftar proyek lokal (`.dra`) antara root `localhost` dan `localhost/welcome/` sambil melindungi file `index.php` kustom pengguna.

### Sorotan Fitur v1.2.x (Net Installer Modular)

### 1. Arsitektur Net Installer (Web Installer) Ramping
- **Tanpa Komponen Bawaan Berat di Awal:** File installer kini dibuat ramping tanpa membundel ratusan MB berkas runtime lokal di dalam payload.
- **Ukuran Installer Ringan & Bersih:** Installer hanya mengemas binary inti aplikasi (`DraGon.App.exe` dan `DraGon.Elevator.exe`).
- **Selalu Mendapatkan Versi Resmi Segar:** Komponen diunduh langsung dari server vendor resmi saat instalasi (windows.php.net, apachelounge.com, mariadb.org, getcomposer.org, nodejs.org, python.org, enterprisedb.com).

### 2. Antarmuka Pemilihan Komponen Interaktif di Wizard Instalasi
- **Katalog Pilihan Komponen Lengkap dengan Badge Ukuran:**
  - **Web Server:** Apache HTTP Server 2.4 (~15 MB) & Nginx Web Server 1.26 (~2 MB).
  - **PHP FastCGI:** PHP 8.3 LTS (~35 MB) & PHP 8.4 Terbaru (~35 MB).
  - **Database:** MariaDB Server 11.4 LTS (~95 MB) & PostgreSQL Server 17 (~315 MB).
  - **Manajer Paket & Tooling:** Composer (~4 MB) & Node.js 22 LTS + NPM (~35 MB).
  - **Antarmuka & Bahasa Lain:** phpMyAdmin Web GUI (~16 MB) & Python 3.12 Embeddable (~11 MB).
- **Tombol Preset 1-Klik:**
  - `Standar (Apache)`: Memilih Apache, PHP 8.3 LTS, MariaDB 11.4 LTS, Composer, Node.js 22 LTS, dan phpMyAdmin.
  - `Stack Nginx`: Memilih Nginx, PHP 8.3 LTS, MariaDB 11.4 LTS, Composer, Node.js 22 LTS, dan phpMyAdmin.
  - `Pilih Semua`: Memilih seluruh komponen untuk pengembang full-stack.
  - `Hanya Inti DraGon`: Pemasangan minimal hanya core DraGon tanpa komponen runtime.
- **Kalkulator Estimasi Ukuran Unduhan Real-Time:** Menghitung total MB perkiraan unduhan secara langsung saat checkbox dipilih atau diubah.

### 3. Otomatisasi Instalasi & Integrasi Sistem Operasi
- **Pelacakan Unduhan Asinkron & Ekstraksi:** Menampilkan progres byte per detik, persentase unduh, dan proses ekstraksi secara transparan.
- **Inisialisasi & Konfigurasi Cerdas Otomatis:**
  - Otomatis mengaktifkan ekstensi populer pada `php.ini` (`curl`, `fileinfo`, `gd`, `mbstring`, `mysqli`, `openssl`, `pdo_mysql`, `zip`).
  - Menjalankan `mariadb-install-db` secara otomatis untuk penyiapan direktori database dan pengguna root.
  - Menyesuaikan pengaturan `preferredWebServer` di `settings.json` secara otomatis sesuai pilihan server pengguna.
  - Mendaftarkan direktori komponen runtime ke **Windows User PATH** agar `php`, `composer`, dan `node` langsung dapat dipanggil di Command Prompt / PowerShell.

### 4. Skrip Build Installer Fleksibel (`scripts/build-installer.ps1`)
- Mendukung parameter `-Mode Online` (default, menghasilkan Net Installer ramping).
- Mendukung parameter `-Mode Offline` (menghasilkan full offline bundle installer bagi kebutuhan offline PC).

---

## 📦 Berkas Distribusi Rilis (Release Assets)

| Berkas | Tipe Paket | Deskripsi & Kegunaan |
| :--- | :--- | :--- |
| **`DraGon-Setup.exe`** / **`DraGon-WebSetup.exe`** | **Net Installer (Online)** | Installer GUI mandiri ramping dengan pilihan unduhan komponen on-demand. |
| **`DraGon-win-x64.zip`** | **Portable ZIP** | Paket arsip portabel siap ekstrak tanpa instalasi registry. |
| **`DraGon-Patch-v1.3.0.zip`** | **Delta / Patch** | Paket pembaruan kecil untuk pengguna versi sebelumnya (v1.2.x/v1.1.0/v1.0.0). |
| **`update-manifest.json`** | **Update Feed** | Metadata manifes untuk endpoint update server DraGon. |

---

## 🚀 Panduan Memasang Pembaruan

### Opsi A: Menggunakan Net Installer (Rekomendasi untuk Instalasi Baru)
1. Unduh **`DraGon-Setup.exe`** (atau **`DraGon-WebSetup.exe`**).
2. Jalankan file setup wizard.
3. Tentukan folder aplikasi dan folder workspace proyek.
4. Pilih komponen server, PHP, database, atau tools yang diinginkan.
5. Klik **Pasang DraGon Sekarang (Install)**. Komponen akan otomatis diunduh dan dikonfigurasi hingga selesai!

### Opsi B: Melalui Tombol Update di Aplikasi DraGon
1. Buka aplikasi **DraGon**.
2. Masuk ke menu **Pengaturan (Settings)**.
3. Gulir ke bagian **"5. Pembaruan Aplikasi & Patch Inkremental"**.
4. Klik **"🔄 Periksa Pembaruan"** dan klik **"⚡ Pasang Update Kecil (Patch)"**.
