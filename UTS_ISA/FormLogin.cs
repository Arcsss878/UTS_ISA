using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace UTS_ISA
{
    public partial class FormLogin : Form
    {
        public FormLogin()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Username dan password tidak boleh kosong.");
                return;
            }

            try
            {
                var (client, reader, writer, aesKey, aesIv) = ConnectAndExchangeKeys();

                string pwHash = CryptoHelper.Sha256Hash(txtPassword.Text);
                writer.WriteLine($"LOGIN|{txtUsername.Text}|{pwHash}");

                string response = reader.ReadLine();
                if (response == null) { MessageBox.Show("Server tidak merespons."); client.Close(); return; }

                string[] parts = response.Split('|');
                if (parts[0] == "LOGIN_SUCCESS")
                {
                    string role = parts[1];
                    var chat = new FormChat(client, reader, writer, txtUsername.Text, aesKey, aesIv, role);
                    chat.Show();
                    this.Hide();
                }
                else
                {
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

        private void btnRegister_Click(object sender, EventArgs e)
        {
            var form = new FormRegister();
            form.ShowDialog(this);
        }

        // Hubungkan ke server dan lakukan RSA key exchange untuk mendapatkan AES key
        public static (TcpClient, StreamReader, StreamWriter, byte[], byte[]) ConnectAndExchangeKeys()
        {
            var client = new TcpClient("127.0.0.1", 6000);
            var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

            // Terima RSA public key dari server
            string pubKeyMsg = reader.ReadLine();
            string[] pubParts = pubKeyMsg.Split('|');
            string publicKeyXml = Encoding.UTF8.GetString(Convert.FromBase64String(pubParts[1]));

            // Generate AES-256 key + IV
            byte[] aesKey = CryptoHelper.GenerateAesKey();
            byte[] aesIv  = CryptoHelper.GenerateAesIv();

            // RSA-enkripsi AES key dan IV, kirim ke server
            byte[] encKey = CryptoHelper.RsaEncrypt(aesKey, publicKeyXml);
            byte[] encIv  = CryptoHelper.RsaEncrypt(aesIv, publicKeyXml);
            writer.WriteLine($"KEY_EXCHANGE|{Convert.ToBase64String(encKey)}|{Convert.ToBase64String(encIv)}");

            return (client, reader, writer, aesKey, aesIv);
        }
    }
}
