using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UTS_ISA.users
{
    public class ClientHandler
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private string username = "";

        public ClientHandler(TcpClient client)
        {
            this.client = client;

            reader = new StreamReader(client.GetStream());
            writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
        }

        public void Start()
        {
            Thread t = new Thread(Run);
            t.Start();
        }

        private void Run()
        {
            try
            {
                while (true)
                {
                    string data = reader.ReadLine();
                    if (data == null) break;

                    string[] parts = data.Split('|');

                    switch (parts[0])
                    {
                        case "LOGIN":
                            HandleLogin(parts);
                            break;

                        case "CHAT":
                            HandleChat(parts);
                            break;
                    }
                }
            }
            catch { }

            Disconnect();
        }

        private void HandleLogin(string[] parts)
        {
            username = parts[1];

            ClientManager.AddClient(username, client);

            writer.WriteLine("LOGIN_SUCCESS");

            Console.WriteLine(username + " connected");
        }

        private void HandleChat(string[] parts)
        {
            string from = parts[1];
            string to = parts[2];
            string message = parts[3];

            TcpClient target = ClientManager.GetClient(to);

            if (target != null)
            {
                var targetWriter = new StreamWriter(target.GetStream()) { AutoFlush = true };
                targetWriter.WriteLine($"MSG|{from}|{message}");
            }
        }

        private void Disconnect()
        {
            Console.WriteLine(username + " disconnected");

            if (!string.IsNullOrEmpty(username))
            {
                ClientManager.RemoveClient(username);
            }

            client.Close();
        }
    }
}
