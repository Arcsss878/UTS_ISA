using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace UTS_ISA.users
{
    // ════════════════════════════════════════════════════════════════════════════
    //  DATABASE HELPER
    //
    //  Semua operasi database (MySQL) terpusat di sini:
    //    - RegisterUser      : daftarkan user baru ke tabel users
    //    - LoginUser         : verifikasi username + password hash
    //    - GetAllUsernames   : ambil semua username untuk user list
    //    - SaveMessage       : simpan pesan ke tabel messages (delivered=0)
    //    - MarkDelivered     : update delivered=1 setelah pesan berhasil dikirim
    //    - GetPendingMessages: ambil pesan offline saat user login
    //    - GetAllMessages    : ambil riwayat chat lengkap saat login
    //
    //  LAPISAN ENKRIPSI KOLOM message_backup:
    //    Kolom ini TIDAK menyimpan plaintext langsung.
    //    Sebelum disimpan → dienkripsi dengan _dbKey (AES-256, fixed server key).
    //    Tujuan: siapapun yang buka phpMyAdmin tidak bisa membaca isi pesan.
    //
    //    Alur simpan pesan:
    //      plaintext → DbEncrypt(_dbKey) → ciphertext → simpan ke DB
    //    Alur baca pesan:
    //      ciphertext dari DB → DbDecrypt(_dbKey) → plaintext
    //      → re-enkripsi AES session key client → kirim ke client
    // ════════════════════════════════════════════════════════════════════════════
    public static class DatabaseHelper
    {
        // Connection string ke MySQL lokal
        // Ganti Pwd= dengan password MySQL kamu jika ada
        private const string ConnStr = "Server=localhost;Database=secure_chat;Uid=root;Pwd=;";

        // ── Server-side DB encryption key (AES-256) ───────────────────────────
        //
        // _dbKey : derived dari passphrase tetap menggunakan SHA-256
        //          → selalu menghasilkan key yang SAMA setiap server restart
        //          → 32 byte (256 bit) = cocok untuk AES-256
        //
        // _dbIv  : fixed IV 16 byte (hardcoded)
        //
        // PENTING: _dbKey dan _dbIv HARUS IDENTIK dengan yang di ServerHost.cs
        //          agar data yang ditulis satu server bisa dibaca server lain.
        private static readonly byte[] _dbKey; // akan diisi di static constructor
        private static readonly byte[] _dbIv = new byte[]
        {
            0x55, 0x54, 0x53, 0x5F, 0x49, 0x53, 0x41, 0x5F,  // "UTS_ISA_" dalam ASCII hex
            0x32, 0x30, 0x32, 0x34, 0x00, 0x00, 0x00, 0x00   // "2024\0\0\0\0"
        };

        // Static constructor — dijalankan SEKALI saat class pertama kali diakses
        static DatabaseHelper()
        {
            // Derive 256-bit DB key dari passphrase menggunakan SHA-256
            // SHA-256("UTS_ISA_SECURE_DB_KEY_2024") → 32 byte key yang selalu konsisten
            using (var sha = System.Security.Cryptography.SHA256.Create())
                _dbKey = sha.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes("UTS_ISA_SECURE_DB_KEY_2024"));
        }

        // ── Helper enkripsi/dekripsi kolom message_backup ──────────────────────

        /// <summary>
        /// Enkripsi plaintext dengan server DB key sebelum disimpan ke DB.
        /// Dipanggil oleh SaveMessage sebelum INSERT ke kolom message_backup.
        /// </summary>
        private static string DbEncrypt(string plain)
            => CryptoHelper.AesEncrypt(plain, _dbKey, _dbIv); // enkripsi AES-256-CBC

        /// <summary>
        /// Dekripsi data dari kolom message_backup di DB.
        /// Jika gagal decrypt (misal data lama yang masih plaintext), kembalikan string kosong.
        /// </summary>
        private static string DbDecrypt(string cipher)
        {
            try
            {
                return CryptoHelper.AesDecrypt(cipher, _dbKey, _dbIv); // dekripsi AES-256-CBC
            }
            catch
            {
                return ""; // gagal → data lama (plaintext) → skip, jangan crash
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  REGISTER USER
        //  Masukkan user baru ke tabel users.
        //  passwordHash sudah di-hash SHA-256 di sisi client sebelum dikirim.
        // ════════════════════════════════════════════════════════════════════════
        public static bool RegisterUser(string username, string passwordHash, string role = "user")
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi ke MySQL

                    // Query INSERT — jika username duplikat akan throw MySqlException error 1062
                    var cmd = new MySqlCommand(
                        "INSERT INTO users (username, password_hash, role) VALUES (@u, @p, @r)", conn);
                    cmd.Parameters.AddWithValue("@u", username);     // username baru
                    cmd.Parameters.AddWithValue("@p", passwordHash); // password hash SHA-256
                    cmd.Parameters.AddWithValue("@r", role);         // "user" atau "admin"
                    cmd.ExecuteNonQuery(); // jalankan INSERT

                    return true; // registrasi berhasil
                }
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Error 1062 = Duplicate entry → username sudah dipakai orang lain
                return false;
            }
            catch { return false; } // error lain (koneksi gagal, dll)
        }

        // ════════════════════════════════════════════════════════════════════════
        //  LOGIN USER
        //  Cek apakah kombinasi username + password_hash ada di DB.
        //  Kembalikan (true, role) jika cocok, (false, null) jika tidak.
        // ════════════════════════════════════════════════════════════════════════
        public static (bool success, string role) LoginUser(string username, string passwordHash)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi ke MySQL

                    // Query: ambil kolom 'role' jika username DAN password_hash cocok
                    var cmd = new MySqlCommand(
                        "SELECT role FROM users WHERE username=@u AND password_hash=@p", conn);
                    cmd.Parameters.AddWithValue("@u", username);     // username yang dicari
                    cmd.Parameters.AddWithValue("@p", passwordHash); // password hash SHA-256

                    // ExecuteScalar: ambil nilai pertama dari baris pertama (null jika tidak ada)
                    var result = cmd.ExecuteScalar();

                    // Jika result tidak null → user ditemukan → login berhasil
                    return result != null
                        ? (true, result.ToString())   // login berhasil, kembalikan role
                        : (false, null);              // user tidak ditemukan
                }
            }
            catch { return (false, null); } // error DB → anggap login gagal
        }

        // ════════════════════════════════════════════════════════════════════════
        //  GET ALL USERNAMES
        //  Ambil semua username dari DB, diurutkan A-Z.
        //  Dipakai server untuk broadcast ALL_USERS ke semua client yang online.
        // ════════════════════════════════════════════════════════════════════════
        public static List<string> GetAllUsernames()
        {
            var list = new System.Collections.Generic.List<string>(); // list kosong untuk diisi
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi ke MySQL

                    // Query: ambil semua username diurutkan alfabet A-Z
                    var cmd = new MySqlCommand("SELECT username FROM users ORDER BY username", conn);
                    var r   = cmd.ExecuteReader(); // jalankan query, dapat DataReader

                    // Loop baca setiap baris hasil query satu per satu
                    while (r.Read())
                        list.Add(r.GetString(0)); // kolom index 0 = username
                }
            }
            catch { } // error → kembalikan list yang sudah terkumpul (bisa kosong)
            return list;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SAVE MESSAGE
        //  Simpan pesan ke tabel messages dengan delivered=0.
        //
        //  Kolom yang diisi:
        //    sender            : username pengirim
        //    receiver          : username penerima
        //    message_encrypted : ciphertext AES asli dari sender (audit trail)
        //    message_backup     : plaintext yang sudah dienkripsi ulang dengan _dbKey
        //    sent_at           : waktu pesan dikirim (NOW())
        //    delivered         : 0 (belum terkirim ke penerima)
        //
        //  Kolom delivered:
        //    0 = pesan "menunggu" di server, penerima belum terima (offline)
        //        Analogi WhatsApp: centang satu (✓)
        //    1 = pesan sudah sampai ke penerima
        //        Analogi WhatsApp: centang dua (✓✓)
        //
        //  Return: ID row yang baru dibuat (dipakai MarkDelivered), atau -1 jika gagal
        // ════════════════════════════════════════════════════════════════════════
        public static long SaveMessage(string sender, string receiver,
                                       string encryptedMessage, string plainMessage)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi ke MySQL

                    // Query INSERT semua kolom sekaligus
                    var cmd = new MySqlCommand(
                        "INSERT INTO messages (sender, receiver, message_encrypted, message_backup, sent_at, delivered)" +
                        " VALUES (@s, @r, @enc, @plain, NOW(), 0)", conn);

                    cmd.Parameters.AddWithValue("@s",   sender);           // username pengirim
                    cmd.Parameters.AddWithValue("@r",   receiver);         // username penerima
                    cmd.Parameters.AddWithValue("@enc", encryptedMessage); // ciphertext asli (audit trail)

                    // ENKRIPSI plaintext dengan DB key sebelum disimpan ke kolom message_backup
                    // → siapapun yang buka phpMyAdmin hanya melihat ciphertext, bukan isi pesan
                    cmd.Parameters.AddWithValue("@plain", DbEncrypt(plainMessage));

                    cmd.ExecuteNonQuery(); // jalankan INSERT

                    return cmd.LastInsertedId; // ambil ID row baru → dipakai MarkDelivered()
                }
            }
            catch { return -1; } // gagal → kembalikan -1 sebagai tanda error
        }

        // ════════════════════════════════════════════════════════════════════════
        //  MARK DELIVERED
        //  Update kolom delivered=1 untuk pesan dengan ID tertentu.
        //
        //  Dipanggil di dua situasi:
        //    1. Recipient ONLINE  → dipanggil setelah Writer.WriteLine berhasil di HandleChat
        //    2. Recipient OFFLINE → dipanggil setelah GetPendingMessages selesai mengirim semua
        //
        //  Jika TIDAK dipanggil (misal koneksi recipient putus saat forward):
        //    delivered tetap 0 → pesan akan dikirim ulang saat recipient login kembali.
        // ════════════════════════════════════════════════════════════════════════
        public static void MarkDelivered(long messageId)
        {
            if (messageId < 0) return; // ID tidak valid (SaveMessage sebelumnya gagal) → skip

            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi ke MySQL

                    // Update hanya baris dengan ID yang spesifik → delivered=1
                    var cmd = new MySqlCommand(
                        "UPDATE messages SET delivered=1 WHERE id=@id", conn);
                    cmd.Parameters.AddWithValue("@id", messageId); // ID dari LastInsertedId
                    cmd.ExecuteNonQuery(); // jalankan UPDATE
                }
            }
            catch { } // error → abaikan, delivered tetap 0 (akan retry saat login)
        }

        // ════════════════════════════════════════════════════════════════════════
        //  GET PENDING MESSAGES
        //  Ambil semua pesan yang belum terkirim (delivered=0) untuk receiver.
        //  Dipanggil saat user login → kirim semua pesan yang masuk saat offline.
        //  Setelah semua pesan diambil → langsung update delivered=1.
        // ════════════════════════════════════════════════════════════════════════
        public static List<(string sender, string plain, string sentAt)> GetPendingMessages(string receiver)
        {
            var list = new List<(string, string, string)>(); // list hasil yang akan dikembalikan
            try
            {
                // ── Query 1: Ambil semua pesan yang belum terkirim ────────────
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi ke MySQL

                    // Ambil sender, message_backup, sent_at WHERE delivered=0 (belum terkirim)
                    // ORDER BY sent_at ASC → urutkan dari yang paling lama (kronologis)
                    var cmd = new MySqlCommand(
                        "SELECT sender, message_backup, sent_at FROM messages" +
                        " WHERE receiver=@r AND delivered=0 ORDER BY sent_at ASC", conn);
                    cmd.Parameters.AddWithValue("@r", receiver); // penerima = user yang baru login

                    var r = cmd.ExecuteReader(); // jalankan query SELECT
                    while (r.Read())
                    {
                        // Cek apakah kolom message_backup NULL (untuk keamanan, hindari SqlNullValueException)
                        string raw   = r.IsDBNull(1) ? "" : r.GetString(1); // ambil ciphertext dari DB

                        // Dekripsi ciphertext dengan DB key → dapat plaintext
                        string plain = DbDecrypt(raw);

                        // Hanya tambahkan jika dekripsi berhasil (tidak kosong)
                        if (!string.IsNullOrEmpty(plain))
                            list.Add((
                                r.GetString(0),                       // sender (kolom 0)
                                plain,                                // plaintext hasil dekripsi
                                r.GetDateTime(2).ToString("HH:mm")   // waktu pengiriman (kolom 2)
                            ));
                    }
                } // koneksi pertama ditutup di sini

                // ── Query 2: Tandai semua pesan tersebut sebagai delivered=1 ──
                // Koneksi baru karena DataReader dari koneksi lama sudah dipakai
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi baru untuk UPDATE

                    // Update semua pesan dengan receiver=@r dan delivered=0 → delivered=1
                    var cmd = new MySqlCommand(
                        "UPDATE messages SET delivered=1 WHERE receiver=@r AND delivered=0", conn);
                    cmd.Parameters.AddWithValue("@r", receiver); // penerima yang sama
                    cmd.ExecuteNonQuery(); // jalankan UPDATE massal
                }
            }
            catch { } // error → kembalikan list yang sudah terkumpul
            return list;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  GET ALL MESSAGES (HISTORY)
        //  Ambil riwayat chat lengkap untuk username tertentu.
        //  Dipanggil saat user login untuk load semua history chat.
        //
        //  Filter SQL:
        //    sender=@u                       → pesan yang dikirim user ini (semua)
        //    OR receiver=@u AND delivered=1  → pesan yang diterima user ini (sudah delivered saja)
        //
        //  Kenapa receiver harus delivered=1?
        //    Untuk mencegah DOUBLE MESSAGE:
        //    Pesan pending (delivered=0) sudah dikirim terpisah via GetPendingMessages (sebagai MSG).
        //    Kalau HISTORY juga kirim yang delivered=0, client akan terima dua kali.
        //    Dengan filter ini, pending hanya dikirim sekali lewat GetPendingMessages.
        // ════════════════════════════════════════════════════════════════════════
        public static List<(string sender, string receiver, string plain, string sentAt)>
            GetAllMessages(string username)
        {
            var list = new List<(string, string, string, string)>(); // list hasil
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open(); // buka koneksi ke MySQL

                    var cmd = new MySqlCommand(
                        "SELECT sender, receiver, message_backup, sent_at FROM messages" +
                        " WHERE message_backup IS NOT NULL AND message_backup <> ''" + // skip baris kosong
                        "   AND (sender=@u OR (receiver=@u AND delivered=1))"       // filter: sent + received delivered
                        + " ORDER BY sent_at ASC", conn); // urutkan dari lama ke baru (kronologis)

                    cmd.Parameters.AddWithValue("@u", username); // user yang sedang login

                    var r = cmd.ExecuteReader(); // jalankan query SELECT
                    while (r.Read())
                    {
                        // Dekripsi message_backup dari DB key → dapat plaintext
                        string plain = DbDecrypt(r.GetString(2)); // kolom index 2 = message_backup

                        // Hanya tambahkan jika dekripsi berhasil (bukan string kosong)
                        if (!string.IsNullOrEmpty(plain))
                            list.Add((
                                r.GetString(0),                       // sender (kolom 0)
                                r.GetString(1),                       // receiver (kolom 1)
                                plain,                                // plaintext hasil dekripsi
                                r.GetDateTime(3).ToString("HH:mm")   // sent_at (kolom 3)
                            ));
                    }
                }
            }
            catch { } // error → kembalikan list kosong
            return list;
        }
    }
}
