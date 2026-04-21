using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UTS_ISA
{
    public partial class FormChat : Form
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread receiveThread;

        private string username;
        private string role;
        private byte[] aesKey;
        private byte[] aesIv;

        public FormChat() { InitializeComponent(); }

        public FormChat(TcpClient c, StreamReader r, StreamWriter w,
                        string user, byte[] key, byte[] iv, string userRole)
        {
            InitializeComponent();

            client   = c;
            reader   = r;
            writer   = w;
            username = user;
            aesKey   = key;
            aesIv    = iv;
            role     = userRole;

            this.Text     = $"Secure Chat — {username}";
            lblRole.Text  = $"Login sebagai: {username} [{role}]";

            receiveThread = new Thread(ReceiveMessage) { IsBackground = true };
            receiveThread.Start();
        }

        private void ReceiveMessage()
        {
            while (true)
            {
                try
                {
                    string data = reader.ReadLine();
                    if (data == null) break;

                    string[] parts = data.Split('|');

                    Invoke(new Action(() =>
                    {
                        if (parts[0] == "MSG" && parts.Length >= 3)
                        {
                            string from         = parts[1];
                            string encryptedMsg = parts[2];

                            string plainMsg = CryptoHelper.AesDecrypt(encryptedMsg, aesKey, aesIv);
                            lstRiwayat.Items.Add($"[{DateTime.Now:HH:mm}] {from}: {plainMsg}");
                            lstRiwayat.TopIndex = lstRiwayat.Items.Count - 1;
                        }
                        else if (parts[0] == "USERS" && parts.Length >= 2)
                        {
                            lstUser.Items.Clear();
                            string[] users = parts[1].Split(',');
                            foreach (var u in users)
                            {
                                if (!string.IsNullOrEmpty(u) && u != username)
                                    lstUser.Items.Add(u);
                            }
                        }
                    }));
                }
                catch { break; }
            }
        }

        private void btnKirim_Click(object sender, EventArgs e)
        {
            if (lstUser.SelectedItem == null)
            {
                MessageBox.Show("Pilih user tujuan dulu!");
                return;
            }

            string msg = txtChat.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            string target       = lstUser.SelectedItem.ToString();
            string encryptedMsg = CryptoHelper.AesEncrypt(msg, aesKey, aesIv);

            writer.WriteLine($"CHAT|{username}|{target}|{encryptedMsg}");

            lstRiwayat.Items.Add($"[{DateTime.Now:HH:mm}] Me → {target}: {msg}");
            lstRiwayat.TopIndex = lstRiwayat.Items.Count - 1;

            txtChat.Clear();
            txtChat.Focus();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter   = "Text File|*.txt";
                sfd.FileName = $"chat_history_{username}_{DateTime.Now:yyyyMMdd_HHmm}.txt";

                if (sfd.ShowDialog() != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("=== Riwayat Chat ===");
                sb.AppendLine($"User    : {username}");
                sb.AppendLine($"Role    : {role}");
                sb.AppendLine($"Diekspor: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine(new string('=', 40));
                sb.AppendLine();

                foreach (var item in lstRiwayat.Items)
                    sb.AppendLine(item.ToString());

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Riwayat chat berhasil diekspor!");
            }
        }

        private void FormChat_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { client?.Close(); } catch { }
            Application.Exit();
        }
    }
}
