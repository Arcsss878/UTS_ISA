using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace UTS_ISA
{
    /// <summary>
    /// Form registrasi akun baru.
    /// Dibuka dari FormLogin saat tombol "Register" diklik.
    ///
    /// ALUR REGISTRASI:
    ///   1. User isi username, password, konfirmasi password
    ///   2. Validasi lokal (tidak kosong, minimal 6 karakter, password cocok)
    ///   3. Buka koneksi baru ke server + RSA key exchange
    ///   4. Hash password SHA-256, kirim REGISTER|username|pwHash|user
    ///   5. Terima REGISTER_SUCCESS atau REGISTER_FAIL|alasan
    ///   6. Tutup form jika berhasil → user langsung bisa login
    ///
    /// Catatan: Role selalu "user" (tidak ada pilihan admin dari form ini)
    /// </summary>
    public partial class FormRegister : Form
    {
        public FormRegister()
        {
            InitializeComponent();
        }

        // ── Event: tombol Register diklik ─────────────────────────────────────────
        private void btnRegister_Click(object sender, EventArgs e)
        {
            // Validasi 1: field tidak boleh kosong
            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Username dan password tidak boleh kosong.");
                return;
            }

            // Validasi 2: password dan konfirmasi harus sama
            if (txtPassword.Text != txtConfirm.Text)
            {
                MessageBox.Show("Password dan konfirmasi tidak cocok.");
                return;
            }

            // Validasi 3: password minimal 6 karakter
            if (txtPassword.Text.Length < 6)
            {
                MessageBox.Show("Password minimal 6 karakter.");
                return;
            }

            try
            {
                // Buka koneksi ke server + RSA key exchange
                // (gunakan method static dari FormLogin agar tidak duplikasi kode)
                var (client, reader, writer, _, _) = FormLogin.ConnectAndExchangeKeys();

                // Hash password SHA-256 — tidak pernah kirim password plaintext
                string pwHash = CryptoHelper.Sha256Hash(txtPassword.Text);

                // Kirim command REGISTER ke server
                // Format: REGISTER|username|sha256_hash|role
                // Role selalu "user" — tidak ada registrasi admin dari sini
                writer.WriteLine($"REGISTER|{txtUsername.Text}|{pwHash}|user");

                // Tunggu respons server
                string response = reader.ReadLine();
                client.Close(); // tutup koneksi setelah registrasi selesai

                if (response == "REGISTER_SUCCESS")
                {
                    MessageBox.Show("Registrasi berhasil! Silakan login.");
                    this.Close(); // kembali ke FormLogin
                }
                else
                {
                    // REGISTER_FAIL|alasan (contoh: "Username sudah dipakai")
                    string[] parts = response?.Split('|');
                    string reason  = parts != null && parts.Length > 1
                                     ? parts[1] : "Registrasi gagal.";
                    MessageBox.Show(reason);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal terhubung ke server: " + ex.Message);
            }
        }

        // ── Event: tombol Kembali diklik ──────────────────────────────────────────
        private void btnBack_Click(object sender, EventArgs e)
        {
            this.Close(); // kembali ke FormLogin
        }
    }
}
