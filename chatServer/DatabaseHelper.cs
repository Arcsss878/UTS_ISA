using System;
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

        public static void SaveMessage(string sender, string receiver, string encryptedMessage)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    var cmd = new MySqlCommand(
                        "INSERT INTO messages (sender, receiver, message_encrypted, sent_at) VALUES (@s, @r, @m, NOW())", conn);
                    cmd.Parameters.AddWithValue("@s", sender);
                    cmd.Parameters.AddWithValue("@r", receiver);
                    cmd.Parameters.AddWithValue("@m", encryptedMessage);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }
    }
}
