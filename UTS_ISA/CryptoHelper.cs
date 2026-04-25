using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UTS_ISA
{
    /// <summary>
    /// Kumpulan fungsi kriptografi yang dipakai seluruh aplikasi.
    /// Berisi implementasi:
    ///   - AES-256-CBC  : enkripsi/dekripsi pesan chat
    ///   - RSA-2048     : enkripsi AES key saat key exchange
    ///   - SHA-256      : hashing password sebelum dikirim ke server
    /// </summary>
    public static class CryptoHelper
    {
        // ════════════════════════════════════════════════════════════════════
        //  AES-256-CBC  —  enkripsi pesan chat
        //
        //  Kenapa AES?
        //    AES (Advanced Encryption Standard) adalah algoritma enkripsi
        //    simetris yang dipakai standar industri (perbankan, militer, dll).
        //    Kita pakai mode CBC (Cipher Block Chaining) dengan key 256-bit
        //    agar setiap blok ciphertext bergantung pada blok sebelumnya,
        //    sehingga pola pesan tidak bocor.
        //
        //  Key & IV:
        //    - Key (32 byte = 256 bit) : kunci enkripsi/dekripsi, harus SAMA
        //      di kedua sisi.
        //    - IV  (16 byte = 128 bit) : Initialization Vector, dipakai agar
        //      enkripsi pesan yang sama tidak menghasilkan ciphertext yang sama.
        //    - Key + IV di-generate random per sesi koneksi, lalu dikirim
        //      ke server lewat RSA (lihat bagian RSA di bawah).
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enkripsi plaintext menjadi Base64 ciphertext menggunakan AES-256-CBC.
        /// Hasil adalah string Base64 yang aman dikirim lewat jaringan.
        /// </summary>
        /// <param name="plainText">Teks asli yang ingin dienkripsi</param>
        /// <param name="key">AES key 32 byte (256-bit)</param>
        /// <param name="iv">AES IV 16 byte (128-bit)</param>
        /// <returns>Ciphertext dalam format Base64</returns>
        public static string AesEncrypt(string plainText, byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider { Key = key, IV = iv })
            using (var ms  = new MemoryStream())
            using (var cs  = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                return Convert.ToBase64String(ms.ToArray()); // kembalikan sebagai Base64 string
            }
        }

        /// <summary>
        /// Dekripsi Base64 ciphertext menjadi plaintext menggunakan AES-256-CBC.
        /// </summary>
        /// <param name="cipherBase64">Ciphertext dalam format Base64</param>
        /// <param name="key">AES key 32 byte — harus sama dengan yang dipakai saat enkripsi</param>
        /// <param name="iv">AES IV 16 byte — harus sama dengan yang dipakai saat enkripsi</param>
        /// <returns>Plaintext asli</returns>
        public static string AesDecrypt(string cipherBase64, byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider { Key = key, IV = iv })
            using (var ms  = new MemoryStream(Convert.FromBase64String(cipherBase64)))
            using (var cs  = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (var sr  = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// Generate AES key 256-bit secara acak menggunakan RNG (Random Number Generator)
        /// yang cryptographically secure. Dipanggil sekali per sesi koneksi.
        /// </summary>
        public static byte[] GenerateAesKey()
        {
            byte[] key = new byte[32]; // 32 byte = 256 bit
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(key);
            return key;
        }

        /// <summary>
        /// Generate AES IV 128-bit secara acak. IV bukan rahasia (boleh dikirim
        /// bersama ciphertext), tapi harus berbeda setiap sesi agar enkripsi
        /// pesan yang sama tidak menghasilkan ciphertext yang sama.
        /// </summary>
        public static byte[] GenerateAesIv()
        {
            byte[] iv = new byte[16]; // 16 byte = 128 bit
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(iv);
            return iv;
        }

        // ════════════════════════════════════════════════════════════════════
        //  RSA-2048  —  enkripsi AES key saat key exchange
        //
        //  Kenapa RSA?
        //    AES butuh key yang SAMA di client dan server. Masalahnya,
        //    bagaimana cara mengirim AES key secara aman ke server tanpa
        //    bisa disadap di tengah jalan?
        //
        //    Jawaban: pakai RSA (asimetris). RSA punya dua kunci:
        //      - Public key  : boleh dibagikan ke semua orang
        //      - Private key : hanya server yang punya, TIDAK pernah dikirim
        //
        //    Alur (di FormLogin.ConnectAndExchangeKeys):
        //      1. Server kirim RSA public key ke client
        //      2. Client generate AES key + IV secara random
        //      3. Client enkripsi AES key+IV pakai RSA public key server
        //      4. Client kirim hasil enkripsi ke server
        //      5. Server dekripsi pakai RSA private key → dapat AES key+IV
        //      6. Mulai sekarang semua komunikasi pakai AES (lebih cepat)
        //
        //    RSA dipakai hanya sekali di awal (key exchange), setelah itu
        //    AES yang dipakai karena AES jauh lebih cepat dari RSA.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enkripsi data (AES key/IV) menggunakan RSA public key server.
        /// Pakai padding OAEP (true) yang lebih aman dari PKCS#1 v1.5.
        /// </summary>
        /// <param name="data">Data yang ingin dienkripsi (AES key atau IV)</param>
        /// <param name="publicKeyXml">RSA public key dalam format XML string</param>
        /// <returns>Data terenkripsi dalam bentuk byte array</returns>
        public static byte[] RsaEncrypt(byte[] data, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(publicKeyXml);
                return rsa.Encrypt(data, true); // true = pakai OAEP padding
            }
        }

        /// <summary>
        /// Dekripsi data yang dienkripsi RSA menggunakan private key.
        /// Hanya server yang bisa melakukan ini karena hanya server yang punya private key.
        /// </summary>
        /// <param name="data">Data terenkripsi (byte array)</param>
        /// <param name="privateKeyXml">RSA private key dalam format XML string</param>
        /// <returns>Data asli (AES key atau IV)</returns>
        public static byte[] RsaDecrypt(byte[] data, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(privateKeyXml);
                return rsa.Decrypt(data, true); // true = pakai OAEP padding
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  SHA-256  —  hashing password
        //
        //  Kenapa password di-hash?
        //    Password TIDAK PERNAH dikirim as plaintext ke server.
        //    Sebelum dikirim, password di-hash dulu dengan SHA-256.
        //    Hash bersifat ONE-WAY: dari hash tidak bisa balik ke password asli.
        //
        //    Keuntungan:
        //      - Kalau traffic disadap, yang terlihat hanya hash, bukan password
        //      - Kalau database bocor, password asli tetap aman
        //      - Server tidak perlu tahu password asli, cukup bandingkan hash-nya
        //
        //    Contoh:
        //      "password123" → SHA-256 → "ef92b778bafe771..." (64 karakter hex)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Hash string input menggunakan SHA-256. Dipakai untuk hash password
        /// sebelum dikirim ke server dan sebelum disimpan ke database.
        /// </summary>
        /// <param name="input">String yang ingin di-hash (password plaintext)</param>
        /// <returns>Hash dalam format lowercase hex string (64 karakter)</returns>
        public static string Sha256Hash(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
}
