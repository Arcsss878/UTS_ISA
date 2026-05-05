# Alur Proses Desain Keamanan Data — SecureChat

Diagram berikut menggambarkan seluruh alur keamanan data pada aplikasi SecureChat,
mencakup enkripsi password, key exchange RSA, session token, dan triple cipher enkripsi pesan.

---

```mermaid
flowchart TD
    subgraph CLIENT["🖥️ CLIENT"]
        direction TB

        subgraph SHA["1. Keamanan Password (SHA-256)"]
            A1([Mulai]) --> A2[Input Username & Password]
            A2 --> A3["Hash Password\nSHA-256 di Client\n(tidak dikirim plaintext)"]
            A3 --> A4{Register\natau Login?}
            A4 -->|Register| A5["Kirim REGISTER\n|username|sha256hash|"]
            A4 -->|Login| A6["Kirim LOGIN\n|username|sha256hash|"]
        end

        subgraph RSA["2. Key Exchange (RSA-2048)"]
            B1["Terima RSA Public Key\ndari Server (XML Base64)"]
            B2["Generate AES Key 256-bit\n+ AES IV 128-bit (RANDOM)"]
            B3["Enkripsi AES Key/IV\npakai RSA Public Key\n(OAEP Padding)"]
            B4["Kirim KEY_EXCHANGE\n|encAesKey|encAesIV|"]
            B1 --> B2 --> B3 --> B4
        end

        subgraph TOKEN["3. Session Token"]
            C1["Terima LOGIN_SUCCESS\n|role|token|expiry|"]
            C2["Simpan Token\ndi Memori Client"]
            C3["Timer Countdown\n(tiap 1 detik)"]
            C4{Sisa token\n< 2 menit?}
            C5["Kirim TOKEN_REFRESH\n|username|token|"]
            C6["Terima TOKEN_REFRESHED\n|newToken|expiry|"]
            C1 --> C2 --> C3 --> C4
            C4 -->|Ya| C5 --> C6 --> C2
            C4 -->|Tidak| C3
        end

        subgraph AES["4. Enkripsi Pesan (AES-256)"]
            D1["Tulis Pesan"]
            D2["AesEncrypt(pesan, aesKey, aesIV)"]
            D3["Kirim CHAT\n|from|to|AES(pesan)|token|"]
            D4["Terima MSG\n|from|AES(pesan)|"]
            D5["AesDecrypt(cipher, aesKey, aesIV)"]
            D6["Tampilkan Pesan\ndi Chat Window"]
            D7([Selesai])
            D1 --> D2 --> D3
            D4 --> D5 --> D6 --> D7
        end
    end

    subgraph SERVER["🗄️ SERVER & DATABASE"]
        direction TB

        subgraph SRSA["RSA Key Exchange"]
            S1([Terima Koneksi]) --> S2["Generate RSA-2048\nPublic + Private Key"]
            S2 --> S3["Kirim PUBKEY\n(format XML Base64)"]
            S3 --> S4["Terima KEY_EXCHANGE\n|encAesKey|encAesIV|"]
            S4 --> S5["Dekripsi AES Key/IV\npakai RSA Private Key\n→ Dapat AES Session Key"]
        end

        subgraph SAUTH["Autentikasi (SHA-256 + Session Token)"]
            SA1["Terima LOGIN\n|username|sha256|"]
            SA2["Cek DB: SELECT\nWHERE username=?\nAND password_hash=?"]
            SA3{Valid?}
            SA4["Generate Token\nGUID 32 char\nExpiry = +1 jam"]
            SA5["Kirim LOGIN_SUCCESS\n|role|token|expiry|"]
            SA6["Simpan ke tabel sessions\n(audit trail, is_valid=1)"]
            SA7["Kirim HISTORY\n+ Pesan Pending"]
            SA8["Kirim LOGIN_FAIL"]
            SA1 --> SA2 --> SA3
            SA3 -->|Valid| SA4 --> SA5 --> SA6 --> SA7
            SA3 -->|Tidak Valid| SA8
        end

        subgraph SCIPHER["Proses Pesan (Triple Cipher)"]
            SC1["Terima CHAT\n|from|to|AES(pesan)|token|"]
            SC2["Validasi Session Token\n(cocok + belum expired?)"]
            SC3{Token\nValid?}
            SC4["LAYER 1: Dekripsi\nAesDecrypt(cipher, aesKey_sender)\n→ Dapat plaintext"]
            SC5["LAYER 2: Simpan\nmessage_encrypted = cipher asli\n(audit trail, tidak bisa dibuka)"]
            SC6["LAYER 3: Simpan\nmessage_backup = AesEncrypt(plain, dbKey)\n(untuk offline delivery)"]
            SC7{Penerima\nOnline?}
            SC8["Re-enkripsi:\nAesEncrypt(plain, aesKey_recipient)\nKirim MSG ke Recipient\nUpdate delivered=1"]
            SC9["Pesan tetap di DB\ndelivered=0\n(kirim saat recipient login)"]
            SC10["Kirim ERROR\n(Session tidak valid)"]
            SC1 --> SC2 --> SC3
            SC3 -->|Valid| SC4 --> SC5 --> SC6 --> SC7
            SC3 -->|Tidak Valid| SC10
            SC7 -->|Online| SC8
            SC7 -->|Offline| SC9
        end
    end

    %% Message Flows antar Client dan Server
    S3 -.->|PUBKEY| B1
    B4 -.->|KEY_EXCHANGE| S4
    A5 -.->|REGISTER| SERVER
    A6 -.->|LOGIN| SA1
    SA5 -.->|LOGIN_SUCCESS| C1
    SA8 -.->|LOGIN_FAIL| CLIENT
    D3 -.->|CHAT + token| SC1
    SC8 -.->|MSG| D4
    SA7 -.->|HISTORY + PENDING| CLIENT

    style SHA fill:#dbeafe,stroke:#3b82f6
    style RSA fill:#fef3c7,stroke:#f59e0b
    style TOKEN fill:#d1fae5,stroke:#10b981
    style AES fill:#fce7f3,stroke:#ec4899
    style SRSA fill:#fef3c7,stroke:#f59e0b
    style SAUTH fill:#dbeafe,stroke:#3b82f6
    style SCIPHER fill:#ede9fe,stroke:#8b5cf6
    style CLIENT fill:#f0f9ff,stroke:#0ea5e9
    style SERVER fill:#f0fdf4,stroke:#22c55e
```

