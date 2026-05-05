using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UTS_ISA
{
    /// <summary>
    /// Form utama aplikasi chat. Ditampilkan setelah login berhasil.
    ///
    /// FITUR UTAMA:
    ///   1. Kirim/terima pesan terenkripsi AES-256
    ///   2. Daftar user (online/offline) dari DB, di-update real-time
    ///   3. History chat per user (disimpan di memory + di-load dari DB saat login)
    ///   4. Session token countdown + auto-refresh (bonus keamanan)
    ///   5. Notif pesan belum dibaca "(!) " di user list
    ///   6. Kirim pesan ke user offline (tersimpan di DB, terkirim saat login)
    ///   7. Export riwayat chat ke file .txt
    /// </summary>
    public partial class FormChat : Form
    {
        // ── Koneksi TCP ke server ─────────────────────────────────────────────────
        private TcpClient    client;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread       receiveThread; // background thread untuk terima pesan

        // ── Identitas user yang sedang login ──────────────────────────────────────
        private string username;
        private string role;

        // ── Session Token (Bonus Poin 6: Secure Session) ──────────────────────────
        // Token adalah "kartu akses" sesi. Setiap kirim pesan, token ikut dikirim
        // ke server sebagai bukti bahwa pengirim memang sedang login sah.
        // Token berlaku 1 jam, auto-refresh 2 menit sebelum habis.
        private string   sessionToken;  // GUID 32 karakter (contoh: b541001d3a2f...)
        private DateTime tokenExpiry;   // waktu token kedaluwarsa
        private System.Windows.Forms.Timer tokenTimer; // timer tick per 1 detik untuk countdown

        // ── Enkripsi AES (session key unik per koneksi) ───────────────────────────
        // Key dan IV ini di-generate random saat key exchange di FormLogin,
        // dikirim ke server lewat RSA, dan dipakai untuk enkripsi semua pesan.
        private byte[] aesKey;
        private byte[] aesIv;

        // ── History chat per user ─────────────────────────────────────────────────
        // key   = username lawan chat (contoh: "alice")
        // value = list pesan dalam format "[HH:mm] sender: pesan"
        // Di-load dari DB saat login (via HISTORY), ditambah real-time saat terima MSG
        private Dictionary<string, List<string>> chatHistories =
            new Dictionary<string, List<string>>();

        // ── Daftar user yang punya pesan BARU belum dibaca ───────────────────────
        // Ditambah saat MSG masuk dan bukan dari currentChatTarget
        // Dihapus saat user membuka chat dengan orang tersebut
        private HashSet<string> unreadUsers = new HashSet<string>();

        // ── Status online/offline per user ────────────────────────────────────────
        // Di-update setiap kali server broadcast ALL_USERS
        private Dictionary<string, bool> userStatus = new Dictionary<string, bool>();

        // ── User yang sedang dibuka chatnya ───────────────────────────────────────
        private string currentChatTarget = null;

        // ── Flag anti-StackOverflow di RefreshUserList ────────────────────────────
        // lstUser.SelectedIndex = i (di dalam RefreshUserList) bisa trigger
        // SelectedIndexChanged event → panggil RefreshUserList lagi → infinite loop!
        // Flag ini mencegah hal tersebut.
        private bool isRefreshing = false;

        // ── Constructor default (diperlukan WinForms designer) ────────────────────
        public FormChat() { InitializeComponent(); }

        /// <summary>
        /// Constructor utama — dipanggil dari FormLogin setelah login berhasil.
        /// </summary>
        /// <param name="c">Koneksi TCP yang sudah terhubung ke server</param>
        /// <param name="r">StreamReader untuk baca data dari server</param>
        /// <param name="w">StreamWriter untuk kirim data ke server</param>
        /// <param name="user">Username yang sedang login</param>
        /// <param name="key">AES key 32-byte hasil key exchange</param>
        /// <param name="iv">AES IV 16-byte hasil key exchange</param>
        /// <param name="userRole">Role user: "user" atau "admin"</param>
        /// <param name="token">Session token dari server</param>
        /// <param name="expiryTimeStr">Waktu token habis format "HH:mm:ss"</param>
        public FormChat(TcpClient c, StreamReader r, StreamWriter w,
                        string user, byte[] key, byte[] iv,
                        string userRole, string token, string expiryTimeStr)
        {
            InitializeComponent();

            // Simpan semua data sesi
            client       = c;
            reader       = r;
            writer       = w;
            username     = user;
            aesKey       = key;
            aesIv        = iv;
            role         = userRole;
            sessionToken = token;

            // Parse waktu expiry yang dikirim server (format "HH:mm:ss")
            // Contoh: "14:30:00" → tokenExpiry = jam 14:30:00 hari ini
            if (!string.IsNullOrEmpty(expiryTimeStr) &&
                TimeSpan.TryParse(expiryTimeStr, out TimeSpan ts))
            {
                tokenExpiry = DateTime.Today.Add(ts);
                // Kalau sudah lewat tengah malam, tambah 1 hari
                if (tokenExpiry < DateTime.Now) tokenExpiry = tokenExpiry.AddDays(1);
            }
            else
            {
                // Fallback: 1 jam dari sekarang
                tokenExpiry = DateTime.Now.AddHours(1);
            }

            // Set judul window dan label role
            this.Text    = "Secure Chat \u2014 " + username;
            lblRole.Text = "Login sebagai: " + username + "  [" + role + "]";

            // Setup countdown timer — tick setiap 1 detik
            tokenTimer          = new System.Windows.Forms.Timer();
            tokenTimer.Interval = 1000; // 1000ms = 1 detik
            tokenTimer.Tick    += TokenTimer_Tick;

            // Buat thread penerima pesan (background, otomatis stop saat app tutup)
            receiveThread = new Thread(ReceiveMessage) { IsBackground = true };

            // Start receive thread dan timer SETELAH form Load
            // (penting: Invoke() hanya bisa dipanggil setelah window handle dibuat)
            this.Load += (s, ev) =>
            {
                receiveThread.Start();
                tokenTimer.Start();
                UpdateTokenLabel();
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SESSION TOKEN COUNTDOWN
        //
        //  Setiap detik timer tick, UpdateTokenLabel() dipanggil untuk:
        //    - Hitung sisa waktu token
        //    - Update tampilan label (Token: xxx | Expires: HH:mm | Sisa: mm:ss)
        //    - Warna merah jika sisa < 5 menit (peringatan)
        //    - Auto-refresh jika sisa < 2 menit (kirim TOKEN_REFRESH ke server)
        //    - Stop timer + disable tombol jika token expired
        // ════════════════════════════════════════════════════════════════════════

        private void TokenTimer_Tick(object sender, EventArgs e)
        {
            UpdateTokenLabel();
        }

        private void UpdateTokenLabel()
        {
            TimeSpan remaining = tokenExpiry - DateTime.Now;

            if (remaining.TotalSeconds <= 0)
            {
                // Token sudah habis — user harus login ulang
                lblTokenInfo.Text       = "Session: EXPIRED \u2014 silakan login ulang";
                lblTokenInfo.ForeColor  = System.Drawing.Color.Red;
                btnRefreshToken.Enabled = false;
                tokenTimer.Stop();
            }
            else
            {
                // Tampilkan token (8 karakter pertama saja agar tidak terlalu panjang)
                string shortToken = sessionToken.Length >= 8
                    ? sessionToken.Substring(0, 8) + "..."
                    : sessionToken;

                // Format sisa waktu: MM:SS
                string mins = ((int)remaining.TotalMinutes).ToString("D2");
                string secs = remaining.Seconds.ToString("D2");

                lblTokenInfo.Text = string.Format(
                    "Token: {0}  |  Expires: {1}  |  Sisa: {2}:{3}",
                    shortToken, tokenExpiry.ToString("HH:mm:ss"), mins, secs);

                // Warna peringatan jika sisa < 5 menit
                lblTokenInfo.ForeColor = remaining.TotalMinutes < 5
                    ? System.Drawing.Color.OrangeRed
                    : System.Drawing.Color.DarkGreen;

                // Auto-refresh token 2 menit sebelum expired
                // (agar user tidak perlu klik manual)
                if (remaining.TotalMinutes < 2 && btnRefreshToken.Enabled)
                    RequestTokenRefresh();
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  REFRESH TOKEN
        //  Minta token baru ke server tanpa harus login ulang.
        //  Server generate GUID baru + perpanjang expiry 1 jam lagi.
        // ════════════════════════════════════════════════════════════════════════

        private void btnRefreshToken_Click(object sender, EventArgs e)
        {
            RequestTokenRefresh();
        }

        private void RequestTokenRefresh()
        {
            btnRefreshToken.Enabled = false; // disable sementara agar tidak klik berkali-kali
            try
            {
                // Kirim command TOKEN_REFRESH ke server
                // Format: TOKEN_REFRESH|username|old_token
                writer.WriteLine(string.Format("TOKEN_REFRESH|{0}|{1}", username, sessionToken));
            }
            catch
            {
                btnRefreshToken.Enabled = true; // re-enable jika gagal kirim
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  RECEIVE LOOP (background thread)
        //
        //  Thread ini terus berjalan selama koneksi aktif, membaca setiap baris
        //  data dari server dan mendistribusikannya ke handler yang sesuai.
        //
        //  Semua update UI dilakukan lewat Invoke() karena UI hanya boleh
        //  diakses dari UI thread (main thread), bukan dari background thread.
        //
        //  Format pesan dari server:
        //    MSG|sender|encMsg|time          → pesan masuk
        //    HISTORY|sender|receiver|enc|time → riwayat chat saat login
        //    ALL_USERS|user:status,...        → update daftar user
        //    TOKEN_REFRESHED|newToken|expiry  → konfirmasi token baru
        //    TOKEN_REFRESH_FAIL|alasan        → gagal refresh token
        //    ERROR|alasan                     → error session, paksa logout
        // ════════════════════════════════════════════════════════════════════════

        private void ReceiveMessage()
        {
            while (true)
            {
                try
                {
                    string data = reader.ReadLine();
                    if (data == null) break; // koneksi terputus

                    string[] parts = data.Split('|');

                    // Semua update UI harus lewat Invoke() agar thread-safe
                    Invoke(new Action(() =>
                    {
                        switch (parts[0])
                        {
                            case "MSG":
                                // Pesan masuk dari user lain (real-time atau pending offline)
                                HandleIncomingMsg(parts);
                                break;
                            case "HISTORY":
                                // Riwayat chat dari DB dikirim server saat login
                                HandleHistory(parts);
                                break;
                            case "ALL_USERS":
                                // Broadcast daftar semua user + status online/offline
                                HandleAllUsers(parts);
                                break;
                            case "TOKEN_REFRESHED":
                                // Server konfirmasi token baru berhasil di-generate
                                HandleTokenRefreshed(parts);
                                break;
                            case "TOKEN_REFRESH_FAIL":
                                MessageBox.Show(
                                    parts.Length > 1 ? parts[1] : "Gagal refresh token.",
                                    "Token Refresh");
                                btnRefreshToken.Enabled = true;
                                break;
                            case "ERROR":
                                // Error session (token invalid/expired) → paksa keluar
                                MessageBox.Show(
                                    parts.Length > 1 ? parts[1] : "Server error.",
                                    "Session Error");
                                Application.Exit();
                                break;
                        }
                    }));
                }
                catch { break; } // koneksi putus / app ditutup
            }
        }

        // ── Handler: riwayat chat dari DB (dikirim server saat login) ─────────────
        private void HandleHistory(string[] parts)
        {
            // Format: HISTORY|sender|receiver|encMsg|time
            if (parts.Length < 5) return;
            string sender   = parts[1];
            string receiver = parts[2];
            string encMsg   = parts[3];
            string time     = parts[4];

            // Dekripsi pesan dengan AES key milik user ini
            string plain     = CryptoHelper.AesDecryptMsg(encMsg, aesKey);

            // Tentukan "user lawan" sebagai key di chatHistories
            string otherUser = sender == username ? receiver : sender;

            // Format entri sesuai arah pesan
            string entry = sender == username
                ? "[" + time + "] Me -> " + receiver + ": " + plain  // pesan yang kita kirim
                : "[" + time + "] " + sender + ": " + plain;          // pesan yang kita terima

            if (!chatHistories.ContainsKey(otherUser))
                chatHistories[otherUser] = new List<string>();

            // Hindari duplikat (safety check)
            if (!chatHistories[otherUser].Contains(entry))
                chatHistories[otherUser].Add(entry);

            // Kalau sedang membuka chat dengan user ini, langsung tampil di lstRiwayat
            if (currentChatTarget == otherUser &&
                !lstRiwayat.Items.Contains(entry))
            {
                lstRiwayat.Items.Add(entry);
                lstRiwayat.TopIndex = lstRiwayat.Items.Count - 1;
            }
        }

        // ── Handler: pesan masuk real-time ────────────────────────────────────────
        private void HandleIncomingMsg(string[] parts)
        {
            // Format: MSG|sender|encMsg|time
            if (parts.Length < 3) return;
            string from         = parts[1];
            string encryptedMsg = parts[2];
            string time         = parts.Length >= 4 ? parts[3] : DateTime.Now.ToString("HH:mm");

            // Dekripsi pesan dengan AES key milik user ini
            string plainMsg = CryptoHelper.AesDecryptMsg(encryptedMsg, aesKey);
            string entry    = "[" + time + "] " + from + ": " + plainMsg;

            // Simpan ke history
            if (!chatHistories.ContainsKey(from))
                chatHistories[from] = new List<string>();
            chatHistories[from].Add(entry);

            if (currentChatTarget == from)
            {
                // Sedang membuka chat dengan pengirim → langsung tampilkan
                lstRiwayat.Items.Add(entry);
                lstRiwayat.TopIndex = lstRiwayat.Items.Count - 1;
            }
            else
            {
                // Tidak sedang membuka chat dengan pengirim → tandai sebagai unread
                unreadUsers.Add(from); // tambah ke daftar belum dibaca
                RefreshUserList();     // refresh agar (!) muncul di user list
            }
        }

        // ── Handler: broadcast daftar semua user + status ─────────────────────────
        private void HandleAllUsers(string[] parts)
        {
            // Format: ALL_USERS|alice:online,bob:offline,charlie:online
            if (parts.Length < 2) return;
            userStatus.Clear();

            string[] entries = parts[1].Split(',');
            foreach (var entry in entries)
            {
                var kv = entry.Split(':');
                // Masukkan semua user KECUALI diri sendiri
                if (kv.Length == 2 && kv[0] != username)
                    userStatus[kv[0]] = kv[1] == "online";
            }

            RefreshUserList();
            if (currentChatTarget != null) UpdateChatLabel();
        }

        // ── Handler: konfirmasi token baru dari server ────────────────────────────
        private void HandleTokenRefreshed(string[] parts)
        {
            // Format: TOKEN_REFRESHED|newToken|newExpiryTime
            if (parts.Length < 3) return;
            sessionToken = parts[1]; // ganti token lama dengan yang baru

            // Update waktu expiry
            if (TimeSpan.TryParse(parts[2], out TimeSpan ts))
            {
                tokenExpiry = DateTime.Today.Add(ts);
                if (tokenExpiry < DateTime.Now) tokenExpiry = tokenExpiry.AddDays(1);
            }
            else
            {
                tokenExpiry = DateTime.Now.AddHours(1);
            }

            btnRefreshToken.Enabled = true;
            UpdateTokenLabel(); // perbarui tampilan countdown
        }

        // ════════════════════════════════════════════════════════════════════════
        //  USER LIST
        //
        //  Menampilkan semua user yang terdaftar di DB beserta status online/offline.
        //  Jika ada pesan belum dibaca dari user tertentu, ditampilkan "(!)".
        //  Format item: "[Online] alice" atau "[Offline] bob (!)"
        // ════════════════════════════════════════════════════════════════════════

        private void RefreshUserList()
        {
            // Guard: cegah StackOverflow akibat lstUser.SelectedIndex = i
            // yang memicu SelectedIndexChanged → RefreshUserList → loop tak terbatas
            if (isRefreshing) return;
            isRefreshing = true;
            try
            {
                lstUser.Items.Clear();
                foreach (var kv in userStatus)
                {
                    string status  = kv.Value ? "[Online]" : "[Offline]";

                    // Cek apakah ada pesan BARU belum dibaca dari user ini
                    // Hanya tampil (!) jika user ada di unreadUsers (pesan baru masuk)
                    // Bukan semua user yang punya history
                    bool hasUnread = unreadUsers.Contains(kv.Key);

                    lstUser.Items.Add(status + " " + kv.Key + (hasUnread ? " (!)" : ""));
                }

                // Pertahankan seleksi user yang sedang dibuka chatnya
                if (currentChatTarget != null)
                {
                    for (int i = 0; i < lstUser.Items.Count; i++)
                    {
                        if (ExtractUsername(lstUser.Items[i].ToString()) == currentChatTarget)
                        {
                            lstUser.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            finally { isRefreshing = false; }
        }

        private void UpdateChatLabel()
        {
            if (currentChatTarget == null) return;
            bool online   = userStatus.ContainsKey(currentChatTarget) && userStatus[currentChatTarget];
            string status = online ? "Online" : "Offline";
            lblChatWith.Text = "Chat dengan: " + currentChatTarget + "  |  Status: " + status;
        }

        /// <summary>
        /// Ekstrak username bersih dari item lstUser.
        /// Contoh: "[Online] alice (!)" → "alice"
        /// </summary>
        private string ExtractUsername(string item)
        {
            return item.Replace("[Online] ", "")
                       .Replace("[Offline] ", "")
                       .Replace(" (!)", "")
                       .Trim();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS — interaksi user
        // ════════════════════════════════════════════════════════════════════════

        // ── Klik user di daftar → buka chat dengan user itu ──────────────────────
        private void lstUser_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstUser.SelectedItem == null || isRefreshing) return;

            string target = ExtractUsername(lstUser.SelectedItem.ToString());
            currentChatTarget = target;

            // Load history chat dengan user yang dipilih
            lstRiwayat.Items.Clear();
            if (chatHistories.ContainsKey(target))
            {
                foreach (var msg in chatHistories[target])
                    lstRiwayat.Items.Add(msg);
                lstRiwayat.TopIndex = lstRiwayat.Items.Count - 1;
            }

            unreadUsers.Remove(currentChatTarget); // hapus tanda (!) setelah dibuka
            UpdateChatLabel();
            RefreshUserList();
            txtChat.Focus();
        }

        // ── Klik tombol Kirim ─────────────────────────────────────────────────────
        private void btnKirim_Click(object sender, EventArgs e)
        {
            if (currentChatTarget == null)
            {
                MessageBox.Show("Pilih user tujuan dulu!");
                return;
            }

            string msg = txtChat.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            // Cek apakah session token masih valid sebelum kirim pesan
            if (DateTime.Now > tokenExpiry)
            {
                MessageBox.Show("Session token sudah expired. Silakan login ulang.", "Session Expired");
                return;
            }

            // Enkripsi pesan dengan AES key milik user ini
            // Server nanti akan dekripsi, lalu re-enkripsi pakai key milik penerima
            string encryptedMsg = CryptoHelper.AesEncryptMsg(msg, aesKey);
            // ↑ AesEncryptMsg: IV baru random tiap pesan → ciphertext selalu berbeda

            // Kirim ke server dengan format: CHAT|from|to|encMsg|sessionToken
            // sessionToken wajib disertakan sebagai validasi sesi
            writer.WriteLine(string.Format("CHAT|{0}|{1}|{2}|{3}",
                username, currentChatTarget, encryptedMsg, sessionToken));

            // Tampilkan pesan yang dikirim di history lokal (tanpa menunggu echo dari server)
            string entry = "[" + DateTime.Now.ToString("HH:mm") + "] Me -> " +
                           currentChatTarget + ": " + msg;
            if (!chatHistories.ContainsKey(currentChatTarget))
                chatHistories[currentChatTarget] = new List<string>();
            chatHistories[currentChatTarget].Add(entry);

            lstRiwayat.Items.Add(entry);
            lstRiwayat.TopIndex = lstRiwayat.Items.Count - 1;

            txtChat.Clear();
            txtChat.Focus();
        }

        // ── Klik tombol Export Riwayat ────────────────────────────────────────────
        private void btnExport_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter   = "Text File|*.txt";
                sfd.FileName = "chat_history_" + username + "_" +
                               DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt";
                if (sfd.ShowDialog() != DialogResult.OK) return;

                // Tulis semua history ke file .txt
                var sb = new StringBuilder();
                sb.AppendLine("=== Riwayat Chat ===");
                sb.AppendLine("User    : " + username);
                sb.AppendLine("Role    : " + role);
                sb.AppendLine("Diekspor: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                sb.AppendLine(new string('=', 40));

                foreach (var kv in chatHistories)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- Chat dengan " + kv.Key + " ---");
                    foreach (var m in kv.Value)
                        sb.AppendLine(m);
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Riwayat chat berhasil diekspor!");
            }
        }

        // ── Form ditutup → bersihkan koneksi dan keluar ───────────────────────────
        private void FormChat_FormClosing(object sender, FormClosingEventArgs e)
        {
            tokenTimer?.Stop();
            try { client?.Close(); } catch { }
            Application.Exit(); // pastikan semua thread ikut berhenti
        }
    }
}
