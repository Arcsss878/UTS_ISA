using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace UTS_ISA
{
    /// <summary>
    /// Form pertama yang muncul saat aplikasi dibuka.
    /// Menangani proses login dan membuka FormRegister untuk registrasi.
    ///
    /// ALUR LOGIN:
    ///   1. User isi username + password lalu klik Login
    ///   2. ConnectAndExchangeKeys() — buka koneksi TCP ke server port 6000
    ///      dan lakukan RSA key exchange untuk dapat AES session key
    ///   3. Hash password SHA-256, kirim LOGIN|username|pwHash ke server
    ///   4. Terima LOGIN_SUCCESS|role|token|expiryTime dari server
    ///   5. Buka FormChat dengan semua data sesi
    /// </summary>
    public partial class FormLogin : Form
    {
        public FormLogin()
        {
            InitializeComponent();
        }

        // ── Event: tombol Login diklik ────────────────────────────────────────────
        private void btnLogin_Click(object sender, EventArgs e)
        {
            // Validasi: username dan password tidak boleh kosong
            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Username dan password tidak boleh kosong.");
                return;
            }

            try
            {
                // Langkah 1: Hubungkan ke server dan lakukan RSA key exchange.
                // Hasil: koneksi TCP aktif + AES key + AES IV yang sudah
                // disepakati secara aman dengan server.
                var (client, reader, writer, aesKey, aesIv) = ConnectAndExchangeKeys();

                // Langkah 2: Hash password dengan SHA-256 sebelum dikirim.
                // Password TIDAK PERNAH dikirim as plaintext ke jaringan.
                string pwHash = CryptoHelper.Sha256Hash(txtPassword.Text);

                // Langkah 3: Kirim command LOGIN ke server.
                // Format: LOGIN|username|sha256_password_hash
                writer.WriteLine(string.Format("LOGIN|{0}|{1}", txtUsername.Text, pwHash));

                // Langkah 4: Tunggu respons dari server
                string response = reader.ReadLine();
                if (response == null)
                {
                    MessageBox.Show("Server tidak merespons.");
                    client.Close();
                    return;
                }

                // Langkah 5: Parse respons server
                string[] parts = response.Split('|');
                if (parts[0] == "LOGIN_SUCCESS")
                {
                    // Format: LOGIN_SUCCESS|role|token|expiryTime
                    // Contoh: LOGIN_SUCCESS|user|b541001d3a2f...|14:30:00
                    string role       = parts[1];                           // role: "user" / "admin"
                    string token      = parts[2];                           // session token (GUID 32 char)
                    string expiryTime = parts.Length >= 4 ? parts[3] : ""; // jam token kedaluwarsa

                    // Langkah 6: Buka FormChat dengan semua data sesi
                    var chat = new FormChat(client, reader, writer,
                                           txtUsername.Text, aesKey, aesIv,
                                           role, token, expiryTime);
                    chat.Show();
                    this.Hide(); // sembunyikan FormLogin agar app tidak langsung exit
                }
                else
                {
                    // Respons: LOGIN_FAIL|alasan
                    string reason = parts.Length > 1 ? parts[1] : "Login gagal.";
                    MessageBox.Show(reason);
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal terhubung ke server: " + ex.Message);
            }
        }

        // ── Event: tombol Register diklik ─────────────────────────────────────────
        private void btnRegister_Click(object sender, EventArgs e)
        {
            // Buka FormRegister sebagai dialog modal
            var form = new FormRegister();
            form.ShowDialog(this);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  RSA KEY EXCHANGE — inti keamanan koneksi
        //
        //  Masalah:
        //    Semua pesan dienkripsi AES. Client & server harus pakai AES key
        //    yang SAMA. Tapi bagaimana mengirim AES key ke server tanpa
        //    disadap di tengah jalan?
        //
        //  Solusi — RSA Asymmetric Encryption:
        //    RSA punya dua kunci yang berpasangan:
        //      - Public key  : bebas dibagikan ke siapa saja
        //      - Private key : HANYA server yang punya, tidak pernah dikirim
        //    Apapun yang dienkripsi pakai public key, hanya bisa dibuka
        //    dengan private key pasangannya.
        //
        //  Alur key exchange:
        //    Server ──► PUBKEY|<base64 RSA public key> ──► Client
        //    Client:
        //      • Generate AES key (32 byte random) + IV (16 byte random)
        //      • Enkripsi AES key pakai RSA public key → encKey
        //      • Enkripsi AES IV  pakai RSA public key → encIV
        //    Client ──► KEY_EXCHANGE|<base64 encKey>|<base64 encIV> ──► Server
        //    Server:
        //      • Dekripsi encKey + encIV pakai RSA private key
        //      • Kini server tahu AES key + IV yang sama dengan client
        //    ✓ Mulai sekarang semua komunikasi pakai AES (lebih cepat dari RSA)
        //
        //  Method ini static agar bisa dipanggil dari FormRegister juga
        //  (registrasi pun butuh koneksi aman ke server).
        // ════════════════════════════════════════════════════════════════════════
        public static (TcpClient, StreamReader, StreamWriter, byte[], byte[])
            ConnectAndExchangeKeys()
        {
            // Buka koneksi TCP ke server di localhost port 6000
            var client = new TcpClient("127.0.0.1", 6000);
            var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            var writer = new StreamWriter(client.GetStream(), Encoding.UTF8)
                             { AutoFlush = true }; // langsung kirim tanpa buffering

            // Terima RSA public key dari server
            // Format: PUBKEY|<base64 encoded XML public key>
            string pubKeyMsg    = reader.ReadLine();
            string[] pubParts   = pubKeyMsg.Split('|');
            string publicKeyXml = Encoding.UTF8.GetString(Convert.FromBase64String(pubParts[1]));

            // Generate AES session key + IV secara acak (unik per koneksi)
            byte[] aesKey = CryptoHelper.GenerateAesKey(); // 32 byte = 256-bit
            byte[] aesIv  = CryptoHelper.GenerateAesIv();  // 16 byte = 128-bit

            // Enkripsi AES key & IV dengan RSA public key server
            // Hanya server (pemilik private key) yang bisa membukanya
            byte[] encKey = CryptoHelper.RsaEncrypt(aesKey, publicKeyXml);
            byte[] encIv  = CryptoHelper.RsaEncrypt(aesIv,  publicKeyXml);

            // Kirim AES key+IV terenkripsi ke server
            // Format: KEY_EXCHANGE|<base64 encKey>|<base64 encIV>
            writer.WriteLine(string.Format("KEY_EXCHANGE|{0}|{1}",
                Convert.ToBase64String(encKey),
                Convert.ToBase64String(encIv)));

            // Kembalikan koneksi + AES key untuk dipakai enkripsi pesan selanjutnya
            return (client, reader, writer, aesKey, aesIv);
        }
    }
}
