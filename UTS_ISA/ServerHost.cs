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
    //
    //  Mengelola semua operasi MySQL: users, messages, sessions.
    //
    //  LAPISAN ENKRIPSI (3 lapis):
    //  ┌─────────────────────────────────────────────────────────────────────┐
    //  │  Layer 1 — Transit (AES session key per client)                     │
    //  │    Pesan di jaringan dienkripsi AES-256 dengan key unik per sesi.   │
    //  │    Disadap pun tidak bisa dibaca.                                   │
    //  │                                                                     │
    //  │  Layer 2 — DB kolom message_encrypted (AES session key sender)      │
    //  │    Ciphertext asli dari sender disimpan sebagai audit trail.        │
    //  │    Hanya bisa dibuka dengan AES key milik sender (sudah hilang      │
    //  │    setelah sesi berakhir) → tidak bisa dibuka siapapun.             │
    //  │                                                                     │
    //  │  Layer 3 — DB kolom message_backup (AES server DB key)  ← BARU      │
    //  │    Plaintext ikut dienkripsi pakai fixed server key sebelum         │
    //  │    disimpan ke DB. Siapapun yang buka phpMyAdmin tidak akan         │
    //  │    bisa membaca isi pesan. Server decrypt dulu saat perlu           │
    //  │    mengirim pesan ke recipient (offline delivery / history).        │
    //  └─────────────────────────────────────────────────────────────────────┘
    // ════════════════════════════════════════════════════════════════════════════
    public static class ServerDb
    {
        // Ganti Pwd= jika MySQL kamu pakai password
        private const string ConnStr =
            "Server=localhost;Database=secure_chat;Uid=root;Pwd=;";

        // ── Server-side DB encryption key (Layer 3) ───────────────────────────
        //
        // Key ini KHUSUS untuk enkripsi data di database, berbeda dengan
        // AES session key yang dipakai untuk enkripsi pesan di jaringan.
        //
        // _dbKey : di-derive dari passphrase tetap menggunakan SHA-256
        //          hasil = 32 byte (256-bit AES key)
        // _dbIv  : fixed IV 16 byte (hardcoded)
        //
        // Kenapa pakai SHA-256 untuk derive key?
        //   Agar key selalu sama setiap kali server restart, tapi tidak
        //   perlu disimpan as-is di kode (lebih aman dari hardcode langsung).
        //
        // PENTING: Ganti passphrase "UTS_ISA_SECURE_DB_KEY_2024" dengan
        //   string rahasia sendiri sebelum deploy ke production.
        private static readonly byte[] _dbKey;
        private static readonly byte[] _dbIv = new byte[]
        {
            0x55,0x54,0x53,0x5F,0x49,0x53,0x41,0x5F,  // "UTS_ISA_"
            0x32,0x30,0x32,0x34,0x00,0x00,0x00,0x00   // "2024\0\0\0\0"
        };

        static ServerDb()
        {
            // Derive 256-bit DB key dari passphrase menggunakan SHA-256
            using (var sha = SHA256.Create())
                _dbKey = sha.ComputeHash(Encoding.UTF8.GetBytes("UTS_ISA_SECURE_DB_KEY_2024"));
        }

        /// <summary>Enkripsi plaintext dengan server DB key sebelum disimpan ke DB.</summary>
        private static string DbEncrypt(string plain)
            => CryptoHelper.AesEncrypt(plain, _dbKey, _dbIv);

        /// <summary>
        /// Dekripsi data dari kolom message_backup di DB.
        /// Jika gagal (misal data lama yang belum terenkripsi), kembalikan string kosong.
        /// </summary>
        private static string DbDecrypt(string cipher)
        {
            try { return CryptoHelper.AesDecrypt(cipher, _dbKey, _dbIv); }
            catch { return ""; } // data lama (plaintext) tidak bisa di-decrypt → skip
        }

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
                        "SELECT sender, receiver, message_backup, sent_at FROM messages" +
                        " WHERE message_backup IS NOT NULL AND message_backup <> ''" +
                        "   AND (sender=@u OR (receiver=@u AND delivered=1))" +
                        " ORDER BY sent_at ASC", c);
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

        /// <summary>
        /// Simpan pesan ke DB.
        /// encMsg = ciphertext AES (untuk audit trail).
        /// plain  = plaintext  (untuk dikirim ulang saat recipient login offline).
        /// </summary>
        /// <summary>
        /// Simpan pesan ke DB dengan delivered=0 (belum terkirim ke penerima).
        /// Mengembalikan ID row agar bisa di-update delivered=1 setelah forward berhasil.
        ///
        /// KOLOM delivered:
        ///   0 = pesan "menunggu" di server, penerima belum menerimanya
        ///       → terjadi saat penerima sedang OFFLINE
        ///       → akan berubah ke 1 saat penerima login (via GetPending)
        ///   1 = pesan sudah sampai ke penerima
        ///       → terjadi saat penerima ONLINE dan forward berhasil (via MarkDelivered)
        ///       → atau saat penerima login dan ambil pesan pending
        ///
        /// Analogi: seperti centang WhatsApp
        ///   delivered=0 → ✓  (terkirim ke server, belum ke penerima)
        ///   delivered=1 → ✓✓ (sudah diterima)
        /// </summary>
        /// <returns>ID row yang baru diinsert, atau -1 jika gagal</returns>
        public static long SaveMessage(string sender, string receiver,
                                       string encMsg, string plain)
        {
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "INSERT INTO messages" +
                        "(sender,receiver,message_encrypted,message_backup,sent_at,delivered)" +
                        " VALUES(@s,@r,@enc,@plain,NOW(),0)", c);
                    cmd.Parameters.AddWithValue("@s",     sender);
                    cmd.Parameters.AddWithValue("@r",     receiver);
                    cmd.Parameters.AddWithValue("@enc",   encMsg);
                    // Enkripsi plaintext dengan server DB key sebelum disimpan
                    // → DB tidak pernah menyimpan plaintext yang bisa dibaca langsung
                    cmd.Parameters.AddWithValue("@plain", DbEncrypt(plain));
                    cmd.ExecuteNonQuery();
                    return cmd.LastInsertedId; // ID dipakai oleh MarkDelivered()
                }
            }
            catch { return -1; }
        }

        /// <summary>
        /// Update delivered=1 untuk pesan tertentu berdasarkan ID.
        ///
        /// Dipanggil di dua situasi:
        ///   1. Recipient ONLINE  → dipanggil di HandleChat setelah Writer.WriteLine berhasil
        ///   2. Recipient OFFLINE → dipanggil di GetPending setelah semua pesan dikirim
        ///
        /// Jika WriteLine gagal (koneksi putus mendadak), MarkDelivered tidak dipanggil
        /// sehingga delivered tetap 0 → pesan akan dikirim ulang saat recipient login lagi.
        /// </summary>
        public static void MarkDelivered(long messageId)
        {
            if (messageId < 0) return;
            try
            {
                using (var c = new MySqlConnection(ConnStr))
                {
                    c.Open();
                    var cmd = new MySqlCommand(
                        "UPDATE messages SET delivered=1 WHERE id=@id", c);
                    cmd.Parameters.AddWithValue("@id", messageId);
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
                        "SELECT sender, message_backup, sent_at FROM messages" +
                        " WHERE receiver=@r AND delivered=0 ORDER BY sent_at", c);
                    cmd.Parameters.AddWithValue("@r", receiver);
                    var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        string raw   = r.IsDBNull(1) ? "" : r.GetString(1);
                        string plain = DbDecrypt(raw); // dekripsi dari DB key
                        if (!string.IsNullOrEmpty(plain))
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
                // ── STEP 1: Kirim RSA public key ke client ────────────────────
                // Encode XML public key ke Base64 agar aman dikirim sebagai satu baris teks
                // Format yang dikirim: PUBKEY|<base64 encoded XML RSA public key>
                _writer.WriteLine("PUBKEY|" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(_pubXml)));

                // ── STEP 2: Terima KEY_EXCHANGE dari client ───────────────────
                // Client sudah punya RSA public key kita → client generate AES key+IV random
                // → enkripsi AES key+IV pakai RSA public key → kirim kemari
                string keLine = _reader.ReadLine(); // baca satu baris dari client
                if (keLine == null || !keLine.StartsWith("KEY_EXCHANGE|")) return; // validasi format

                string[] kp = keLine.Split('|'); // pisah: ["KEY_EXCHANGE", "<base64 encKey>", "<base64 encIV>"]

                // Dekripsi AES key + IV menggunakan RSA private key server
                // Hanya server yang bisa melakukan ini karena hanya server yang punya private key
                _aesKey = CryptoHelper.RsaDecrypt(Convert.FromBase64String(kp[1]), _privXml); // AES key 32 byte
                _aesIv  = CryptoHelper.RsaDecrypt(Convert.FromBase64String(kp[2]), _privXml); // AES IV 16 byte
                // Sekarang server tahu AES key+IV yang sama dengan client → siap enkripsi pesan

                // ── STEP 3: Loop terima dan proses command dari client ────────
                while (true)
                {
                    string line = _reader.ReadLine(); // baca satu baris command dari client
                    if (line == null) break;           // null = koneksi terputus → keluar loop

                    string[] parts = line.Split('|'); // pisah command dan argumennya

                    // Dispatch ke handler yang sesuai berdasarkan kata pertama
                    switch (parts[0])
                    {
                        case "REGISTER":      HandleRegister(parts);      break; // proses registrasi
                        case "LOGIN":         HandleLogin(parts);         break; // proses login
                        case "CHAT":          HandleChat(parts);          break; // proses kirim pesan
                        case "TOKEN_REFRESH": HandleTokenRefresh(parts);  break; // refresh token sesi
                    }
                }
            }
            catch { } // koneksi putus mendadak (IOException/SocketException)

            Disconnect(); // bersihkan data client saat keluar loop
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE REGISTER
        //  Mendaftarkan user baru ke database.
        //  Format command dari client: REGISTER|username|sha256_password
        // ════════════════════════════════════════════════════════════════════════
        private void HandleRegister(string[] p)
        {
            // Validasi: pastikan ada minimal 3 bagian (REGISTER|username|pwHash)
            if (p.Length < 3)
            {
                _writer.WriteLine("REGISTER_FAIL|Format salah"); // kirim error ke client
                return;
            }

            // Coba daftarkan ke DB; false jika username sudah dipakai (duplicate)
            bool ok = ServerDb.Register(p[1], p[2]); // p[1]=username, p[2]=passwordHash

            // Kirim hasil ke client
            _writer.WriteLine(ok
                ? "REGISTER_SUCCESS"                       // berhasil → client tampilkan sukses
                : "REGISTER_FAIL|Username sudah dipakai"); // gagal → client tampilkan pesan error
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE LOGIN
        //  Verifikasi kredensial dan buat session token untuk user.
        //  Format command dari client: LOGIN|username|sha256_password
        // ════════════════════════════════════════════════════════════════════════
        private void HandleLogin(string[] p)
        {
            // Validasi: pastikan ada minimal 3 bagian (LOGIN|username|pwHash)
            if (p.Length < 3)
            {
                _writer.WriteLine("LOGIN_FAIL|Format salah");
                return;
            }

            // Cek kombinasi username + passwordHash di DB
            var (ok, role) = ServerDb.Login(p[1], p[2]); // p[1]=username, p[2]=passwordHash
            if (!ok)
            {
                // Credentials tidak cocok → kirim FAIL ke client
                _writer.WriteLine("LOGIN_FAIL|Username atau password salah");
                return;
            }

            _username = p[1]; // simpan username untuk dipakai handler lain (HandleChat, dll)

            // Generate session token: GUID 32 karakter tanpa tanda hubung
            // Contoh: "b541001d3a2f4c8e9d1f2a3b4c5d6e7f"
            string   token  = Guid.NewGuid().ToString("N"); // "N" = tanpa tanda hubung
            DateTime expiry = DateTime.Now.AddHours(1);     // token berlaku 1 jam dari sekarang

            // Buat objek yang menyimpan semua info sesi client ini
            var info = new ServerClientInfo
            {
                TcpClient    = _tcp,     // koneksi TCP client ini
                Writer       = _writer,  // stream untuk kirim ke client ini
                AesKey       = _aesKey,  // AES key unik sesi ini (dari key exchange)
                AesIv        = _aesIv,   // AES IV unik sesi ini (dari key exchange)
                Role         = role,     // "user" atau "admin"
                SessionToken = token,    // token untuk validasi setiap CHAT command
                TokenExpiry  = expiry    // waktu token kedaluwarsa
            };

            // PENTING: Kirim LOGIN_SUCCESS SEBELUM ServerClients.Add()
            // Jika Add() dulu → Broadcast() dipanggil → mungkin race condition dengan client lain
            // Format yang dikirim: LOGIN_SUCCESS|role|token|expiry_time
            _writer.WriteLine($"LOGIN_SUCCESS|{role}|{token}|{expiry:HH:mm:ss}");

            // Daftarkan client ke manager → otomatis trigger Broadcast ALL_USERS ke semua online
            ServerClients.Add(_username, info);

            // Catat session ke tabel sessions di DB untuk audit trail keamanan
            ServerDb.LogSession(_username, token, expiry);

            // Kirim riwayat chat lengkap dari DB ke client yang baru login
            // Format tiap pesan: HISTORY|sender|receiver|encMsg|time
            foreach (var (s, r, plain, t) in ServerDb.GetAllMessages(_username))
            {
                string reEnc = CryptoHelper.AesEncryptMsg(plain, _aesKey); // IV baru random tiap pesan
                _writer.WriteLine($"HISTORY|{s}|{r}|{reEnc}|{t}"); // kirim ke client
            }

            // Kirim pesan yang masuk saat user offline (delivered=0 di DB)
            // Format tiap pesan: MSG|sender|encMsg|sentAt
            foreach (var (sender, plain, time) in ServerDb.GetPending(_username))
            {
                if (string.IsNullOrEmpty(plain)) continue; // skip jika dekripsi gagal
                string reEnc = CryptoHelper.AesEncryptMsg(plain, _aesKey); // IV baru random tiap pesan
                _writer.WriteLine($"MSG|{sender}|{reEnc}|{time}"); // kirim sebagai MSG biasa
            }
            // GetPending otomatis update delivered=1 setelah semua pesan diambil

            Console.WriteLine($"[+] {_username} ({role}) connected"); // log di console server
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE CHAT
        //  Proses pesan yang dikirim client ke user lain.
        //  Format command dari client: CHAT|from|to|aes_encrypted_msg|session_token
        //
        //  Alur:
        //    1. Validasi session token (cegah spoofing / session expired)
        //    2. Dekripsi pesan dari sender (pakai AES key sender)
        //    3. Simpan ke DB terenkripsi (delivered=0)
        //    4. Jika recipient online → re-enkripsi & forward → delivered=1
        //    5. Jika recipient offline → delivered=0, kirim saat login
        // ════════════════════════════════════════════════════════════════════════
        private void HandleChat(string[] p)
        {
            if (p.Length < 5) return; // validasi: harus ada 5 bagian

            string from   = p[1]; // username pengirim
            string to     = p[2]; // username penerima
            string encMsg = p[3]; // pesan terenkripsi AES (dengan key milik sender)
            string token  = p[4]; // session token dari client (harus cocok dengan server)

            // ── Validasi session token ────────────────────────────────────────
            var sender = ServerClients.Get(from); // ambil info sender dari manager

            // Cek: apakah token yang dikirim cocok dengan yang ada di server?
            if (sender == null || sender.SessionToken != token)
            {
                _writer.WriteLine("ERROR|Session tidak valid, silakan login ulang.");
                return; // tolak pesan
            }

            // Cek: apakah token sudah expired (lebih dari 1 jam)?
            if (DateTime.Now > sender.TokenExpiry)
            {
                _writer.WriteLine("ERROR|Session expired, silakan login ulang.");
                ServerClients.Remove(from); // hapus dari daftar online
                return;
            }

            // ── Dekripsi pesan dari sender ────────────────────────────────────
            // _aesKey/_aesIv di sini adalah key milik pengirim (dari key exchange saat login)
            string plain = CryptoHelper.AesDecryptMsg(encMsg, _aesKey);
            // plain = isi pesan yang bisa dibaca manusia (contoh: "halo, apa kabar?")

            // ── Simpan ke DB ──────────────────────────────────────────────────
            // encMsg  → kolom message_encrypted (audit trail)
            // plain   → dienkripsi DB key → kolom message_backup
            // delivered=0 → belum terkirim ke penerima
            long msgId = ServerDb.SaveMessage(from, to, encMsg, plain); // dapat ID row baru

            // ── Forward ke recipient jika online ──────────────────────────────
            var recipient = ServerClients.Get(to); // null jika recipient sedang offline

            if (recipient != null) // recipient sedang online
            {
                // Re-enkripsi plaintext dengan AES key milik recipient
                // (key berbeda tiap client, jadi harus re-enkripsi dulu)
                string reEnc = CryptoHelper.AesEncryptMsg(plain, recipient.AesKey);
                try
                {
                    recipient.Writer.WriteLine($"MSG|{from}|{reEnc}"); // kirim ke recipient

                    // Forward berhasil → update delivered=1 (analogi: ✓✓ WhatsApp)
                    ServerDb.MarkDelivered(msgId);
                }
                catch { }
                // Jika WriteLine gagal (koneksi recipient putus mendadak):
                // MarkDelivered tidak dipanggil → delivered tetap 0
                // → pesan akan dikirim ulang saat recipient login kembali
            }
            // Jika offline: delivered=0 tetap, kirim via GetPending saat recipient login
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE TOKEN REFRESH
        //  Client meminta token baru sebelum token lama expired.
        //  Format command dari client: TOKEN_REFRESH|username|old_token
        // ════════════════════════════════════════════════════════════════════════
        private void HandleTokenRefresh(string[] p)
        {
            if (p.Length < 3) return; // validasi argumen

            string user     = p[1]; // username yang minta refresh
            string oldToken = p[2]; // token lama yang sedang dipakai client

            // Ambil info client dan validasi token lama
            var info = ServerClients.Get(user);
            if (info == null || info.SessionToken != oldToken)
            {
                // Token tidak cocok → session tidak valid
                _writer.WriteLine("TOKEN_REFRESH_FAIL|Session tidak valid.");
                return;
            }

            // Generate token baru + perpanjang expiry 1 jam lagi dari sekarang
            string   newToken  = Guid.NewGuid().ToString("N"); // GUID baru yang unik
            DateTime newExpiry = DateTime.Now.AddHours(1);     // 1 jam dari sekarang

            // Update in-memory dengan token baru
            info.SessionToken = newToken;
            info.TokenExpiry  = newExpiry;

            // Update audit log di DB: invalidate token lama, catat token baru
            ServerDb.InvalidateSession(oldToken); // tandai token lama is_valid=0
            ServerDb.LogSession(user, newToken, newExpiry); // insert token baru

            // Kirim konfirmasi ke client beserta token baru dan waktu expiry baru
            // Format: TOKEN_REFRESHED|newToken|newExpiry_time
            _writer.WriteLine($"TOKEN_REFRESHED|{newToken}|{newExpiry:HH:mm:ss}");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  DISCONNECT
        //  Dipanggil saat koneksi client terputus (normal atau mendadak).
        //  Hapus client dari daftar online → trigger broadcast ALL_USERS baru.
        // ════════════════════════════════════════════════════════════════════════
        private void Disconnect()
        {
            Console.WriteLine($"[-] {_username} disconnected"); // log di console server

            if (!string.IsNullOrEmpty(_username)) // hanya jika sudah login
            {
                // Invalidate session token di DB (audit trail: tandai session berakhir)
                var info = ServerClients.Get(_username);
                if (info != null)
                    ServerDb.InvalidateSession(info.SessionToken); // is_valid=0 di tabel sessions

                // Hapus dari daftar online → otomatis trigger Broadcast ALL_USERS
                ServerClients.Remove(_username);
            }
            try { _tcp.Close(); } catch { } // tutup koneksi TCP (abaikan error jika sudah tutup)
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
