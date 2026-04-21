using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace UTS_ISA
{
    public partial class FormRegister : Form
    {
        public FormRegister()
        {
            InitializeComponent();
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Username dan password tidak boleh kosong.");
                return;
            }

            if (txtPassword.Text != txtConfirm.Text)
            {
                MessageBox.Show("Password dan konfirmasi tidak cocok.");
                return;
            }

            if (txtPassword.Text.Length < 6)
            {
                MessageBox.Show("Password minimal 6 karakter.");
                return;
            }

            try
            {
                var (client, reader, writer, _, _) = FormLogin.ConnectAndExchangeKeys();

                string pwHash = CryptoHelper.Sha256Hash(txtPassword.Text);
                writer.WriteLine($"REGISTER|{txtUsername.Text}|{pwHash}|user");

                string response = reader.ReadLine();
                client.Close();

                if (response == "REGISTER_SUCCESS")
                {
                    MessageBox.Show("Registrasi berhasil! Silakan login.");
                    this.Close();
                }
                else
                {
                    string[] parts = response?.Split('|');
                    string reason = parts != null && parts.Length > 1 ? parts[1] : "Registrasi gagal.";
                    MessageBox.Show(reason);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal terhubung ke server: " + ex.Message);
            }
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
