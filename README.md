<div align="center">

# DraGon (.dra) — Local Development Environment

[![Release](https://img.shields.io/badge/Release-v1.2.0-orange.svg)](https://github.com/dningratx/dragon/releases)
[![Website](https://img.shields.io/badge/Website-dningratx.github.io%2Fdragon-success.svg)](https://dningratx.github.io/dragon/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-blue.svg)](https://github.com/dningratx/dragon)
[![Framework](https://img.shields.io/badge/.NET-10.0%20Windows-512bd4.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Freeware-emerald.svg)](https://github.com/dningratx/dragon)

<br/>

**DraGon** adalah lingkungan pengembangan web lokal (*local development environment*) modern untuk sistem operasi Windows yang dirancang sebagai alternatif ringan, cepat, modular, dan terintegrasi penuh untuk mengelola proyek **PHP**, **Laravel**, dan **Node.js** dengan dukungan domain lokal **`.dra`** (seperti `tokoku.dra`, `api.dra`) melalui HTTP dan HTTPS lokal.

> **Catatan Penting Domain `.dra`:**  
> Seluruh domain berakhiran `.dra` hanya dipetakan ke alamat loopback lokal (`127.0.0.1`) pada komputer Anda. Domain `.dra` **tidak** dapat diakses secara publik melalui internet.

</div>

---

## 🚀 Unduh DraGon v1.2.0 Terbaru

Dapatkan installer dan paket rilis resmi langsung dari GitHub Releases:

| Paket Distribusi | Tipe | Deskripsi | Tautan Unduh |
| :--- | :--- | :--- | :--- |
| **DraGon Setup** | **Net Installer** | Installer GUI mandiri ramping tanpa runtime bawaan di awal. Pilih dan unduh komponen on-demand saat instalasi. | [⬇️ **Unduh DraGon-Setup.exe**](https://github.com/dningratx/dragon/releases/download/v1.2.0/DraGon-Setup.exe) |
| **DraGon Portable** | **ZIP Archive** | Versi portabel siap ekstrak tanpa instalasi registry Windows. | [📦 **Unduh DraGon-win-x64.zip**](https://github.com/dningratx/dragon/releases/download/v1.2.0/DraGon-win-x64.zip) |
| **DraGon Patch** | **Delta Update** | Paket pembaruan inkremental ringan untuk pengguna versi sebelumnya via Auto-Updater di aplikasi. | [⚡ **DraGon-Patch-v1.2.0.zip**](https://github.com/dningratx/dragon/releases/download/v1.2.0/DraGon-Patch-v1.2.0.zip) |

---

## 🌟 Fitur Unggulan

### 1. 🌐 Net Installer Modular & Komponen On-Demand
- **Installer Awal Ramping Tanpa Runtime Bawaan:** File installer tidak lagi memuat berkas komponen berat di awal, sehingga proses unduh awal sangat cepat.
- **Pilihan Komponen Fleksibel:** Pengguna bebas memilih komponen yang diinginkan (Apache 2.4, Nginx 1.26, PHP 8.3 LTS, PHP 8.4, MariaDB 11.4 LTS, PostgreSQL 17, Composer, Node.js 22 LTS, phpMyAdmin, Python 3.12).
- **Preset 1-Klik:** Pilihan cepat *Standar Apache*, *Stack Nginx*, *Pilih Semua*, atau *Hanya Inti DraGon*.
- **Estimasi Unduhan Real-Time:** Menghitung total MB perkiraan unduhan secara langsung saat checkbox dipilih atau diubah.

### 2. 🐘 Multi-Versi PHP Berdampingan
- Beralih versi PHP aktif secara instan hanya dengan 1 klik melalui *PHP Version Switcher*.
- Mendukung preset rilis **PHP 8.3 LTS** (Rekomendasi Laravel), **PHP 8.4** (Terbaru), dan **PHP 8.2**.
- Otomatis mengaktifkan ekstensi populer (`curl`, `fileinfo`, `gd`, `mbstring`, `mysqli`, `openssl`, `pdo_mysql`, `zip`).

### 3. 🐬 Database MariaDB 11.4 LTS & PostgreSQL
- Server database relasional SQL lokal cepat, otomatis inisialisasi folder data dan pengguna `root`.
- Integrasi tombol pintasan langsung ke **phpMyAdmin Web GUI** dan **HeidiSQL**.

### 4. 🔒 Otomatisasi SSL Lokal (`https://*.dra`)
- DraGon Local Root CA diterbitkan secara native menggunakan kriptografi X.509 .NET 10 tanpa OpenSSL eksternal.
- Sertifikat leaf otomatis dibuat per domain `.dra`.
- Tombol **"Percayai Root CA di Windows"** untuk mendaftarkan Root CA ke penyimpanan sertifikat pengguna (`CurrentUser\Root`).

### 5. ⚡ Deteksi & Scaffolding Proyek Instan
- Template bawaan siap pakai: **Standar (PHP/Web)**, **Laravel** (`composer create-project`), **WordPress** (unduh resmi `wordpress.org`), dan **Git Clone**.
- Tombol cepat untuk `php artisan serve`, `composer install`, dan `npm run dev` yang berjalan sebagai proses latar belakang terkelola.

### 6. 🔄 Sistem Pembaruan Cerdas (Delta Update)
- Fitur **"Periksa Pembaruan"** di menu Pengaturan.
- Hanya mengunduh file patch yang berubah tanpa perlu mengunduh ulang runtime maupun merusak data database lokal.

---

## 🛠️ Keputusan Teknis & Struktur Proyek

Aplikasi dibangun menggunakan **.NET 10 (LTS)**, **C#**, **WPF**, dan pola arsitektur **MVVM (`CommunityToolkit.Mvvm`)**:

```text
DRAGON/
├── DraGon.slnx                 # Solution file (.NET 10)
├── DraGon.Core/                # Logika bisnis, layanan proses, SSL lokal, Multi-PHP, & auto-update
├── DraGon.Elevator/            # Helper CLI ringan untuk meminta elevasi UAC (hosts file)
├── DraGon.App/                 # Aplikasi Desktop WPF (MVVM, Dark/Light Theme, Auto-Updater)
├── DraGon.Installer/           # Wizard Installer Mandiri GUI (DraGon-Setup.exe)
├── DraGon.Tests/               # Pengujian unit otomatis 53 skenario (xUnit)
├── docs/                       # Halaman dokumentasi web (GitHub Pages)
└── scripts/
    ├── publish-release.ps1     # Skrip pembuat paket distribusi rilis portabel (ZIP)
    ├── build-installer.ps1     # Skrip pembuat standalone installer GUI (Online/Offline)
    └── publish-patch.ps1       # Skrip pembuat paket patch inkremental (Delta Update)
```

---

## 💻 Panduan Menjalankan & Membangun Proyek

```powershell
# 1. Membangun seluruh solution
dotnet build DraGon.slnx

# 2. Menjalankan seluruh pengujian unit otomatis
dotnet test DraGon.slnx

# 3. Menjalankan aplikasi desktop DraGon
dotnet run --project DraGon.App/DraGon.App.csproj

# 4. Membangun Net Installer Mandiri GUI Ramping (dist/DraGon-Setup.exe)
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Mode Online

# (Opsional) Membangun Full Offline Installer Bundle
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -Mode Offline

# 5. Membuat paket distribusi portabel ZIP (dist/DraGon-win-x64.zip)
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1

# 6. Membangun paket patch inkremental delta
powershell -ExecutionPolicy Bypass -File .\scripts\publish-patch.ps1 -Version "1.2.0" -Changelog "Pembaruan arsitektur Net Installer modular tanpa komponen bawaan dan pemilihan unduhan on-demand."
```

---

## 📄 Dokumentasi Rilis

Informasi lengkap mengenai rilis versi, catatan perubahan, dan aset distribusi dapat dibaca pada:
- [`RELEASE.md`](RELEASE.md) — Panduan rilis resmi v1.2.0 dan rincian paket distribusi.
- [`dist/patch/update-manifest.json`](dist/patch/update-manifest.json) — Format manifes pembaruan aplikasi.
