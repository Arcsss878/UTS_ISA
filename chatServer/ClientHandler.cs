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

            // Generate session token (berlaku 1 jam)
            string   token  = Guid.NewGuid().ToString("N");
            DateTime expiry = DateTime.Now.AddHours(1);

            var info = new ClientInfo
            {
                TcpClient    = client,
                Writer       = writer,
                AesKey       = aesKey,
                AesIv        = aesIv,
                Role         = role,
                SessionToken = token,
                TokenExpiry  = expiry
            };

            // Kirim LOGIN_SUCCESS + token + expiry dulu sebelum AddClient (hindari race condition)
            writer.WriteLine($"LOGIN_SUCCESS|{role}|{token}|{expiry:HH:mm:ss}");
            ClientManager.AddClient(username, info);

            // Kirim riwayat chat lengkap (history) sebagai HISTORY|sender|receiver|enc|time
            foreach (var (s, r, plain, t) in DatabaseHelper.GetAllMessages(username))
            {
                string reEnc = CryptoHelper.AesEncrypt(plain, aesKey, aesIv);
                writer.WriteLine($"HISTORY|{s}|{r}|{reEnc}|{t}");
            }

            // Kirim pesan pending (offline) sebagai MSG biasa
            foreach (var (sender, plain, sentAt) in DatabaseHelper.GetPendingMessages(username))
            {
                string reEnc = CryptoHelper.AesEncrypt(plain, aesKey, aesIv);
                writer.WriteLine($"MSG|{sender}|{reEnc}|{sentAt}");
            }

            Console.WriteLine($"{username} ({role}) connected");

        }

        private void HandleChat(string[] parts)
        {
            // CHAT|from|to|<aes_encrypted_message>|token
            if (parts.Length < 5) return;

            string from       = parts[1];
            string to         = parts[2];
            string encMsg     = parts[3];
            string token      = parts[4];

            // Validasi session token
            ClientInfo senderInfo = ClientManager.GetClientInfo(from);
            if (senderInfo == null || senderInfo.SessionToken != token)
            {
                writer.WriteLine("ERROR|Session tidak valid, silakan login ulang.");
                return;
            }
            if (DateTime.Now > senderInfo.TokenExpiry)
            {
                writer.WriteLine("ERROR|Session expired, silakan login ulang.");
                ClientManager.RemoveClient(from);
                return;
            }

            // Dekripsi pesan dari sender (pakai AES key milik sender)
            string plainText = CryptoHelper.AesDecrypt(encMsg, aesKey, aesIv);

            // Simpan ke DB dengan delivered=0, dapat ID row yang baru dibuat
            long msgId = DatabaseHelper.SaveMessage(from, to, encMsg, plainText);

            // Forward ke recipient kalau sedang online
            ClientInfo recipientInfo = ClientManager.GetClientInfo(to);
            if (recipientInfo != null)
            {
                string reEncrypted = CryptoHelper.AesEncrypt(
                    plainText, recipientInfo.AesKey, recipientInfo.AesIv);
                try
                {
                    recipientInfo.Writer.WriteLine($"MSG|{from}|{reEncrypted}");
                    // Recipient online dan berhasil menerima → tandai delivered=1
                    DatabaseHelper.MarkDelivered(msgId);
                }
                catch { }
            }
            // Kalau offline: delivered=0, dikirim saat target login (via GetPendingMessages)
        }

        private void HandleTokenRefresh(string[] parts)
        {
            // TOKEN_REFRESH|username|old_token
            if (parts.Length < 3) return;
            string user     = parts[1];
            string oldToken = parts[2];

            ClientInfo info = ClientManager.GetClientInfo(user);
            if (info == null || info.SessionToken != oldToken)
            {
                writer.WriteLine("TOKEN_REFRESH_FAIL|Session tidak valid.");
                return;
            }

            string   newToken  = Guid.NewGuid().ToString("N");
            DateTime newExpiry = DateTime.Now.AddHours(1);
            info.SessionToken = newToken;
            info.TokenExpiry  = newExpiry;

            writer.WriteLine($"TOKEN_REFRESHED|{newToken}|{newExpiry:HH:mm:ss}");
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
