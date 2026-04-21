using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace UTS_ISA.users
{
    public class ClientHandler
    {
        // RSA keypair dibuat sekali saat server start, dipakai semua koneksi
        private static readonly System.Security.Cryptography.RSACryptoServiceProvider ServerRsa =
            new System.Security.Cryptography.RSACryptoServiceProvider(2048);
        private static readonly string ServerPublicKeyXml = ServerRsa.ToXmlString(false);
        private static readonly string ServerPrivateKeyXml = ServerRsa.ToXmlString(true);

        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private string username = "";
        private byte[] aesKey;
        private byte[] aesIv;

        public ClientHandler(TcpClient client)
        {
            this.client = client;
            reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
        }

        public void Start()
        {
            new Thread(Run) { IsBackground = true }.Start();
        }

        private void Run()
        {
            try
            {
                // Step 1: Kirim RSA public key ke client
                string pubKeyB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ServerPublicKeyXml));
                writer.WriteLine("PUBKEY|" + pubKeyB64);

                // Step 2: Terima AES key yang sudah di-enkripsi dengan RSA
                string keyExchangeMsg = reader.ReadLine();
                if (keyExchangeMsg == null || !keyExchangeMsg.StartsWith("KEY_EXCHANGE|"))
                    return;

                string[] keParts = keyExchangeMsg.Split('|');
                byte[] encAesKey = Convert.FromBase64String(keParts[1]);
                byte[] encAesIv  = Convert.FromBase64String(keParts[2]);

                aesKey = CryptoHelper.RsaDecrypt(encAesKey, ServerPrivateKeyXml);
                aesIv  = CryptoHelper.RsaDecrypt(encAesIv, ServerPrivateKeyXml);

                // Step 3: Proses command selanjutnya
                while (true)
                {
                    string data = reader.ReadLine();
                    if (data == null) break;

                    string[] parts = data.Split('|');
                    switch (parts[0])
                    {
                        case "REGISTER": HandleRegister(parts); break;
                        case "LOGIN":    HandleLogin(parts);    break;
                        case "CHAT":     HandleChat(parts);     break;
                    }
                }
            }
            catch { }

            Disconnect();
        }

        private void HandleRegister(string[] parts)
        {
            // REGISTER|username|sha256_password|role
            if (parts.Length < 4) { writer.WriteLine("REGISTER_FAIL|Format salah"); return; }

            string user  = parts[1];
            string pwHash = parts[2];
            string role  = parts[3] == "admin" ? "admin" : "user";

            bool ok = DatabaseHelper.RegisterUser(user, pwHash, role);
            writer.WriteLine(ok ? "REGISTER_SUCCESS" : "REGISTER_FAIL|Username sudah dipakai");
        }

        private void HandleLogin(string[] parts)
        {
            // LOGIN|username|sha256_password
            if (parts.Length < 3) { writer.WriteLine("LOGIN_FAIL|Format salah"); return; }

            string user   = parts[1];
            string pwHash = parts[2];

            var (success, role) = DatabaseHelper.LoginUser(user, pwHash);

            if (!success)
            {
                writer.WriteLine("LOGIN_FAIL|Username atau password salah");
                return;
            }

            username = user;
            var info = new ClientInfo
            {
                TcpClient = client,
                Writer    = writer,
                AesKey    = aesKey,
                AesIv     = aesIv,
                Role      = role
            };

            // Kirim LOGIN_SUCCESS dulu sebelum AddClient agar USERS broadcast tidak kebaca duluan
            writer.WriteLine($"LOGIN_SUCCESS|{role}");
            ClientManager.AddClient(username, info);
            Console.WriteLine($"{username} ({role}) connected");
        }

        private void HandleChat(string[] parts)
        {
            // CHAT|from|to|<aes_encrypted_message>
            if (parts.Length < 4) return;

            string from       = parts[1];
            string to         = parts[2];
            string encMsg     = parts[3];

            // Dekripsi pesan dari sender
            string plainText = CryptoHelper.AesDecrypt(encMsg, aesKey, aesIv);

            // Simpan ke DB (simpan versi terenkripsi)
            DatabaseHelper.SaveMessage(from, to, encMsg);

            // Re-enkripsi pakai AES key milik recipient, lalu forward
            ClientInfo recipientInfo = ClientManager.GetClientInfo(to);
            if (recipientInfo != null)
            {
                string reEncrypted = CryptoHelper.AesEncrypt(plainText, recipientInfo.AesKey, recipientInfo.AesIv);
                try { recipientInfo.Writer.WriteLine($"MSG|{from}|{reEncrypted}"); }
                catch { }
            }
        }

        private void Disconnect()
        {
            Console.WriteLine($"{username} disconnected");
            if (!string.IsNullOrEmpty(username))
                ClientManager.RemoveClient(username);
            try { client.Close(); } catch { }
        }
    }
}
