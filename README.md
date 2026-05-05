# SecureChat — UTS Information Security Assurance

Aplikasi chat berbasis TCP dengan enkripsi end-to-end menggunakan **RSA-2048** dan **AES-256**.  
Dibangun dengan C# WinForms (.NET Framework 4.7.2).

> 📄 **[Lihat Analisis Desain Keamanan Data lengkap → SECURITY_ANALYSIS.md](./SECURITY_ANALYSIS.md)**  
> 📊 **[Lihat Diagram Alur Keamanan (Mermaid) → SECURITY_DIAGRAM.md](./SECURITY_DIAGRAM.md)**

---

## Daftar Isi

1. [Arsitektur Aplikasi](#arsitektur-aplikasi)
2. [Alur Enkripsi](#alur-enkripsi)
3. [Database](#database)
   - [Tabel users](#tabel-users)
   - [Tabel sessions](#tabel-sessions)
   - [Tabel messages](#tabel-messages)
4. [Session Token](#session-token)
5. [Offline Message Delivery](#offline-message-delivery)
6. [Cara Menjalankan](#cara-menjalankan)

---

## Arsitektur Aplikasi

```
┌─────────────────────────────────────────────────────┐
│                   UTS_ISA.exe                       │
│                                                     │
│   ┌─────────────┐         ┌─────────────────────┐  │
│   │  ServerHost  │         │  FormLogin          │  │
│   │  (port 6000) │         │  FormChat           │  │
│   │  background  │         │  FormRegister       │  │
│   │  thread      │         │  (UI Client)        │  │
│   └─────────────┘         └─────────────────────┘  │
│          │                         │                │
│          └─────────── TCP ──────────┘                │
│                    localhost:6000                   │
└─────────────────────────────────────────────────────┘
```

- **Instance pertama** → otomatis jadi server (bind port 6000) sekaligus client
- **Instance berikutnya** → hanya jadi client, konek ke instance pertama
- Satu `.exe` bisa jalankan banyak jendela sekaligus

---

## Alur Enkripsi

### Step 1 — RSA Key Exchange (sekali di awal)

```
Server                              Client
  │                                   │
  │── PUBKEY|<base64 RSA pubkey> ────►│
  │                                   │  generate AES key (32 byte random)
  │                                   │  generate AES IV  (16 byte random)
  │                                   │  enkripsi AES key pakai RSA pubkey
  │                                   │  enkripsi AES IV  pakai RSA pubkey
  │◄── KEY_EXCHANGE|encKey|encIV ─────│
  │                                   │
  │  dekripsi encKey+encIV            │
  │  pakai RSA private key            │
  │  → dapat AES key + IV             │
  │                                   │
  │  ✓ Kedua pihak kini punya         │
  │    AES key + IV yang sama         │
```

### Step 2 — Semua Chat Pakai AES

```
Client A kirim pesan:
  "halo" → AesEncrypt(aesKey_A) → "X7kL9mN2..." → kirim lewat jaringan

Server terima:
  "X7kL9mN2..." → AesDecrypt(aesKey_A) → "halo"
  → simpan ke DB
  → AesEncrypt("halo", aesKey_B) → "R3tY6wZ1..."
  → kirim ke Client B

Client B terima:
  "R3tY6wZ1..." → AesDecrypt(aesKey_B) → "halo" ✓
```

### Triple Cipher (3 Lapis Enkripsi)

| Layer | Lokasi | Key | Tujuan |
|---|---|---|---|
| **Layer 1** | Jaringan (transit) | AES session key (random per sesi) | Cegah penyadapan |
| **Layer 2** | DB kolom `message_encrypted` | AES session key sender | Audit trail, tidak bisa dibuka siapapun |
| **Layer 3** | DB kolom `message_backup` | AES DB key (fixed server key) | Offline delivery, aman dari phpMyAdmin |

---

## Database

### Tabel `users`

Menyimpan data akun semua pengguna.

```sql
CREATE TABLE users (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    username      VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(64) NOT NULL,   -- SHA-256 hash, bukan plaintext
    role          VARCHAR(10) DEFAULT 'user'
);
```

| Kolom | Keterangan |
|---|---|
| `username` | Nama pengguna, unik, tidak boleh duplikat |
| `password_hash` | Password yang sudah di-hash SHA-256 di sisi client sebelum dikirim ke server. Server tidak pernah tahu password aslinya |
| `role` | `user` atau `admin` |

**Contoh isi tabel:**

| id | username | password_hash | role |
|---|---|---|---|
| 1 | alice | ef92b778bafe771e... | user |
| 2 | bob | 5e884898da280471... | admin |

> **Catatan:** Password TIDAK PERNAH disimpan as-is. Yang disimpan adalah hasil SHA-256 dari password. Jadi meskipun DB bocor, password asli tidak bisa diketahui.

---

### Tabel `sessions`

Menyimpan audit trail semua session login.

```sql
CREATE TABLE sessions (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    username   VARCHAR(50) NOT NULL,
    token      VARCHAR(64) NOT NULL,   -- GUID 32 karakter
    issued_at  DATETIME NOT NULL,      -- waktu login
    expires_at DATETIME NOT NULL,      -- issued_at + 1 jam
    is_valid   TINYINT(1) DEFAULT 1    -- 1=aktif, 0=sudah tidak valid
);
```

| Kolom | Keterangan |
|---|---|
| `token` | GUID 32 karakter yang di-generate server saat login. Contoh: `b541001d95b24572...` |
| `issued_at` | Kapan user login |
| `expires_at` | Kapan token mati (1 jam setelah login) |
| `is_valid` | `1` = token masih aktif, `0` = sudah logout/expired/di-refresh |

**Contoh isi tabel:**

| id | username | token | issued_at | expires_at | is_valid |
|---|---|---|---|---|---|
| 1 | a | b541001d95b2... | 2026-04-25 00:20:28 | 2026-04-25 01:20:28 | 0 |
| 2 | a | 94a217ac919b... | 2026-04-25 00:44:01 | 2026-04-25 01:44:01 | 0 |

> **Kenapa `is_valid = 0` semua?** Karena user sudah disconnect. Setiap login baru → token lama dimatikan → token baru dibuat. Tabel ini berfungsi sebagai **audit trail** keamanan.

---

### Tabel `messages`

Menyimpan semua pesan chat.

```sql
CREATE TABLE messages (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    sender            VARCHAR(50) NOT NULL,
    receiver          VARCHAR(50) NOT NULL,
    message_encrypted TEXT,         -- ciphertext AES session key sender (audit trail)
    message_backup    TEXT,         -- plaintext dienkripsi DB key (untuk offline delivery)
    sent_at           DATETIME NOT NULL,
    delivered         TINYINT(1) DEFAULT 0
);
```

| Kolom | Keterangan |
|---|---|
| `message_encrypted` | Ciphertext asli dari sender menggunakan AES session key sender. Tidak bisa dibuka karena key sender hilang setelah sesi berakhir. Fungsi: audit trail |
| `message_backup` | Plaintext yang sudah dienkripsi ulang menggunakan fixed server DB key. Dipakai server untuk mengirim ulang pesan ke recipient yang sedang offline saat pesan dikirim |
| `delivered` | `0` = belum terkirim ke penerima (penerima sedang offline), `1` = sudah diterima |

**Contoh isi tabel:**

| id | sender | receiver | message_encrypted | message_backup | sent_at | delivered |
|---|---|---|---|---|---|---|
| 1 | alice | bob | X7kL9mN2... | P9qR3sT6... | 2026-05-01 19:51:31 | 1 |
| 2 | alice | bob | bB/dtJGDs... | onvvo49L4... | 2026-05-01 19:52:26 | 0 |

**Arti kolom `delivered`:**

```
delivered = 0  →  Pesan menunggu di server
                  Penerima sedang offline saat pesan dikirim
                  Analogi WhatsApp: centang satu (✓)

delivered = 1  →  Pesan sudah sampai ke penerima
                  Penerima online saat pesan dikirim, ATAU
                  Penerima sudah login dan ambil pesan pending
                  Analogi WhatsApp: centang dua (✓✓)
```

---

## Session Token

Token adalah **"kartu akses"** yang wajib disertakan setiap kali client mengirim pesan.

### Alur Token

```
1. User login → server generate token (GUID 32 karakter)
2. Token dikirim ke client: LOGIN_SUCCESS|role|token|expires_at
3. Client simpan token di memori
4. Setiap kirim pesan: CHAT|from|to|encMsg|token
5. Server validasi token:
     ✅ Token cocok + belum expired → proses pesan
     ❌ Token tidak cocok           → ERROR, login ulang
     ❌ Token expired               → ERROR, login ulang
6. Token otomatis di-refresh 2 menit sebelum expired (tanpa login ulang)
7. User disconnect → token dimatikan (is_valid=0 di DB)
```

### Kenapa Perlu Token?

Tanpa token, siapapun bisa kirim pesan palsu dengan format:
```
CHAT|alice|bob|encMsg
```
Dengan token, server bisa memastikan bahwa yang kirim memang alice yang sedang login sah, bukan orang lain yang meniru.

---

## Offline Message Delivery

Ketika penerima sedang offline saat pesan dikirim:

```
Alice kirim ke Bob (Bob sedang offline)
        │
        ▼
Server:
  - Dekripsi pesan pakai AES key Alice → dapat plaintext
  - Enkripsi plaintext pakai DB key → simpan ke message_backup
  - delivered = 0

Bob login keesokan harinya
        │
        ▼
Server:
  - Baca message_backup dari DB
  - Dekripsi pakai DB key → dapat plaintext
  - Enkripsi pakai AES key Bob → kirim ke Bob
  - Update delivered = 1

Bob terima pesan Alice ✓
```

**Kenapa perlu `message_backup`?**
AES key Alice hanya ada saat sesi Alice aktif. Begitu Alice disconnect, key hilang dari memori server. Tanpa `message_backup`, pesan ke penerima offline tidak bisa dikirim ulang selamanya.

---

## Cara Menjalankan

### Prasyarat

- Visual Studio 2019/2022
- MySQL / MariaDB (XAMPP)
- .NET Framework 4.7.2

### Setup Database

```sql
CREATE DATABASE secure_chat;

USE secure_chat;

CREATE TABLE users (
    id            INT AUTO_INCREMENT PRIMARY KEY,
    username      VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(64) NOT NULL,
    role          VARCHAR(10) DEFAULT 'user'
);

CREATE TABLE messages (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    sender            VARCHAR(50) NOT NULL,
    receiver          VARCHAR(50) NOT NULL,
    message_encrypted TEXT,
    message_backup    TEXT,
    sent_at           DATETIME NOT NULL,
    delivered         TINYINT(1) DEFAULT 0
);

-- Tabel sessions dibuat otomatis saat pertama kali ada user login
```

### Jalankan Aplikasi

1. Buka `UTS_ISA.sln` di Visual Studio
2. Pastikan MySQL sudah jalan (XAMPP)
3. Sesuaikan connection string di `ServerHost.cs` dan `DatabaseHelper.cs` jika pakai password MySQL
4. Run (`F5`) → instance pertama otomatis jadi server
5. Run lagi → instance kedua jadi client kedua
6. Register akun → Login → mulai chat

---

## Teknologi yang Digunakan

| Teknologi | Fungsi |
|---|---|
| **RSA-2048** | Key exchange — kirim AES key secara aman dari client ke server |
| **AES-256-CBC** | Enkripsi semua pesan chat (transit + database) |
| **SHA-256** | Hash password sebelum disimpan ke DB |
| **TCP Socket** | Komunikasi client-server real-time |
| **MySQL/MariaDB** | Penyimpanan data user, pesan, dan session |
| **C# WinForms** | UI aplikasi desktop |
