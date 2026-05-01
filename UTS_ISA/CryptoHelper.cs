using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UTS_ISA
{
    // ════════════════════════════════════════════════════════════════════════════
    //  CRYPTO HELPER
    //
    //  Kelas ini berisi semua fungsi kriptografi yang dipakai seluruh aplikasi.
    //  Semua method bersifat static → bisa dipanggil langsung tanpa buat objek.
    //
    //  TIGA ALGORITMA YANG DIPAKAI:
    //
    //  1. AES-256-CBC  → enkripsi/dekripsi pesan chat
    //     - Simetris: key yang sama untuk enkripsi dan dekripsi
    //     - Key 256-bit (32 byte), IV 128-bit (16 byte)
    //     - Dipakai: transit jaringan + simpan di DB (message_backup)
    //
    //  2. RSA-2048     → enkripsi AES key saat pertama kali konek
    //     - Asimetris: public key untuk enkripsi, private key untuk dekripsi
    //     - Dipakai HANYA SEKALI di awal koneksi (key exchange)
    //     - Setelah dapat AES key → semua komunikasi beralih ke AES
    //
    //  3. SHA-256      → hashing password
    //     - One-way: dari hash TIDAK BISA balik ke plaintext
    //     - Password di-hash di client sebelum dikirim ke server
    //     - Server hanya menyimpan dan membandingkan hash-nya
    // ════════════════════════════════════════════════════════════════════════════
    public static class CryptoHelper
    {
        // ════════════════════════════════════════════════════════════════════════
        //  AES-256-CBC  —  enkripsi pesan chat
        //
        //  AES (Advanced Encryption Standard):
        //    Algoritma enkripsi simetris standar industri (perbankan, militer, dll).
        //    "Simetris" artinya key yang sama dipakai untuk enkripsi DAN dekripsi.
        //
        //  Mode CBC (Cipher Block Chaining):
        //    Pesan dipecah jadi blok-blok 128-bit.
        //    Setiap blok di-XOR dengan ciphertext blok sebelumnya sebelum dienkripsi.
        //    Efeknya: pola dalam plaintext tidak terlihat di ciphertext.
        //    Contoh: "halo halo halo" tidak menghasilkan pola yang berulang.
        //
        //  Key (32 byte = 256 bit):
        //    Kunci utama enkripsi. Harus SAMA di pengirim dan penerima.
        //    Di-generate random per sesi → berbeda setiap kali login.
        //    Dikirim ke server lewat RSA (aman dari sadapan).
        //
        //  IV (16 byte = 128 bit):
        //    Initialization Vector. Dipakai sebagai "blok pertama" di CBC.
        //    Harus random dan berbeda setiap sesi.
        //    Tujuan: dua sesi yang kirim pesan sama → ciphertext berbeda.
        //
        //  Hasil enkripsi dikonversi ke Base64 agar aman dikirim sebagai teks
        //  (karena byte raw mungkin mengandung karakter yang tidak valid di teks).
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enkripsi plaintext menjadi Base64 ciphertext menggunakan AES-256-CBC.
        /// Dipanggil di:
        ///   - FormChat: sebelum kirim pesan ke server (enkripsi dengan AES session key)
        ///   - ServerHost/ClientHandler: saat re-enkripsi pesan untuk dikirim ke recipient
        ///   - DatabaseHelper: saat simpan plaintext ke kolom message_backup (enkripsi DB key)
        /// </summary>
        /// <param name="plainText">Teks asli yang ingin dienkripsi (contoh: "halo")</param>
        /// <param name="key">AES key 32 byte (256-bit)</param>
        /// <param name="iv">AES IV 16 byte (128-bit)</param>
        /// <returns>Ciphertext dalam format Base64 (contoh: "X7kL9mN2pQ==")</returns>
        public static string AesEncrypt(string plainText, byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider { Key = key, IV = iv })
            // ↑ Buat objek AES, langsung set Key dan IV
            // AesCryptoServiceProvider otomatis pakai mode CBC dan padding PKCS7

            using (var ms = new MemoryStream())
            // ↑ MemoryStream = buffer di memori untuk tampung hasil enkripsi
            // Seperti "kertas" tempat kita tulis ciphertext

            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            // ↑ CryptoStream = "pipa" enkripsi
            // Apapun yang ditulis ke cs akan otomatis dienkripsi AES, lalu hasilnya masuk ke ms
            // CryptoStreamMode.Write = mode tulis (kita akan tulis plaintext, keluar ciphertext)
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                // ↑ Konversi string "halo" ke byte array [104, 97, 108, 111]
                // UTF-8 dipilih agar karakter Indonesia (é, ñ, dll) juga bisa dienkripsi

                cs.Write(data, 0, data.Length);
                // ↑ Tulis semua byte ke CryptoStream
                // Di balik layar: AES enkripsi byte per blok (128-bit)

                cs.FlushFinalBlock();
                // ↑ WAJIB dipanggil di akhir untuk:
                //   1. Flush sisa data yang belum diproses
                //   2. Tambah PKCS7 padding agar panjang data kelipatan 128-bit
                //   Tanpa ini, ciphertext bisa tidak lengkap / corrupt

                return Convert.ToBase64String(ms.ToArray());
                // ↑ Ambil semua byte hasil enkripsi dari MemoryStream
                // Konversi ke Base64 string agar bisa dikirim sebagai teks biasa
                // Base64: setiap 3 byte → 4 karakter ASCII (aman untuk dikirim)
            }
        }

        /// <summary>
        /// Dekripsi Base64 ciphertext menjadi plaintext menggunakan AES-256-CBC.
        /// Dipanggil di:
        ///   - FormChat: saat terima pesan dari server (dekripsi dengan AES session key)
        ///   - ServerHost/ClientHandler: saat dekripsi pesan dari sender
        ///   - DatabaseHelper: saat baca message_backup dari DB (dekripsi DB key)
        /// </summary>
        /// <param name="cipherBase64">Ciphertext dalam format Base64</param>
        /// <param name="key">AES key 32 byte — HARUS sama dengan yang dipakai saat enkripsi</param>
        /// <param name="iv">AES IV 16 byte — HARUS sama dengan yang dipakai saat enkripsi</param>
        /// <returns>Plaintext asli (contoh: "halo")</returns>
        public static string AesDecrypt(string cipherBase64, byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider { Key = key, IV = iv })
            // ↑ Buat objek AES dengan key+IV yang SAMA saat enkripsi
            // Jika key/IV berbeda → hasil dekripsi akan salah / throw exception

            using (var ms = new MemoryStream(Convert.FromBase64String(cipherBase64)))
            // ↑ Decode Base64 string → byte array ciphertext
            // Masukkan ke MemoryStream sebagai sumber data untuk dibaca

            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            // ↑ CryptoStream mode READ = "pipa" dekripsi
            // Saat kita baca dari cs, data dari ms otomatis didekripsi AES
            // Kebalikan dari AesEncrypt yang pakai CryptoStreamMode.Write

            using (var sr = new StreamReader(cs))
            // ↑ StreamReader membaca byte dari CryptoStream dan konversi ke string UTF-8
            {
                return sr.ReadToEnd();
                // ↑ Baca seluruh hasil dekripsi sampai habis
                // Hasilnya: plaintext asli yang sama seperti sebelum dienkripsi
            }
        }

        /// <summary>
        /// Generate AES key 256-bit secara acak menggunakan RNG cryptographically secure.
        /// Dipanggil SEKALI per koneksi di FormLogin.ConnectAndExchangeKeys().
        /// Key ini kemudian dikirim ke server lewat RSA (terenkripsi).
        /// </summary>
        /// <returns>AES key 32 byte (256-bit) yang acak dan unik per sesi</returns>
        public static byte[] GenerateAesKey()
        {
            byte[] key = new byte[32];
            // ↑ Siapkan array 32 byte kosong
            // 32 byte = 256 bit = panjang key untuk AES-256

            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(key);
            // ↑ RNGCryptoServiceProvider = Random Number Generator yang AMAN untuk kriptografi
            // Berbeda dengan Random() biasa yang bisa diprediksi
            // rng.GetBytes() mengisi array dengan byte acak dari sumber entropy OS
            // (di Windows: CryptGenRandom API, pakai noise hardware + waktu + proses)

            return key;
            // ↑ Key 256-bit siap dipakai untuk enkripsi AES
        }

        /// <summary>
        /// Generate AES IV 128-bit secara acak menggunakan RNG cryptographically secure.
        /// IV bukan rahasia, tapi harus BERBEDA setiap sesi agar keamanan terjaga.
        /// Dipanggil SEKALI per koneksi bersamaan dengan GenerateAesKey().
        /// </summary>
        /// <returns>AES IV 16 byte (128-bit) yang acak dan unik per sesi</returns>
        public static byte[] GenerateAesIv()
        {
            byte[] iv = new byte[16];
            // ↑ Siapkan array 16 byte kosong
            // 16 byte = 128 bit = panjang IV untuk AES (selalu 128-bit terlepas dari key size)

            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(iv);
            // ↑ Isi dengan byte acak cryptographically secure (sama seperti GenerateAesKey)

            return iv;
            // ↑ IV 128-bit siap dipakai bersamaan dengan key di AesEncrypt/AesDecrypt
        }

        // ════════════════════════════════════════════════════════════════════════
        //  RSA-2048  —  enkripsi AES key saat key exchange
        //
        //  RSA (Rivest–Shamir–Adleman):
        //    Algoritma enkripsi ASIMETRIS. Punya dua kunci yang berpasangan:
        //      - Public key  : boleh dibagikan ke siapa saja, dipakai untuk ENKRIPSI
        //      - Private key : rahasia, hanya satu pihak yang punya, dipakai DEKRIPSI
        //
        //    Sifat: data yang dienkripsi public key HANYA bisa dibuka private key.
        //    Meskipun orang lain punya public key yang sama, mereka tidak bisa dekripsi.
        //
        //  Kenapa RSA di sini?
        //    Masalah: AES butuh key yang sama di client dan server.
        //    Tapi bagaimana kirim AES key ke server tanpa disadap?
        //
        //    Solusi RSA:
        //      1. Server punya RSA keypair (public + private)
        //      2. Server kirim PUBLIC key ke client (boleh, siapapun bisa lihat)
        //      3. Client enkripsi AES key pakai RSA PUBLIC key server
        //      4. Client kirim hasil enkripsi ke server
        //      5. Hanya server (punya PRIVATE key) yang bisa buka → dapat AES key
        //      6. Sekarang kedua pihak punya AES key yang sama → pakai AES
        //
        //  RSA-2048:
        //    Angka 2048 = panjang key RSA dalam bit.
        //    Semakin besar = semakin aman, tapi semakin lambat.
        //    2048-bit adalah standar minimum yang direkomendasikan saat ini.
        //
        //  Keterbatasan RSA:
        //    RSA hanya bisa enkripsi data maksimal ~245 byte (untuk 2048-bit).
        //    Itulah kenapa RSA hanya dipakai untuk enkripsi AES key (32 byte),
        //    bukan untuk enkripsi pesan chat langsung.
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enkripsi data kecil (AES key/IV) menggunakan RSA public key.
        /// Dipanggil di FormLogin saat key exchange: enkripsi AES key+IV sebelum dikirim ke server.
        /// Pakai padding OAEP yang lebih aman dari PKCS#1 v1.5.
        /// </summary>
        /// <param name="data">Data yang ingin dienkripsi (AES key 32 byte atau IV 16 byte)</param>
        /// <param name="publicKeyXml">RSA public key dalam format XML string dari server</param>
        /// <returns>Data terenkripsi RSA dalam bentuk byte array (256 byte untuk RSA-2048)</returns>
        public static byte[] RsaEncrypt(byte[] data, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            // ↑ Buat RSA instance dengan key size 2048-bit
            // Angka 2048 harus SAMA dengan yang dipakai server saat generate keypair
            {
                rsa.FromXmlString(publicKeyXml);
                // ↑ Load RSA public key dari format XML string
                // Public key diterima dari server di awal koneksi (format: PUBKEY|<base64>)

                return rsa.Encrypt(data, true);
                // ↑ Enkripsi data menggunakan public key yang sudah di-load
                // Parameter true = pakai OAEP padding (Optimal Asymmetric Encryption Padding)
                // OAEP lebih aman dari PKCS#1 v1.5 (parameter false) karena:
                //   - Menambah random padding sehingga enkripsi data yang sama → hasil berbeda
                //   - Lebih tahan terhadap chosen-ciphertext attack
            }
        }

        /// <summary>
        /// Dekripsi data yang dienkripsi RSA menggunakan private key.
        /// Dipanggil di server (ClientHandler/ServerHandler) setelah terima KEY_EXCHANGE dari client.
        /// Hanya server yang bisa melakukan ini karena hanya server yang punya private key.
        /// </summary>
        /// <param name="data">Data terenkripsi RSA (byte array dari client)</param>
        /// <param name="privateKeyXml">RSA private key dalam format XML string (RAHASIA, jangan dibagikan)</param>
        /// <returns>Data asli (AES key 32 byte atau IV 16 byte)</returns>
        public static byte[] RsaDecrypt(byte[] data, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            // ↑ Buat RSA instance dengan key size 2048-bit (sama dengan saat enkripsi)
            {
                rsa.FromXmlString(privateKeyXml);
                // ↑ Load RSA private key (hanya server yang punya ini)
                // Private key tidak pernah dikirim ke jaringan, hanya ada di memori server

                return rsa.Decrypt(data, true);
                // ↑ Dekripsi menggunakan private key
                // Parameter true = pakai OAEP padding (HARUS sama dengan saat enkripsi)
                // Jika private key bukan pasangan dari public key yang dipakai enkripsi
                // → akan throw CryptographicException (tidak bisa dibuka)
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SHA-256  —  hashing password
        //
        //  SHA-256 (Secure Hash Algorithm 256-bit):
        //    Fungsi hash kriptografi yang menghasilkan "sidik jari" dari sebuah data.
        //    Bersifat ONE-WAY: dari output (hash) tidak bisa dikembalikan ke input (plaintext).
        //
        //  Sifat SHA-256:
        //    - Deterministic : input sama → output selalu sama
        //    - One-way       : tidak bisa balik dari hash ke plaintext
        //    - Avalanche     : satu karakter berubah → hash berubah drastis
        //    - Fixed size    : output selalu 256-bit (64 karakter hex), apapun input-nya
        //
        //  Kenapa password di-hash di CLIENT?
        //    - Password TIDAK PERNAH melintas di jaringan dalam bentuk asli
        //    - Kalau traffic disadap → yang terlihat hanya hash (tidak berguna)
        //    - Kalau DB bocor → hash tidak bisa di-reverse ke password asli
        //    - Server tidak perlu tahu password asli, cukup bandingkan hash-nya
        //
        //  Contoh:
        //    Input  : "password123"
        //    Output : "ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f"
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hash string input menggunakan SHA-256.
        /// Dipanggil di FormLogin dan FormRegister sebelum password dikirim ke server.
        /// Server tidak pernah menerima/menyimpan password asli, hanya hash-nya.
        /// </summary>
        /// <param name="input">String yang ingin di-hash (password plaintext dari user)</param>
        /// <returns>Hash SHA-256 dalam format lowercase hex string, panjang 64 karakter</returns>
        public static string Sha256Hash(string input)
        {
            using (var sha = SHA256.Create())
            // ↑ Buat instance SHA-256 hasher
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                // ↑ Encoding.UTF8.GetBytes(input)
                //     → konversi string password ke byte array
                //     → contoh: "abc" → [97, 98, 99]
                //   sha.ComputeHash(...)
                //     → hitung SHA-256 dari byte array
                //     → hasilnya SELALU 32 byte (256 bit), apapun panjang input-nya

                return BitConverter.ToString(bytes)
                // ↑ Konversi byte array ke string hex dengan tanda hubung
                // Contoh: [239, 146, 183] → "EF-92-B7"

                                   .Replace("-", "")
                // ↑ Hapus semua tanda hubung
                // Contoh: "EF-92-B7" → "EF92B7"

                                   .ToLower();
                // ↑ Konversi ke huruf kecil (konvensi standar untuk hash hex)
                // Contoh: "EF92B7" → "ef92b7"
                // Hasil akhir: 64 karakter hex lowercase
                // Contoh full: "ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f"
            }
        }
    }
}