---

## Keterangan Alur

### Client Pool

| Lane | Fungsi Keamanan |
|---|---|
| **1. Keamanan Password (SHA-256)** | Password di-hash SHA-256 di sisi client sebelum dikirim. Server tidak pernah menerima password asli. |
| **2. Key Exchange (RSA-2048)** | Client generate AES key secara random, lalu enkripsi pakai RSA Public Key server (OAEP). Hanya server yang bisa buka. |
| **3. Session Token** | Setiap aksi chat wajib sertakan token. Token expire 1 jam, auto-refresh 2 menit sebelum expired. |
| **4. Enkripsi Pesan (AES-256)** | Semua pesan dienkripsi AES-256-CBC sebelum dikirim via jaringan. |

### Server Pool

| Lane | Fungsi Keamanan |
|---|---|
| **RSA Key Exchange** | Server generate pasangan RSA-2048. Public key dikirim ke client, private key tetap di memori server. |
| **Autentikasi (SHA-256 + Token)** | Cocokkan hash di DB, generate GUID token, simpan ke tabel `sessions` sebagai audit trail. |
| **Triple Cipher** | Layer 1: dekripsi dari sender. Layer 2: simpan `message_encrypted` (audit). Layer 3: simpan `message_backup` (offline delivery). |

### Triple Cipher Detail

```
Pesan masuk dari Sender:
  AES(pesan, aesKey_sender) ──► Dekripsi ──► plaintext
                                                │
                    ┌───────────────────────────┤
                    │                           │
                    ▼                           ▼
          message_encrypted            message_backup
          = cipher asli sender         = AES(plain, dbKey)
          (audit trail)                (offline delivery)
          tidak bisa dibuka            bisa dibuka server
          key sudah hilang             kapan saja
```
