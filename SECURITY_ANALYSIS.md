# Analisis Desain Keamanan Data — SecureChat

> **Mata Kuliah:** Information Security Assurance (ISA)  
> **Aplikasi:** SecureChat — Aplikasi Chat Terenkripsi berbasis TCP  
> **Platform:** C# WinForms (.NET Framework 4.7.2)  

---

## Daftar Isi

1. [Pendahuluan](#pendahuluan)
2. [Desain Keamanan Data](#desain-keamanan-data)
3. [Analisis Pemilihan Algoritma](#analisis-pemilihan-algoritma)
4. [Threat Model (Model Ancaman)](#threat-model-model-ancaman)
5. [Analisis Per Lapisan Keamanan](#analisis-per-lapisan-keamanan)
6. [Kelemahan Sistem](#kelemahan-sistem)
7. [Perbandingan dengan Sistem yang Ada](#perbandingan-dengan-sistem-yang-ada)
8. [Kesimpulan](#kesimpulan)
9. [Referensi](#referensi)

---

## Pendahuluan

Keamanan data pada aplikasi chat merupakan hal kritis karena melibatkan pertukaran informasi pribadi antar pengguna. Ancaman yang umum terjadi meliputi penyadapan jaringan, pencurian data dari database, pemalsuan identitas, hingga serangan replay.

Aplikasi SecureChat dirancang dengan pendekatan **Defense in Depth** (pertahanan berlapis), di mana setiap lapisan memberikan perlindungan yang berbeda sehingga kegagalan satu lapisan tidak langsung mengekspos seluruh sistem.

Menurut NIST SP 800-27 Rev A, desain keamanan yang baik harus memenuhi tiga prinsip utama:
- **Confidentiality** — data hanya bisa dibaca oleh pihak yang berhak
- **Integrity** — data tidak bisa diubah tanpa diketahui
- **Availability** — sistem tetap bisa diakses oleh pengguna yang sah

---

## Desain Keamanan Data

### Arsitektur Keamanan (Triple Cipher)

Aplikasi ini mengimplementasikan **tiga lapisan enkripsi** yang bekerja secara bersamaan:

```
┌───────────────────────────────────────────────────────────────┐
│                    TRIPLE CIPHER ARCHITECTURE                 │
│                                                               │
│  LAYER 1 — Transit Security (AES-256-CBC + RSA-2048)          │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │  Semua pesan di jaringan dienkripsi AES-256             │  │
│  │  AES key ditukar secara aman lewat RSA-2048             │  │
│  │  Proteksi: Man-in-the-Middle, Eavesdropping             │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                               │
│  LAYER 2 — Database Audit Trail (AES session key sender)      │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │  Ciphertext asli sender disimpan di message_encrypted   │  │
│  │  Key sender hilang setelah logout → tidak bisa dibuka   │  │
│  │  Proteksi: Insider threat, DB compromise                │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                               │
│  LAYER 3 — Database Encryption (AES-256 + fixed DB key)       │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │  Plaintext dienkripsi DB key sebelum disimpan           │  │
│  │  Kolom message_backup tidak terbaca di phpMyAdmin       │  │
│  │  Proteksi: Direct DB access, Data breach                │  │
│  └─────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────┘
```

### Komponen Keamanan

| Komponen | Algoritma | Fungsi |
|---|---|---|
| Password Storage | SHA-256 | Hash password sebelum disimpan ke DB |
| Key Exchange | RSA-2048 + OAEP | Tukar AES session key secara aman |
| Transit Encryption | AES-256-CBC | Enkripsi pesan di jaringan |
| Database Encryption | AES-256-CBC | Enkripsi plaintext di kolom message_backup |
| Session Management | GUID Token + Expiry | Validasi identitas per sesi |

---

## Analisis Pemilihan Algoritma

### 1. AES-256-CBC

**Kenapa AES?**

AES (Advanced Encryption Standard) dipilih karena telah distandarisasi oleh NIST sejak 2001 melalui FIPS PUB 197 dan menjadi standar enkripsi simetris yang paling banyak digunakan di dunia. AES terbukti tahan terhadap semua serangan kriptanalisis yang diketahui hingga saat ini.

**Kenapa 256-bit, bukan 128-bit?**

| Key Size | Security Level | Cocok Untuk |
|---|---|---|
| AES-128 | 128-bit security | Aplikasi umum |
| AES-192 | 192-bit security | Aplikasi sensitif |
| AES-256 | 256-bit security | Data sangat sensitif, tahan quantum |

NIST SP 800-57 merekomendasikan AES-256 untuk data yang membutuhkan perlindungan jangka panjang (hingga tahun 2031 ke atas). AES-256 juga lebih tahan terhadap ancaman komputasi kuantum (Grover's algorithm hanya mereduksi keamanan menjadi 128-bit, masih aman).

**Kenapa mode CBC, bukan ECB?**

ECB (Electronic Codebook) mengenkripsi setiap blok secara independen, sehingga blok plaintext yang sama selalu menghasilkan ciphertext yang sama. Hal ini membocorkan pola data.

```
ECB (TIDAK AMAN):
  "halo" → "X7kL"
  "halo" → "X7kL"  ← sama! pola terlihat

CBC (AMAN):
  "halo" (blok 1) → di-XOR dengan IV → dienkripsi → "X7kL"
  "halo" (blok 2) → di-XOR dengan "X7kL" → dienkripsi → "P9qR"  ← berbeda!
```

Sesuai NIST SP 800-38A, mode CBC direkomendasikan untuk enkripsi data yang membutuhkan kerahasiaan tanpa autentikasi tambahan.

---

### 2. RSA-2048

**Kenapa RSA?**

RSA (Rivest-Shamir-Adleman) adalah algoritma enkripsi asimetris yang memungkinkan pertukaran kunci secara aman tanpa perlu channel yang sudah terenkripsi sebelumnya. Ini menyelesaikan masalah **key distribution problem** pada enkripsi simetris.

**Kenapa 2048-bit, bukan 1024-bit?**

| Key Size | Status | Keterangan |
|---|---|---|
| RSA-512 | ❌ Tidak aman | Sudah berhasil di-crack tahun 1999 |
| RSA-1024 | ⚠️ Deprecated | NIST melarang penggunaan sejak 2013 |
| RSA-2048 | ✅ Aman | Minimum standar NIST hingga 2030 |
| RSA-4096 | ✅ Sangat aman | Untuk kebutuhan jangka panjang |

Berdasarkan NIST SP 800-131A Rev 2, RSA-1024 resmi di-deprecate pada tahun 2013 karena dapat di-crack dengan sumber daya komputasi modern. RSA-2048 adalah minimum yang direkomendasikan hingga tahun 2030.

**Kenapa OAEP Padding?**

```
PKCS#1 v1.5 (lama):
  - Rentan terhadap Bleichenbacher attack (1998)
  - Deterministik: input sama → output sama

OAEP (Optimal Asymmetric Encryption Padding):
  - Menambah random padding sebelum enkripsi
  - Input sama → output selalu berbeda (probabilistic)
  - Tahan terhadap chosen-ciphertext attack
  - Direkomendasikan PKCS#1 v2.2 dan NIST SP 800-56B
```

---

### 3. SHA-256

**Kenapa SHA-256, bukan MD5 atau SHA-1?**

| Algoritma | Status | Alasan |
|---|---|---|
| MD5 | ❌ Tidak aman | Collision ditemukan tahun 2004 (Wang et al.) |
| SHA-1 | ❌ Deprecated | Collision ditemukan tahun 2017 (SHAttered attack, Google) |
| SHA-256 | ✅ Aman | Bagian dari SHA-2 family, belum ada collision yang ditemukan |
| SHA-3 | ✅ Sangat aman | Generasi terbaru, lebih tahan quantum |

NIST FIPS 180-4 merekomendasikan SHA-256 sebagai standar minimum untuk hashing kriptografi. MD5 dan SHA-1 resmi dilarang untuk penggunaan keamanan baru oleh NIST sejak 2011.

**Kenapa hash di client, bukan di server?**

```
Jika hash di SERVER (berbahaya):
  Client kirim: "password123" (plaintext)
  Jika jaringan disadap → password langsung ketahuan

Jika hash di CLIENT (aman):
  Client kirim: "ef92b778bafe..." (hash)
  Jika disadap → hanya hash, tidak bisa di-reverse
  Server tidak pernah tahu password asli
```

---

### 4. Session Token (GUID)

**Kenapa perlu session token?**

Tanpa session token, siapapun bisa mengirim pesan palsu dengan format:
```
CHAT|alice|bob|pesanPalsu
```

Dengan session token, server memverifikasi bahwa pengirim memang sedang login sah:
```
CHAT|alice|bob|encMsg|b541001d95b2...
```

Token yang dipakai adalah **GUID (Globally Unique Identifier)** versi 4 yang di-generate menggunakan `Guid.NewGuid()`, menghasilkan 32 karakter hexadecimal acak (128-bit entropy). Probabilitas tebak token secara random adalah 1/2^128 ≈ 0 (secara praktis mustahil).

**Kenapa expiry 1 jam?**

OWASP Session Management Cheat Sheet merekomendasikan session timeout antara 15 menit hingga beberapa jam tergantung sensitivitas aplikasi. Expiry 1 jam dipilih sebagai keseimbangan antara keamanan dan kenyamanan pengguna, dengan fitur auto-refresh 2 menit sebelum expired.

---

## Threat Model (Model Ancaman)

Berdasarkan framework **STRIDE** (Microsoft) dan **OWASP Top 10**:

### Identifikasi Ancaman dan Mitigasi

| # | Ancaman | Kategori STRIDE | Mitigasi | Status |
|---|---|---|---|---|
| 1 | Penyadapan pesan di jaringan | Information Disclosure | RSA key exchange + AES-256 transit | ✅ Dimitigasi |
| 2 | Man-in-the-Middle attack | Spoofing | RSA public key verification | ⚠️ Sebagian |
| 3 | Pencurian password dari DB | Information Disclosure | SHA-256 hashing | ✅ Dimitigasi |
| 4 | Membaca pesan di phpMyAdmin | Information Disclosure | AES-256 DB key (message_backup) | ✅ Dimitigasi |
| 5 | Session hijacking | Spoofing | Session token + expiry | ✅ Dimitigasi |
| 6 | Replay attack | Elevation of Privilege | Token expiry + refresh | ✅ Dimitigasi |
| 7 | Pemalsuan identitas pengirim | Spoofing | Token validation per pesan | ✅ Dimitigasi |
| 8 | Brute force password | Elevation of Privilege | SHA-256 (lambat di-brute) | ⚠️ Sebagian |
| 9 | SQL Injection | Tampering | Parameterized query | ✅ Dimitigasi |
| 10 | Denial of Service | Denial of Service | Belum ada rate limiting | ❌ Belum |

---

## Analisis Per Lapisan Keamanan

### Layer 1 — Keamanan Transit (RSA + AES)

```
ALUR:
Client                          Server
  │                               │
  │ ◄── PUBKEY|<RSA public key> ──│  Server kirim public key
  │                               │
  │  generate AES key (random)    │
  │  generate AES IV  (random)    │
  │  enkripsi pakai RSA pubkey    │
  │                               │
  │ ──KEY_EXCHANGE|enc|enc──────► │  Client kirim AES key terenkripsi
  │                               │
  │                               │  Server dekripsi pakai RSA privkey
  │                               │  → dapat AES key + IV
  │                               │
  │ ◄══════ AES encrypted ═══════►│  Semua komunikasi selanjutnya AES
```

**Analisis Keamanan:**
- AES key berbeda setiap sesi → compromise satu sesi tidak expose sesi lain (**Perfect Forward Secrecy sebagian**)
- RSA OAEP padding mencegah chosen-ciphertext attack
- Kelemahan: tidak ada verifikasi identitas server (certificate) → rentan MITM jika attacker bisa intersep koneksi TCP awal

---

### Layer 2 — Keamanan Database (Audit Trail)

```
Kolom message_encrypted:
  Isi    : ciphertext AES session key sender
  Siapa yang bisa buka : TIDAK ADA
  Kenapa : AES key sender hilang setelah sesi berakhir
  Fungsi : Audit trail — bukti pesan pernah ada
```

**Analisis Keamanan:**
- Meskipun DB bocor, message_encrypted tidak bisa dibuka
- Memberikan bukti forensik bahwa pesan pernah dikirim tanpa bisa dibaca isinya

---

### Layer 3 — Keamanan Database (Offline Delivery)

```
Kolom message_backup:
  Isi    : AesEncrypt(plaintext, dbKey, dbIv)
  dbKey  : SHA-256("UTS_ISA_SECURE_DB_KEY_2024")
  Fungsi : Offline delivery — baca saat recipient login

Alur baca:
  message_backup → DbDecrypt(dbKey) → plaintext
  → AesEncrypt(aesKey_recipient) → kirim ke recipient
```

**Analisis Keamanan:**
- phpMyAdmin hanya menampilkan ciphertext, bukan plaintext
- DB key di-derive dari passphrase menggunakan SHA-256 (tidak hardcode key langsung)
- Kelemahan: IV bersifat fixed (hardcoded) → seharusnya random per pesan

---

### Layer 4 — Session Management

```
Login  : generate token GUID → simpan di memori + DB (sessions table)
Chat   : setiap pesan wajib sertakan token
Validasi:
  token cocok?  → lanjut
  token expired? → paksa logout
Refresh: auto-refresh 2 menit sebelum expired (tanpa login ulang)
Logout : invalidate token → is_valid=0 di DB
```

**Analisis Keamanan:**
- Token 128-bit GUID → entropy sangat tinggi, tidak bisa ditebak
- Expiry 1 jam → membatasi window serangan jika token bocor
- Audit trail di tabel sessions → bisa lacak history login

---

## Kelemahan Sistem

Setiap sistem keamanan memiliki kelemahan. Berikut kelemahan yang teridentifikasi beserta rekomendasi perbaikan:

### 1. Fixed IV pada DB Key

```
Kondisi saat ini:
  _dbIv = { 0x55, 0x54, 0x53, ... } // hardcoded, tidak berubah

Risiko:
  IV yang sama + key yang sama → pola yang sama di ciphertext
  Jika dua pesan memiliki plaintext yang sama, ciphertext-nya sama

Rekomendasi:
  Generate IV random per pesan, simpan bersama ciphertext di DB
```

### 2. SHA-256 tanpa Salt untuk Password

```
Kondisi saat ini:
  hash = SHA-256(password)

Risiko:
  Rentan Rainbow Table attack
  Dua user dengan password sama → hash sama di DB

Rekomendasi:
  Gunakan bcrypt, scrypt, atau PBKDF2 dengan salt random
  hash = bcrypt(password, saltRandom, cost=12)
```

### 3. DB Key Hardcoded di Source Code

```
Kondisi saat ini:
  passphrase = "UTS_ISA_SECURE_DB_KEY_2024" // ada di source code

Risiko:
  Jika source code bocor (GitHub public) → DB key ketahuan
  Attacker bisa dekripsi semua message_backup

Rekomendasi:
  Simpan passphrase di environment variable atau config terenkripsi
  Jangan hardcode secret di source code
```

### 4. Tidak Ada TLS/SSL

```
Kondisi saat ini:
  Raw TCP socket tanpa TLS
  RSA key exchange dilakukan manual di application layer

Risiko:
  Tidak ada verifikasi identitas server (certificate)
  Rentan MITM pada tahap key exchange pertama

Rekomendasi:
  Gunakan SslStream dari .NET untuk TLS di transport layer
  Atau gunakan sertifikat server yang terverifikasi
```

### 5. Tidak Ada Rate Limiting

```
Kondisi saat ini:
  Tidak ada batas percobaan login

Risiko:
  Brute force attack pada login tanpa hambatan

Rekomendasi:
  Lockout akun setelah 5 percobaan gagal
  Implementasi CAPTCHA atau delay antar percobaan
```

---

## Perbandingan dengan Sistem yang Ada

### WhatsApp (End-to-End Encryption)

| Aspek | SecureChat | WhatsApp |
|---|---|---|
| Protokol enkripsi | AES-256-CBC custom | Signal Protocol (Double Ratchet) |
| Key exchange | RSA-2048 + OAEP | X3DH (Extended Triple Diffie-Hellman) |
| Forward Secrecy | Sebagian (per sesi) | Penuh (per pesan) |
| Password hashing | SHA-256 | bcrypt/scrypt |
| Session management | Custom GUID token | Token berbasis server |

### Telegram (MTProto 2.0)

| Aspek | SecureChat | Telegram |
|---|---|---|
| Enkripsi transit | AES-256-CBC | AES-256-IGE |
| Key exchange | RSA-2048 | DH-2048 |
| Server-side encryption | ✅ Ada (message_backup) | ✅ Ada |
| End-to-end (default) | ✅ Ya | ❌ Tidak (hanya Secret Chat) |

### Signal

| Aspek | SecureChat | Signal |
|---|---|---|
| Algoritma | AES-256-CBC | AES-256-CBC + ChaCha20 |
| Key exchange | RSA-2048 | Curve25519 (lebih efisien) |
| Forward Secrecy | Sebagian | Penuh (Double Ratchet) |
| Open source | ✅ Ya | ✅ Ya |

**Kesimpulan Perbandingan:**
SecureChat mengimplementasikan prinsip keamanan dasar yang sama dengan aplikasi chat komersial (AES-256, enkripsi transit, session management). Perbedaan utama adalah aplikasi komersial menggunakan algoritma yang lebih canggih untuk forward secrecy (Double Ratchet, X3DH) dan hashing yang lebih kuat (bcrypt).

---

## Kesimpulan

Aplikasi SecureChat mengimplementasikan desain keamanan data berlapis yang mencakup:

1. **Keamanan Password** — SHA-256 hashing di client sebelum transmisi dan penyimpanan
2. **Keamanan Transmisi** — RSA-2048 key exchange + AES-256-CBC enkripsi pesan
3. **Keamanan Database** — Triple cipher (audit trail + DB encryption)
4. **Keamanan Sesi** — Session token GUID dengan expiry dan auto-refresh

Sistem ini memenuhi prinsip **CIA Triad** (Confidentiality, Integrity, Availability):
- **Confidentiality** ✅ — Data terenkripsi di transit dan di database
- **Integrity** ⚠️ — Tidak ada MAC/HMAC untuk verifikasi integritas pesan
- **Availability** ✅ — Offline delivery memastikan pesan sampai meskipun penerima sedang offline

Rekomendasi pengembangan ke depan:
- Implementasi TLS/SSL untuk transport layer
- Ganti SHA-256 dengan bcrypt untuk password hashing
- Tambah HMAC untuk verifikasi integritas pesan
- Implementasi random IV per pesan untuk DB encryption

---

## Referensi

1. **NIST FIPS PUB 197** — Advanced Encryption Standard (AES). National Institute of Standards and Technology, 2001. https://doi.org/10.6028/NIST.FIPS.197

2. **NIST SP 800-38A** — Recommendation for Block Cipher Modes of Operation: Methods and Techniques. NIST, 2001. https://doi.org/10.6028/NIST.SP.800-38A

3. **NIST SP 800-57 Part 1 Rev 5** — Recommendation for Key Management. NIST, 2020. https://doi.org/10.6028/NIST.SP.800-57pt1r5

4. **NIST SP 800-131A Rev 2** — Transitioning the Use of Cryptographic Algorithms and Key Lengths. NIST, 2019. https://doi.org/10.6028/NIST.SP.800-131Ar2

5. **NIST FIPS 180-4** — Secure Hash Standard (SHS). NIST, 2015. https://doi.org/10.6028/NIST.FIPS.180-4

6. **NIST SP 800-56B Rev 2** — Recommendation for Pair-Wise Key-Establishment Using Integer Factorization Cryptography. NIST, 2019. https://doi.org/10.6028/NIST.SP.800-56Br2

7. **OWASP Session Management Cheat Sheet**. OWASP Foundation, 2023. https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html

8. **OWASP Password Storage Cheat Sheet**. OWASP Foundation, 2023. https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html

9. **OWASP Top 10:2021** — A07 Identification and Authentication Failures. OWASP Foundation, 2021. https://owasp.org/Top10/A07_2021-Identification_and_Authentication_Failures/

10. **Rivest, R., Shamir, A., Adleman, L.** — A Method for Obtaining Digital Signatures and Public-Key Cryptosystems. Communications of the ACM, 21(2), 120–126, 1978. https://doi.org/10.1145/359340.359342

11. **Wang, X., Yu, H.** — How to Break MD5 and Other Hash Functions. EUROCRYPT 2005, LNCS 3494, pp. 19–35, 2005.

12. **Stevens, M., Bursztein, E., Karpman, P., Albertini, A., Markov, Y.** — The First Collision for Full SHA-1. CRYPTO 2017. https://shattered.io/

13. **Bellare, M., Rogaway, P.** — Optimal Asymmetric Encryption — How to Encrypt with RSA. EUROCRYPT 1994, LNCS 950, pp. 92–111, 1995.

14. **Marlinspike, M., Perrin, T.** — The Double Ratchet Algorithm. Signal Foundation, 2016. https://signal.org/docs/specifications/doubleratchet/

15. **Microsoft STRIDE Threat Model**. Microsoft Security Documentation, 2023. https://learn.microsoft.com/en-us/azure/security/develop/threat-modeling-tool-threats
