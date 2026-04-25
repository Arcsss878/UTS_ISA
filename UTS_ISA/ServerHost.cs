using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;

namespace UTS_ISA
{
    // ════════════════════════════════════════════════════════════════════════════
    //  DATABASE HELPER
    // ════════════════════════════════════════════════════════════════════════════
    public static class ServerDb
    {
        // Ganti Pwd= jika MySQL kamu pakai password
        private const string ConnStr =
            "Server=localhost;Database=secure_chat;Uid=root;Pwd=;";

        // ── User ──────────────────────────────────────────────────────────────

        public static bool Register(string user, string pwHash)
        {
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "INSERT INTO users(username,password_hash,role) VALUES(@u,@p,'user')", c);
                    cmd.Parameters.AddWithValue("@u", user);
                    cmd.Parameters.AddWithValue("@p", pwHash);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch { return false; }
        }

        public static (bool ok, string role) Login(string user, string pwHash)
        {
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "SELECT role FROM users WHERE username=@u AND password_hash=@p", c);
                    cmd.Parameters.AddWithValue("@u", user);
                    cmd.Parameters.AddWithValue("@p", pwHash);
                    var r = cmd.ExecuteScalar();
                    return r != null ? (true, r.ToString()) : (false, null);
                }
            }
            catch { return (false, null); }
        }

        public static List<string> GetAllUsernames()
        {
            var list = new List<string>();
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var r = new MySqlCommand(
                        "SELECT username FROM users ORDER BY username", c).ExecuteReader();
                    while (r.Read()) list.Add(r.GetString(0));
                }
            }
            catch { }
            return list;
        }

        // ── Messages ──────────────────────────────────────────────────────────

        public static List<(string sender, string receiver, string plain, string time)>
            GetAllMessages(string username)
        {
            var list = new List<(string, string, string, string)>();
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "SELECT sender, receiver, message_plain, sent_at FROM messages" +
                        " WHERE message_plain IS NOT NULL AND message_plain <> ''" +
                        "   AND (sender=@u OR (receiver=@u AND delivered=1))" +
                        " ORDER BY sent_at ASC", c);
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

        /// <summary>
        /// Simpan pesan ke DB.
        /// encMsg = ciphertext AES (untuk audit trail).
        /// plain  = plaintext  (untuk dikirim ulang saat recipient login offline).
        /// </summary>
        public static void SaveMessage(string sender, string receiver,
                                       string encMsg, string plain)
        {
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "INSERT INTO messages" +
                        "(sender,receiver,message_encrypted,message_plain,sent_at,delivered)" +
                        " VALUES(@s,@r,@enc,@plain,NOW(),0)", c);
                    cmd.Parameters.AddWithValue("@s",     sender);
                    cmd.Parameters.AddWithValue("@r",     receiver);
                    cmd.Parameters.AddWithValue("@enc",   encMsg);
                    cmd.Parameters.AddWithValue("@plain", plain);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        /// <summary>
        /// Ambil pesan pending (belum terkirim) untuk receiver, lalu tandai delivered.
        /// </summary>
        public static List<(string sender, string plain, string time)>
            GetPending(string receiver)
        {
            var list = new List<(string, string, string)>();
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "SELECT sender, message_plain, sent_at FROM messages" +
                        " WHERE receiver=@r AND delivered=0 ORDER BY sent_at", c);
                    cmd.Parameters.AddWithValue("@r", receiver);
                    var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        string plain = r.IsDBNull(1) ? "" : r.GetString(1);
                        list.Add((r.GetString(0), plain,
                                  r.GetDateTime(2).ToString("HH:mm")));
                    }
                }
                // Tandai semua sudah terkirim
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "UPDATE messages SET delivered=1" +
                        " WHERE receiver=@r AND delivered=0", c);
                    cmd.Parameters.AddWithValue("@r", receiver);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
            return list;
        }

        // ── Session Audit Log ─────────────────────────────────────────────────

        /// <summary>
        /// Catat session token ke tabel sessions (audit trail).
        /// Tabel dibuat otomatis jika belum ada.
        /// </summary>
        public static void LogSession(string username, string token, DateTime expiry)
        {
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    // Buat tabel jika belum ada
                    new MySqlCommand(
                        "CREATE TABLE IF NOT EXISTS sessions(" +
                        " id INT AUTO_INCREMENT PRIMARY KEY," +
                        " username VARCHAR(50) NOT NULL," +
                        " token VARCHAR(64) NOT NULL," +
                        " issued_at DATETIME NOT NULL," +
                        " expires_at DATETIME NOT NULL," +
                        " is_valid TINYINT(1) NOT NULL DEFAULT 1" +
                        ")", c).ExecuteNonQuery();

                    // Invalidate session lama milik user ini
                    var inv = new MySqlCommand(
                        "UPDATE sessions SET is_valid=0" +
                        " WHERE username=@u AND is_valid=1", c);
                    inv.Parameters.AddWithValue("@u", username);
                    inv.ExecuteNonQuery();

                    // Insert session baru
                    var ins = new MySqlCommand(
                        "INSERT INTO sessions(username,token,issued_at,expires_at,is_valid)" +
                        " VALUES(@u,@t,NOW(),@e,1)", c);
                    ins.Parameters.AddWithValue("@u", username);
                    ins.Parameters.AddWithValue("@t", token);
                    ins.Parameters.AddWithValue("@e",
                        expiry.ToString("yyyy-MM-dd HH:mm:ss"));
                    ins.ExecuteNonQuery();
                }
            }
            catch { }
        }

        public static void InvalidateSession(string token)
        {
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "UPDATE sessions SET is_valid=0 WHERE token=@t", c);
                    cmd.Parameters.AddWithValue("@t", token);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  CLIENT INFO
    // ════════════════════════════════════════════════════════════════════════════
    public class ServerClientInfo
    {
        public TcpClient    TcpClient;
        public StreamWriter Writer;
        public byte[]       AesKey;
        public byte[]       AesIv;
        public string       Role;
        public string       SessionToken;
        public DateTime     TokenExpiry;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  CLIENT MANAGER
    // ════════════════════════════════════════════════════════════════════════════
    public static class ServerClients
    {
        private static readonly Dictionary<string, ServerClientInfo> _clients =
            new Dictionary<string, ServerClientInfo>();

        public static void Add(string user, ServerClientInfo info)
        {
            lock (_clients) _clients[user] = info;
            Broadcast();
        }

        public static void Remove(string user)
        {
            lock (_clients)
            {
                if (_clients.ContainsKey(user)) _clients.Remove(user);
            }
            Broadcast();
        }

        public static ServerClientInfo Get(string user)
        {
            lock (_clients)
                return _clients.ContainsKey(user) ? _clients[user] : null;
        }

        private static HashSet<string> OnlineSet()
        {
            lock (_clients) return new HashSet<string>(_clients.Keys);
        }

        private static List<ServerClientInfo> AllInfos()
        {
            lock (_clients) return new List<ServerClientInfo>(_clients.Values);
        }

        /// <summary>
        /// Broadcast daftar semua user (DB) + status online/offline ke semua client.
        /// Format: ALL_USERS|user1:online,user2:offline,...
        /// </summary>
        public static void Broadcast()
        {
            var all    = ServerDb.GetAllUsernames();
            var online = OnlineSet();
            var parts  = all.ConvertAll(
                u => u + ":" + (online.Contains(u) ? "online" : "offline"));
            string payload = string.Join(",", parts);

            foreach (var info in AllInfos())
            {
                try { info.Writer.WriteLine("ALL_USERS|" + payload); }
                catch { }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  CONNECTION HANDLER  (satu instance per client)
    // ════════════════════════════════════════════════════════════════════════════
    public class ServerHandler
    {
        // RSA keypair dibuat sekali per proses, dipakai semua koneksi
        private static readonly RSACryptoServiceProvider _rsa =
            new RSACryptoServiceProvider(2048);
        private static readonly string _pubXml  = _rsa.ToXmlString(false);
        private static readonly string _privXml = _rsa.ToXmlString(true);

        private readonly TcpClient    _tcp;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private string _username = "";
        private byte[] _aesKey, _aesIv;

        public ServerHandler(TcpClient client)
        {
            _tcp    = client;
            _reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            _writer = new StreamWriter(client.GetStream(), Encoding.UTF8)
                          { AutoFlush = true };
        }

        public void Start() =>
            new Thread(Run) { IsBackground = true }.Start();

        // ── Main loop ─────────────────────────────────────────────────────────

        private void Run()
        {
            try
            {
                // 1. Kirim RSA public key ke client
                _writer.WriteLine("PUBKEY|" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(_pubXml)));

                // 2. Terima KEY_EXCHANGE: client mengirim AES key+IV
                //    yang sudah di-enkripsi dengan RSA public key server
                string keLine = _reader.ReadLine();
                if (keLine == null || !keLine.StartsWith("KEY_EXCHANGE|")) return;

                string[] kp = keLine.Split('|');
                _aesKey = CryptoHelper.RsaDecrypt(
                              Convert.FromBase64String(kp[1]), _privXml);
                _aesIv  = CryptoHelper.RsaDecrypt(
                              Convert.FromBase64String(kp[2]), _privXml);

                // 3. Loop terima command dari client
                while (true)
                {
                    string line = _reader.ReadLine();
                    if (line == null) break;

                    string[] parts = line.Split('|');
                    switch (parts[0])
                    {
                        case "REGISTER":      HandleRegister(parts);      break;
                        case "LOGIN":         HandleLogin(parts);         break;
                        case "CHAT":          HandleChat(parts);          break;
                        case "TOKEN_REFRESH": HandleTokenRefresh(parts);  break;
                    }
                }
            }
            catch { }

            Disconnect();
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleRegister(string[] p)
        {
            // Format: REGISTER|username|sha256_password
            if (p.Length < 3)
            {
                _writer.WriteLine("REGISTER_FAIL|Format salah");
                return;
            }

            bool ok = ServerDb.Register(p[1], p[2]);
            _writer.WriteLine(ok
                ? "REGISTER_SUCCESS"
                : "REGISTER_FAIL|Username sudah dipakai");
        }

        private void HandleLogin(string[] p)
        {
            // Format: LOGIN|username|sha256_password
            if (p.Length < 3)
            {
                _writer.WriteLine("LOGIN_FAIL|Format salah");
                return;
            }

            var (ok, role) = ServerDb.Login(p[1], p[2]);
            if (!ok)
            {
                _writer.WriteLine("LOGIN_FAIL|Username atau password salah");
                return;
            }

            _username = p[1];

            // Generate session token berlaku 1 jam
            string   token  = Guid.NewGuid().ToString("N");
            DateTime expiry = DateTime.Now.AddHours(1);

            var info = new ServerClientInfo
            {
                TcpClient    = _tcp,
                Writer       = _writer,
                AesKey       = _aesKey,
                AesIv        = _aesIv,
                Role         = role,
                SessionToken = token,
                TokenExpiry  = expiry
            };

            // Kirim LOGIN_SUCCESS sebelum AddClient (hindari race condition USERS)
            // Format: LOGIN_SUCCESS|role|token|expiry_time
            _writer.WriteLine(
                $"LOGIN_SUCCESS|{role}|{token}|{expiry:HH:mm:ss}");

            ServerClients.Add(_username, info);

            // Catat session ke DB untuk audit trail
            ServerDb.LogSession(_username, token, expiry);

            // Kirim riwayat chat lengkap (HISTORY) sebagai HISTORY|sender|receiver|enc|time
            foreach (var (s, r, plain, t) in ServerDb.GetAllMessages(_username))
            {
                string reEnc = CryptoHelper.AesEncrypt(plain, _aesKey, _aesIv);
                _writer.WriteLine($"HISTORY|{s}|{r}|{reEnc}|{t}");
            }

            // Kirim pesan yang masuk saat user offline
            foreach (var (sender, plain, time) in ServerDb.GetPending(_username))
            {
                if (string.IsNullOrEmpty(plain)) continue;
                string reEnc = CryptoHelper.AesEncrypt(plain, _aesKey, _aesIv);
                _writer.WriteLine($"MSG|{sender}|{reEnc}|{time}");
            }

            Console.WriteLine($"[+] {_username} ({role}) connected");
        }

        private void HandleChat(string[] p)
        {
            // Format: CHAT|from|to|aes_encrypted_msg|session_token
            if (p.Length < 5) return;

            string from    = p[1];
            string to      = p[2];
            string encMsg  = p[3];
            string token   = p[4];

            // Validasi session token
            var sender = ServerClients.Get(from);
            if (sender == null || sender.SessionToken != token)
            {
                _writer.WriteLine("ERROR|Session tidak valid, silakan login ulang.");
                return;
            }
            if (DateTime.Now > sender.TokenExpiry)
            {
                _writer.WriteLine("ERROR|Session expired, silakan login ulang.");
                ServerClients.Remove(from);
                return;
            }

            // Dekripsi pesan dari sender
            string plain = CryptoHelper.AesDecrypt(encMsg, _aesKey, _aesIv);

            // Simpan ke DB (ciphertext + plaintext)
            ServerDb.SaveMessage(from, to, encMsg, plain);

            // Forward ke recipient jika online
            var recipient = ServerClients.Get(to);
            if (recipient != null)
            {
                string reEnc = CryptoHelper.AesEncrypt(
                    plain, recipient.AesKey, recipient.AesIv);
                try { recipient.Writer.WriteLine($"MSG|{from}|{reEnc}"); }
                catch { }
            }
            // Jika offline: pesan tersimpan di DB, dikirim saat mereka login
        }

        private void HandleTokenRefresh(string[] p)
        {
            // Format: TOKEN_REFRESH|username|old_token
            if (p.Length < 3) return;

            string user     = p[1];
            string oldToken = p[2];

            var info = ServerClients.Get(user);
            if (info == null || info.SessionToken != oldToken)
            {
                _writer.WriteLine("TOKEN_REFRESH_FAIL|Session tidak valid.");
                return;
            }

            // Generate token baru, perpanjang 1 jam
            string   newToken  = Guid.NewGuid().ToString("N");
            DateTime newExpiry = DateTime.Now.AddHours(1);

            info.SessionToken = newToken;
            info.TokenExpiry  = newExpiry;

            // Update audit log
            ServerDb.InvalidateSession(oldToken);
            ServerDb.LogSession(user, newToken, newExpiry);

            // Kirim token baru ke client
            // Format: TOKEN_REFRESHED|newToken|newExpiry_time
            _writer.WriteLine(
                $"TOKEN_REFRESHED|{newToken}|{newExpiry:HH:mm:ss}");
        }

        private void Disconnect()
        {
            Console.WriteLine($"[-] {_username} disconnected");
            if (!string.IsNullOrEmpty(_username))
            {
                // Invalidate session saat disconnect
                var info = ServerClients.Get(_username);
                if (info != null)
                    ServerDb.InvalidateSession(info.SessionToken);

                ServerClients.Remove(_username);
            }
            try { _tcp.Close(); } catch { }
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  SERVER HOST  — start listener di background thread
    // ════════════════════════════════════════════════════════════════════════════
    public static class ServerHost
    {
        public static void Start()
        {
            new Thread(() =>
            {
                try
                {
                    var listener = new TcpListener(IPAddress.Any, 6000);
                    listener.Start();
                    Console.WriteLine("[Server] Listening on port 6000...");

                    while (true)
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        new ServerHandler(client).Start();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Server ERROR] " + ex.Message);
                }
            })
            { IsBackground = true }.Start();
        }
    }
}
