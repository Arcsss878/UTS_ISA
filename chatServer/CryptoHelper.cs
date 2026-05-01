using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UTS_ISA.users
{
    // ════════════════════════════════════════════════════════════════════════════
    //  CRYPTO HELPER  (versi chatServer — standalone server project)
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
        //  Hasil enkripsi dikonversi ke Base64 agar aman dikirim sebagai teks.
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enkripsi plaintext menjadi Base64 ciphertext menggunakan AES-256-CBC.
        /// Dipanggil di:
        ///   - ClientHandler: saat re-enkripsi pesan untuk dikirim ke recipient
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

            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            // ↑ CryptoStream = "pipa" enkripsi
            // Apapun yang ditulis ke cs → otomatis dienkripsi AES → hasilnya masuk ke ms
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                // ↑ Konversi string ke byte array (UTF-8)
                // Contoh: "halo" → [104, 97, 108, 111]

                cs.Write(data, 0, data.Length);
                // ↑ Tulis semua byte ke CryptoStream → AES enkripsi per blok 128-bit

                cs.FlushFinalBlock();
                // ↑ WAJIB dipanggil di akhir:
                // 1. Flush sisa data yang belum diproses
                // 2. Tambah PKCS7 padding agar panjang data kelipatan 128-bit
                // Tanpa ini → ciphertext bisa tidak lengkap / corrupt

                return Convert.ToBase64String(ms.ToArray());
                // ↑ Ambil semua byte hasil enkripsi dari MemoryStream
                // Konversi ke Base64 string agar bisa dikirim sebagai teks biasa
            }
        }

        /// <summary>
        /// Dekripsi Base64 ciphertext menjadi plaintext menggunakan AES-256-CBC.
        /// Dipanggil di:
        ///   - ClientHandler: saat dekripsi pesan dari sender
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
            // Saat dibaca dari cs, data dari ms otomatis didekripsi AES

            using (var sr = new StreamReader(cs))
            // ↑ StreamReader membaca byte dari CryptoStream dan konversi ke string UTF-8
            {
                return sr.ReadToEnd();
                // ↑ Baca seluruh hasil dekripsi sampai habis → plaintext asli
            }
        }

        /// <summary>
        /// Generate AES key 256-bit secara acak menggunakan RNG cryptographically secure.
        /// Dipanggil SEKALI per koneksi di sisi client saat key exchange.
        /// Key ini kemudian dikirim ke server lewat RSA (terenkripsi).
        /// </summary>
        /// <returns>AES key 32 byte (256-bit) yang acak dan unik per sesi</returns>
        public static byte[] GenerateAesKey()
        {
            byte[] key = new byte[32];
            // ↑ Siapkan array 32 byte (256 bit) untuk AES-256

            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(key);
            // ↑ RNGCryptoServiceProvider = Random Number Generator yang AMAN untuk kriptografi
            // Berbeda dengan Random() biasa yang bisa diprediksi
            // rng.GetBytes() mengisi array dengan byte acak dari sumber entropy OS

            return key; // kembalikan AES key 256-bit yang siap dipakai
        }

        /// <summary>
        /// Generate AES IV 128-bit secara acak menggunakan RNG cryptographically secure.
        /// IV bukan rahasia, tapi harus BERBEDA setiap sesi agar keamanan terjaga.
        /// </summary>
        /// <returns>AES IV 16 byte (128-bit) yang acak dan unik per sesi</returns>
        public static byte[] GenerateAesIv()
        {
            byte[] iv = new byte[16];
            // ↑ Siapkan array 16 byte (128 bit) untuk AES IV
            // IV selalu 128-bit di AES, terlepas dari ukuran key (128/192/256-bit)

            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(iv);
            // ↑ Isi dengan byte acak cryptographically secure (sama seperti GenerateAesKey)

            return iv; // kembalikan IV 128-bit yang siap dipakai
        }

        // ════════════════════════════════════════════════════════════════════════
        //  RSA-2048  —  enkripsi AES key saat key exchange
        //
        //  RSA (Rivest–Shamir–Adleman):
        //    Algoritma enkripsi ASIMETRIS. Punya dua kunci yang berpasangan:
        //      - Public key  : boleh dibagikan ke siapa saja → dipakai untuk ENKRIPSI
        //      - Private key : rahasia, hanya server yang punya → dipakai DEKRIPSI
        //
        //    Sifat: data yang dienkripsi public key HANYA bisa dibuka private key.
        //
        //  Kenapa RSA di sini?
        //    AES butuh key yang sama di client dan server.
        //    RSA dipakai untuk mengirim AES key ke server secara aman:
        //      1. Server kirim PUBLIC key ke client
        //      2. Client enkripsi AES key pakai RSA PUBLIC key server
        //      3. Kirim ke server → hanya server (punya PRIVATE key) yang bisa buka
        //      4. Sekarang kedua pihak punya AES key yang sama → pakai AES
        //
        //  RSA-2048:
        //    2048 = panjang key RSA dalam bit (standar minimum yang direkomendasikan).
        //    Keterbatasan: hanya bisa enkripsi data maksimal ~245 byte.
        //    Itulah kenapa RSA hanya dipakai untuk enkripsi AES key (32 byte),
        //    bukan untuk enkripsi pesan chat langsung.
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enkripsi data kecil (AES key/IV) menggunakan RSA public key.
        /// Dipanggil di sisi client saat key exchange sebelum kirim AES key ke server.
        /// </summary>
        /// <param name="data">Data yang ingin dienkripsi (AES key 32 byte atau IV 16 byte)</param>
        /// <param name="publicKeyXml">RSA public key dalam format XML string dari server</param>
        /// <returns>Data terenkripsi RSA dalam bentuk byte array</returns>
        public static byte[] RsaEncrypt(byte[] data, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            // ↑ Buat RSA instance dengan key size 2048-bit
            {
                rsa.FromXmlString(publicKeyXml);
                // ↑ Load RSA public key dari format XML string yang dikirim server

                return rsa.Encrypt(data, true);
                // ↑ Enkripsi data menggunakan public key
                // true = pakai OAEP padding (lebih aman dari PKCS#1 v1.5)
                // OAEP menambah random padding → enkripsi data yang sama → hasil selalu berbeda
            }
        }

        /// <summary>
        /// Dekripsi data yang dienkripsi RSA menggunakan private key.
        /// Dipanggil di server (ClientHandler) setelah terima KEY_EXCHANGE dari client.
        /// Hanya server yang bisa melakukan ini karena hanya server yang punya private key.
        /// </summary>
        /// <param name="data">Data terenkripsi RSA (byte array dari client)</param>
        /// <param name="privateKeyXml">RSA private key dalam format XML string (RAHASIA)</param>
        /// <returns>Data asli (AES key 32 byte atau IV 16 byte)</returns>
        public static byte[] RsaDecrypt(byte[] data, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            // ↑ Buat RSA instance dengan key size 2048-bit (sama dengan saat enkripsi)
            {
                rsa.FromXmlString(privateKeyXml);
                // ↑ Load RSA private key (tidak pernah dikirim ke jaringan, hanya ada di memori server)

                return rsa.Decrypt(data, true);
                // ↑ Dekripsi menggunakan private key
                // true = pakai OAEP padding (HARUS sama dengan saat enkripsi)
                // Jika private key bukan pasangan dari public key → throw CryptographicException
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SHA-256  —  hashing password
        //
        //  SHA-256 (Secure Hash Algorithm 256-bit):
        //    Fungsi hash yang menghasilkan "sidik jari" dari sebuah data.
        //    Bersifat ONE-WAY: dari output (hash) tidak bisa dikembalikan ke input.
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
        /// Dipanggil sebelum password dikirim ke server dan sebelum disimpan ke database.
        /// </summary>
        /// <param name="input">String yang ingin di-hash (password plaintext dari user)</param>
        /// <returns>Hash SHA-256 dalam format lowercase hex string (64 karakter)</returns>
        public static string Sha256Hash(string input)
        {
            using (var sha = SHA256.Create()) // buat instance SHA-256 hasher
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                // ↑ Encoding.UTF8.GetBytes(input) → konversi string password ke byte array
                //   sha.ComputeHash(...)           → hitung SHA-256 → hasilnya 32 byte (256 bit)

                return BitConverter.ToString(bytes) // konversi byte[] ke "AB-CD-EF-..."
                                   .Replace("-", "") // hapus tanda hubung → "ABCDEF..."
                                   .ToLower();       // huruf kecil → "abcdef..." (64 karakter hex)
                // Contoh hasil: "ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f"
            }
        }
    }
}
