using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace UTS_ISA.users
{
    public static class DatabaseHelper
    {
        // Ganti password sesuai MySQL lokal kamu
        private const string ConnStr = "Server=localhost;Database=secure_chat;Uid=root;Pwd=;";

        // ── Server-side DB encryption key ─────────────────────────────────────
        //
        // Kolom message_plain di DB TIDAK menyimpan plaintext langsung.
        // Sebelum disimpan, plaintext dienkripsi dengan key ini (AES-256).
        // Tujuan: siapapun yang buka phpMyAdmin tidak bisa baca isi pesan.
        //
        // Key dan IV HARUS sama persis dengan yang di ServerHost.cs
        // agar data yang disimpan oleh satu server bisa dibaca server lain.
        //
        // Alur lengkap:
        //   Kirim pesan → AesDecrypt(encMsg, senderKey) → dapat plaintext
        //               → DbEncrypt(plaintext) → simpan ke DB
        //   Ambil pesan → baca dari DB → DbDecrypt() → dapat plaintext
        //               → AesEncrypt(plaintext, recipientKey) → kirim ke client
        private static readonly byte[] _dbKey;
        private static readonly byte[] _dbIv = new byte[]
        {
            0x55,0x54,0x53,0x5F,0x49,0x53,0x41,0x5F,  // "UTS_ISA_"
            0x32,0x30,0x32,0x34,0x00,0x00,0x00,0x00   // "2024\0\0\0\0"
        };

        static DatabaseHelper()
        {
            // Derive 256-bit DB key dari passphrase menggunakan SHA-256
            using (var sha = System.Security.Cryptography.SHA256.Create())
                _dbKey = sha.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes("UTS_ISA_SECURE_DB_KEY_2024"));
        }

        /// <summary>Enkripsi plaintext dengan server DB key sebelum disimpan ke DB.</summary>
        private static string DbEncrypt(string plain)
            => CryptoHelper.AesEncrypt(plain, _dbKey, _dbIv);

        /// <summary>Dekripsi data dari kolom message_plain di DB.</summary>
        private static string DbDecrypt(string cipher)
        {
            try { return CryptoHelper.AesDecrypt(cipher, _dbKey, _dbIv); }
            catch { return ""; } // data lama (plaintext) tidak bisa di-decrypt → skip
        }

        public static bool RegisterUser(string username, string passwordHash, string role = "user")
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        "INSERT INTO users (username, password_hash, role) VALUES (@u, @p, @r)", conn);
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", passwordHash);
                    cmd.Parameters.AddWithValue("@r", role);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Duplicate username
                return false;
            }
            catch { return false; }
        }

        public static (bool success, string role) LoginUser(string username, string passwordHash)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        "SELECT role FROM users WHERE username=@u AND password_hash=@p", conn);
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", passwordHash);
                    var result = cmd.ExecuteScalar();
                    return result != null ? (true, result.ToString()) : (false, null);
                }
            }
            catch { return (false, null); }
        }

        public static List<string> GetAllUsernames()
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand("SELECT username FROM users ORDER BY username", conn);
                    var r = cmd.ExecuteReader();
                    while (r.Read()) list.Add(r.GetString(0));
                }
            }
            catch { }
            return list;
        }

        public static void SaveMessage(string sender, string receiver, string encryptedMessage, string plainMessage)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        "INSERT INTO messages (sender, receiver, message_encrypted, message_plain, sent_at, delivered) VALUES (@s, @r, @enc, @plain, NOW(), 0)", conn);
                    cmd.Parameters.AddWithValue("@s", sender);
                    cmd.Parameters.AddWithValue("@r", receiver);
                    cmd.Parameters.AddWithValue("@enc", encryptedMessage);
                    // Enkripsi plaintext dengan server DB key sebelum disimpan
                    cmd.Parameters.AddWithValue("@plain", DbEncrypt(plainMessage));
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        // Ambil pesan yang belum terkirim untuk user tertentu
        public static List<(string sender, string plain, string sentAt)> GetPendingMessages(string receiver)
        {
            var list = new List<(string, string, string)>();
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        "SELECT sender, message_plain, sent_at FROM messages" +
                        " WHERE receiver=@r AND delivered=0 ORDER BY sent_at ASC", conn);
                    cmd.Parameters.AddWithValue("@r", receiver);
                    var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        string raw   = r.IsDBNull(1) ? "" : r.GetString(1);
                        string plain = DbDecrypt(raw); // dekripsi dari DB key
                        if (!string.IsNullOrEmpty(plain))
                            list.Add((r.GetString(0), plain, r.GetDateTime(2).ToString("HH:mm")));
                    }
                }
                // Tandai sudah terkirim
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        "UPDATE messages SET delivered=1 WHERE receiver=@r AND delivered=0", conn);
                    cmd.Parameters.AddWithValue("@r", receiver);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
            return list;
        }

        // Ambil semua riwayat chat untuk user (sent + received), untuk load history saat login.
        // Untuk sisi receiver: hanya ambil yang delivered=1 (pending dikirim terpisah via MSG).
        // Untuk sisi sender  : ambil semua (termasuk yang belum delivered ke lawan).
        public static List<(string sender, string receiver, string plain, string sentAt)>
            GetAllMessages(string username)
        {
            var list = new List<(string, string, string, string)>();
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        "SELECT sender, receiver, message_plain, sent_at FROM messages" +
                        " WHERE message_plain IS NOT NULL AND message_plain <> ''" +
                        "   AND (sender=@u OR (receiver=@u AND delivered=1))" +
                        " ORDER BY sent_at ASC", conn);
                    cmd.Parameters.AddWithValue("@u", username);
                    var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        string plain = DbDecrypt(r.GetString(2)); // dekripsi dari DB key
                        if (!string.IsNullOrEmpty(plain))
                            list.Add((r.GetString(0), r.GetString(1),
                                      plain, r.GetDateTime(3).ToString("HH:mm")));
                    }
                }
            }
            catch { }
            return list;
        }
    }
}
