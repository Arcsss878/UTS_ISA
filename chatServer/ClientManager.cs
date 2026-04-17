using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace UTS_ISA.users
{
    public class ClientManager
    {
        public static Dictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();

        public static void AddClient(string username, TcpClient client)
        {
            clients[username] = client;
            BroadcastUsers();
        }

        public static void RemoveClient(string username)
        {
            if (clients.ContainsKey(username))
            {
                clients.Remove(username);
                BroadcastUsers();
            }
        }

        public static TcpClient GetClient(string username)
        {
            return clients.ContainsKey(username) ? clients[username] : null;
        }

        public static List<string> GetAllUsers()
        {
            return clients.Keys.ToList();
        }

        public static void BroadcastUsers()
        {
            string userList = string.Join(",", clients.Keys);

            foreach (var client in clients.Values)
            {
                try
                {
                    var writer = new System.IO.StreamWriter(client.GetStream()) { AutoFlush = true };
                    writer.WriteLine("USERS|" + userList);
                }
                catch { }
            }
        }
    }
}
