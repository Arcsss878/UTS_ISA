using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace UTS_ISA.users
{
    public static class DatabaseHelper
    {
        // Ganti password sesuai MySQL lokal kamu
        private const string ConnStr = "Server=localhost;Database=secure_chat;Uid=root;Pwd=;";

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
                    cmd.Parameters.AddWithValue("@plain", plainMessage);
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
                        string plain = r.IsDBNull(1) ? "" : r.GetString(1);
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
                        list.Add((r.GetString(0), r.GetString(1),
                                  r.GetString(2), r.GetDateTime(3).ToString("HH:mm")));
                }
            }
            catch { }
            return list;
        }
    }
}
