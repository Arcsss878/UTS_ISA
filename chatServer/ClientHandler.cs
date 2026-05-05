using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace UTS_ISA.users
{
    // ════════════════════════════════════════════════════════════════════════════
    //  CLIENT HANDLER
    //
    //  Satu instance ClientHandler = satu client yang terhubung ke server.
    //  Setiap client punya thread sendiri (background thread) agar server
    //  bisa melayani banyak client secara bersamaan (concurrent).
    //
    //  ALUR KONEKSI:
    //    1. Server terima koneksi baru → buat ClientHandler → panggil Start()
    //    2. Start() buat background thread → jalankan Run()
    //    3. Run() lakukan RSA key exchange → loop baca command dari client
    //    4. Setiap command di-dispatch ke handler yang sesuai
    //    5. Jika koneksi putus → Disconnect() dipanggil → bersihkan data
    // ════════════════════════════════════════════════════════════════════════════
    public class ClientHandler
    {
        // ── RSA Keypair (static = dibuat SEKALI saat server start) ───────────────
        // Kenapa static? Agar semua ClientHandler pakai RSA keypair yang sama.
        // RSA-2048 artinya panjang key 2048 bit → sangat aman untuk key exchange.
        private static readonly System.Security.Cryptography.RSACryptoServiceProvider ServerRsa =
            new System.Security.Cryptography.RSACryptoServiceProvider(2048); // generate keypair baru

        // Public key: boleh dikirim ke client (dipakai client untuk enkripsi AES key)
        private static readonly string ServerPublicKeyXml  = ServerRsa.ToXmlString(false); // false = hanya public key

        // Private key: TIDAK pernah dikirim, hanya server yang tahu
        private static readonly string ServerPrivateKeyXml = ServerRsa.ToXmlString(true);  // true = include private key

        // ── Data per-koneksi (tidak static = berbeda tiap client) ────────────────
        private TcpClient    client;   // koneksi TCP dengan client ini
        private StreamReader reader;   // untuk baca data masuk dari client
        private StreamWriter writer;   // untuk kirim data ke client
        private string       username = ""; // username setelah login berhasil
        private byte[]       aesKey;  // AES key unik untuk sesi ini (dari key exchange)
        private byte[]       aesIv;   // AES IV unik untuk sesi ini (dari key exchange)

        /// <summary>
        /// Constructor — dipanggil oleh ServerHost setiap ada koneksi baru.
        /// </summary>
        public ClientHandler(TcpClient client)
        {
            this.client = client;

            // Bungkus stream TCP dengan StreamReader/Writer agar bisa baca/tulis per-baris
            reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
            // AutoFlush = true → data langsung dikirim tanpa perlu Flush() manual
        }

        /// <summary>
        /// Mulai background thread untuk handle client ini.
        /// IsBackground = true → thread otomatis berhenti saat app ditutup.
        /// </summary>
        public void Start()
        {
            new Thread(Run) { IsBackground = true }.Start();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  MAIN LOOP — berjalan di background thread
        // ════════════════════════════════════════════════════════════════════════
        private void Run()
        {
            try
            {
                // ── STEP 1: Kirim RSA public key ke client ────────────────────
                // Client butuh public key untuk mengenkripsi AES key yang akan dikirim ke server.
                // Public key di-encode ke Base64 agar aman dikirim sebagai teks.
                string pubKeyB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ServerPublicKeyXml));
                writer.WriteLine("PUBKEY|" + pubKeyB64); // format: PUBKEY|<base64>

                // ── STEP 2: Terima KEY_EXCHANGE dari client ───────────────────
                // Client mengirim AES key+IV yang sudah dienkripsi pakai RSA public key kita.
                // Hanya server (pemilik private key) yang bisa mendekripsinya.
                string keyExchangeMsg = reader.ReadLine(); // tunggu sampai client kirim KEY_EXCHANGE

                // Validasi: pastikan format pesan benar
                if (keyExchangeMsg == null || !keyExchangeMsg.StartsWith("KEY_EXCHANGE|"))
                    return; // koneksi tidak valid, putuskan

                string[] keParts = keyExchangeMsg.Split('|'); // pisah berdasarkan '|'
                byte[] encAesKey = Convert.FromBase64String(keParts[1]); // AES key terenkripsi RSA
                byte[] encAesIv  = Convert.FromBase64String(keParts[2]); // AES IV terenkripsi RSA

                // Dekripsi AES key+IV menggunakan RSA private key server
                // Hasilnya: AES key 32 byte + IV 16 byte yang sama dengan yang ada di client
                aesKey = CryptoHelper.RsaDecrypt(encAesKey, ServerPrivateKeyXml);
                aesIv  = CryptoHelper.RsaDecrypt(encAesIv,  ServerPrivateKeyXml);
                // Mulai sekarang, semua pesan dari/ke client ini akan dienkripsi AES

                // ── STEP 3: Loop terima dan proses command dari client ────────
                while (true)
                {
                    string data = reader.ReadLine(); // baca satu baris dari client
                    if (data == null) break;          // null = koneksi terputus → keluar loop

                    string[] parts = data.Split('|'); // pisah command dan argumennya

                    // Dispatch ke handler sesuai command pertama
                    switch (parts[0])
                    {
                        case "REGISTER":      HandleRegister(parts);      break; // proses registrasi
                        case "LOGIN":         HandleLogin(parts);         break; // proses login
                        case "CHAT":          HandleChat(parts);          break; // proses kirim pesan
                        case "TOKEN_REFRESH": HandleTokenRefresh(parts);  break; // refresh session token
                    }
                }
            }
            catch { } // koneksi putus mendadak (exception IOException/SocketException)

            Disconnect(); // bersihkan data client saat keluar loop
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE REGISTER
        //  Mendaftarkan user baru ke database.
        //  Format command dari client: REGISTER|username|sha256_password|role
        // ════════════════════════════════════════════════════════════════════════
        private void HandleRegister(string[] parts)
        {
            // Validasi jumlah argumen (harus ada 4 bagian)
            if (parts.Length < 4) { writer.WriteLine("REGISTER_FAIL|Format salah"); return; }

            string user   = parts[1]; // username yang ingin didaftarkan
            string pwHash = parts[2]; // password yang sudah di-hash SHA-256 di client
            // Role hanya boleh "admin" jika user memang memilih admin, selain itu "user"
            string role   = parts[3] == "admin" ? "admin" : "user";

            // Simpan ke DB; RegisterUser mengembalikan false jika username sudah ada
            bool ok = DatabaseHelper.RegisterUser(user, pwHash, role);

            // Kirim hasil ke client
            writer.WriteLine(ok ? "REGISTER_SUCCESS" : "REGISTER_FAIL|Username sudah dipakai");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE LOGIN
        //  Memverifikasi kredensial dan membuat session untuk user.
        //  Format command dari client: LOGIN|username|sha256_password
        // ════════════════════════════════════════════════════════════════════════
        private void HandleLogin(string[] parts)
        {
            // Validasi jumlah argumen
            if (parts.Length < 3) { writer.WriteLine("LOGIN_FAIL|Format salah"); return; }

            string user   = parts[1]; // username yang ingin login
            string pwHash = parts[2]; // password hash SHA-256 dari client

            // Cek ke DB: apakah kombinasi username+passwordHash ada?
            var (success, role) = DatabaseHelper.LoginUser(user, pwHash);

            if (!success)
            {
                // Credentials salah → kirim FAIL ke client
                writer.WriteLine("LOGIN_FAIL|Username atau password salah");
                return;
            }

            // Login berhasil → simpan username untuk dipakai di handler lain
            username = user;

            // Generate session token: GUID acak 32 karakter (contoh: b541001d3a2f...)
            // Token ini dipakai sebagai "kartu akses" — setiap kirim pesan harus sertakan token ini
            string   token  = Guid.NewGuid().ToString("N"); // "N" = tanpa tanda hubung
            DateTime expiry = DateTime.Now.AddHours(1);     // token berlaku 1 jam dari sekarang

            // Buat objek info client yang menyimpan semua data sesi
            var info = new ClientInfo
            {
                TcpClient    = client,    // koneksi TCP
                Writer       = writer,    // stream untuk kirim ke client ini
                AesKey       = aesKey,    // AES key unik sesi ini
                AesIv        = aesIv,     // AES IV unik sesi ini
                Role         = role,      // "user" atau "admin"
                SessionToken = token,     // token untuk validasi setiap CHAT command
                TokenExpiry  = expiry     // waktu token kedaluwarsa
            };

            // PENTING: Kirim LOGIN_SUCCESS SEBELUM AddClient
            // Jika AddClient dulu → Broadcast() dipanggil → client belum terdaftar
            // tapi server sudah kirim ALL_USERS → race condition
            // Format: LOGIN_SUCCESS|role|token|expiry_time
            writer.WriteLine($"LOGIN_SUCCESS|{role}|{token}|{expiry:HH:mm:ss}");

            // Daftarkan client ke ClientManager (sekarang bisa menerima pesan dari client lain)
            ClientManager.AddClient(username, info);
            // AddClient juga trigger Broadcast ALL_USERS ke semua client online

            // Kirim riwayat chat lengkap dari DB ke client yang baru login
            // Format tiap pesan: HISTORY|sender|receiver|encMsg|time
            foreach (var (s, r, plain, t) in DatabaseHelper.GetAllMessages(username))
            {
                string reEnc = CryptoHelper.AesEncryptMsg(plain, aesKey); // IV baru random tiap pesan
                writer.WriteLine($"HISTORY|{s}|{r}|{reEnc}|{t}"); // kirim ke client
            }

            // Kirim pesan yang masuk saat user ini sedang offline (delivered=0)
            // Format tiap pesan: MSG|sender|encMsg|sentAt
            foreach (var (sender, plain, sentAt) in DatabaseHelper.GetPendingMessages(username))
            {
                string reEnc = CryptoHelper.AesEncryptMsg(plain, aesKey); // IV baru random tiap pesan
                writer.WriteLine($"MSG|{sender}|{reEnc}|{sentAt}"); // kirim sebagai pesan biasa
            }
            // GetPendingMessages otomatis update delivered=1 setelah semua pesan diambil

            Console.WriteLine($"{username} ({role}) connected"); // log di console server
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE CHAT
        //  Memproses pesan yang dikirim client ke user lain.
        //  Format command dari client: CHAT|from|to|aes_encrypted_msg|session_token
        //
        //  ALUR:
        //    1. Validasi session token (keamanan: cegah spoofing)
        //    2. Dekripsi pesan dari sender (pakai AES key sender)
        //    3. Enkripsi & simpan ke DB (pakai DB key)
        //    4. Jika recipient online → re-enkripsi & forward (pakai AES key recipient)
        //    5. Jika recipient offline → delivered=0, kirim saat recipient login
        // ════════════════════════════════════════════════════════════════════════
        private void HandleChat(string[] parts)
        {
            // Validasi jumlah argumen
            if (parts.Length < 5) return;

            string from   = parts[1]; // username pengirim
            string to     = parts[2]; // username penerima
            string encMsg = parts[3]; // pesan terenkripsi AES (dengan key milik 'from')
            string token  = parts[4]; // session token dari client (harus cocok dengan yang di server)

            // ── Validasi session token ────────────────────────────────────────
            // Ambil info client berdasarkan username pengirim
            ClientInfo senderInfo = ClientManager.GetClientInfo(from);

            // Cek: apakah token yang dikirim cocok dengan token yang ada di server?
            if (senderInfo == null || senderInfo.SessionToken != token)
            {
                // Token tidak cocok → mungkin session sudah tidak valid
                writer.WriteLine("ERROR|Session tidak valid, silakan login ulang.");
                return;
            }

            // Cek: apakah token sudah expired (lebih dari 1 jam)?
            if (DateTime.Now > senderInfo.TokenExpiry)
            {
                writer.WriteLine("ERROR|Session expired, silakan login ulang.");
                ClientManager.RemoveClient(from); // hapus dari daftar online
                return;
            }

            // ── Dekripsi pesan dari sender ────────────────────────────────────
            // aesKey/aesIv di sini adalah key milik pengirim (dari key exchange saat login)
            string plainText = CryptoHelper.AesDecryptMsg(encMsg, aesKey);
            // plainText = isi pesan yang bisa dibaca (contoh: "halo, apa kabar?")

            // ── Simpan ke database ────────────────────────────────────────────
            // encMsg      → disimpan di kolom message_encrypted (audit trail, key tidak bisa diakses lagi)
            // plainText   → dienkripsi ulang dengan DB key → disimpan di kolom message_plain
            // delivered=0 → belum terkirim ke penerima
            // SaveMessage mengembalikan ID row yang baru dibuat
            long msgId = DatabaseHelper.SaveMessage(from, to, encMsg, plainText);

            // ── Forward ke recipient jika online ──────────────────────────────
            ClientInfo recipientInfo = ClientManager.GetClientInfo(to); // null jika offline

            if (recipientInfo != null) // recipient sedang online
            {
                // Re-enkripsi plaintext dengan AES key milik recipient
                // (karena setiap client punya AES key yang berbeda)
                string reEncrypted = CryptoHelper.AesEncryptMsg(plainText, recipientInfo.AesKey);
                try
                {
                    // Kirim pesan ke recipient
                    // Format: MSG|from|encMsg
                    recipientInfo.Writer.WriteLine($"MSG|{from}|{reEncrypted}");

                    // Forward berhasil → update delivered=1 di DB
                    // Analogi: centang dua (✓✓) di WhatsApp
                    DatabaseHelper.MarkDelivered(msgId);
                }
                catch { }
                // Jika WriteLine gagal (koneksi recipient putus mendadak):
                // MarkDelivered tidak dipanggil → delivered tetap 0
                // → pesan akan dikirim ulang saat recipient login kembali
            }
            // Jika recipient offline: delivered=0 di DB
            // → akan dikirim saat recipient login (via GetPendingMessages di HandleLogin)
        }

        // ════════════════════════════════════════════════════════════════════════
        //  HANDLE TOKEN REFRESH
        //  Client meminta token baru sebelum token lama expired.
        //  Format command dari client: TOKEN_REFRESH|username|old_token
        // ════════════════════════════════════════════════════════════════════════
        private void HandleTokenRefresh(string[] parts)
        {
            if (parts.Length < 3) return; // validasi argumen

            string user     = parts[1]; // username yang minta refresh
            string oldToken = parts[2]; // token lama yang sedang dipakai

            // Ambil info client dan validasi token lama
            ClientInfo info = ClientManager.GetClientInfo(user);
            if (info == null || info.SessionToken != oldToken)
            {
                // Token tidak cocok → session tidak valid
                writer.WriteLine("TOKEN_REFRESH_FAIL|Session tidak valid.");
                return;
            }

            // Generate token baru + perpanjang expiry 1 jam lagi
            string   newToken  = Guid.NewGuid().ToString("N"); // GUID baru
            DateTime newExpiry = DateTime.Now.AddHours(1);     // 1 jam dari sekarang

            // Update data session di ClientManager (in-memory)
            info.SessionToken = newToken;
            info.TokenExpiry  = newExpiry;

            // Kirim token baru ke client
            // Format: TOKEN_REFRESHED|newToken|newExpiry_time
            writer.WriteLine($"TOKEN_REFRESHED|{newToken}|{newExpiry:HH:mm:ss}");
        }

        // ════════════════════════════════════════════════════════════════════════
        //  DISCONNECT
        //  Dipanggil saat koneksi client terputus (normal atau mendadak).
        //  Hapus client dari daftar online → trigger broadcast ALL_USERS baru.
        // ════════════════════════════════════════════════════════════════════════
        private void Disconnect()
        {
            Console.WriteLine($"{username} disconnected"); // log di console server

            // Hapus client dari daftar online (jika sudah login)
            if (!string.IsNullOrEmpty(username))
                ClientManager.RemoveClient(username); // otomatis trigger Broadcast ALL_USERS

            try { client.Close(); } catch { } // tutup koneksi TCP (abaikan error jika sudah tutup)
        }
    }
}
