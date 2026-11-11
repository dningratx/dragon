# DraGon v1.1.0 — Rilis Pembaruan Aplikasi & Patch Inkremental (Delta Update)

> **Tanggal Rilis:** 11 November 2026  
> **Versi:** `v1.1.0`  
> **Arsitektur:** Windows x64 (`win-x64`)  
> **Target Framework:** .NET 10 (LTS)

---

## 🌟 Sorotan Pembaruan v1.1.0

### 1. Tombol & Sistem Pembaruan Cerdas (Auto-Update)
- Menambahkan tombol **"🔄 Periksa Pembaruan"** di menu **Pengaturan (Settings)**.
- Banner notifikasi otomatis di Dashboard jika rilis versi baru terdeteksi di server.
- Opsi otomatisasi: *"Periksa pembaruan secara otomatis saat DraGon dibuka"*.
- Dukungan custom update feed URL untuk konfigurasi server rilis internal/enterprise.

### 2. Teknologi Patch Inkremental (Delta Update)
- **Hanya Unduh File yang Berubah:** Saat ada perbaikan kecil (*bugfix* / *hotfix* / *minor update*), pengguna **hanya mengunduh berkas patch berukuran ~300 KB**, bukan seluruh aplikasi (50–100+ MB).
- **Keamanan Data & Runtime 100% Terjamin:**
  - Seluruh paket runtime (**PHP 8.0–8.5, MariaDB, Apache, Nginx, Node.js, Composer, phpMyAdmin**) **TIDAK PERLU diunduh ulang**.
  - Database lokal, sertifikat SSL lokal (`.dra`), dan folder proyek web pengguna (`www`) tetap utuh tanpa risiko terhapus atau tertimpa.
- **Verifikasi Keamanan SHA-256:** Memastikan integritas berkas patch sebelum diekstrak ke sistem.
- **Tombol Simulasi "Demo Update Kecil":** Memungkinkan pengembang dan pengguna menguji alur unduh patch dan penggantian berkas secara instan tanpa perlu ketergantungan koneksi server eksternal.

### 3. Otomatisasi Pipeline Rilis & Distribusi
- Skrip [`scripts/publish-patch.ps1`](file:///c:/Users/odan/Documents/DRAGON/scripts/publish-patch.ps1) untuk menghasilkan paket patch inkremental (`DraGon-Patch-v*.zip`) dan file `update-manifest.json`.
- Skrip [`scripts/build-installer.ps1`](file:///c:/Users/odan/Documents/DRAGON/scripts/build-installer.ps1) untuk membangun wizard installer mandiri GUI `DraGon-Setup.exe`.
- Skrip [`scripts/publish-release.ps1`](file:///c:/Users/odan/Documents/DRAGON/scripts/publish-release.ps1) untuk membangun arsip distribusi portabel `DraGon-win-x64.zip`.

---

## 📦 Berkas Distribusi Rilis (Release Assets)

| Berkas | Tipe Paket | Deskripsi & Kegunaan |
| :--- | :--- | :--- |
| **`DraGon-Setup.exe`** | **Full Installer** | Installer GUI mandiri untuk instalasi baru di komputer Windows. |
| **`DraGon-win-x64.zip`** | **Portable ZIP** | Paket arsip portabel siap ekstrak tanpa instalasi registry. |
| **`DraGon-Patch-v1.1.0.zip`** | **Delta / Patch** | Paket pembaruan kecil (~300 KB) untuk pengguna versi sebelumnya. |
| **`update-manifest.json`** | **Update Feed** | Metadata manifes untuk endpoint update server DraGon. |

---

## 🚀 Panduan Memasang Pembaruan

### Opsi A: Melalui Aplikasi DraGon Langsung (Sangat Direkomendasikan)
1. Buka aplikasi **DraGon**.
2. Masuk ke menu **Pengaturan (Settings)**.
3. Gulir ke kartu **"5. Pembaruan Aplikasi & Patch Inkremental"**.
4. Klik **"🔄 Periksa Pembaruan"**.
5. Jika versi baru ditemukan, klik tombol hijau **"⚡ Pasang Update Kecil (Patch)"**.
6. Aplikasi akan mengunduh patch dan me-restart secara otomatis dengan versi terbaru!

### Opsi B: Instalasi Baru Menggunakan Installer
1. Unduh **`DraGon-Setup.exe`**.
2. Jalankan `DraGon-Setup.exe`.
3. Ikuti langkah wizard instalasi ke folder tujuan (default `C:\DraGon`).
4. Klik **Pasang Sekarang** hingga selesai.
