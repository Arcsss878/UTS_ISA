using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace UTS_ISA.users
{
    public class ClientInfo
    {
        public TcpClient TcpClient { get; set; }
        public System.IO.StreamWriter Writer { get; set; }
        public byte[] AesKey { get; set; }
        public byte[] AesIv { get; set; }
        public string Role { get; set; }
        public string SessionToken { get; set; }
        public DateTime TokenExpiry { get; set; }
    }

    public static class ClientManager
    {
        private static readonly Dictionary<string, ClientInfo> clients = new Dictionary<string, ClientInfo>();

        public static void AddClient(string username, ClientInfo info)
        {
            lock (clients)
            {
                clients[username] = info;
            }
            BroadcastUsers();
        }

        public static void RemoveClient(string username)
        {
            lock (clients)
            {
                if (clients.ContainsKey(username))
                    clients.Remove(username);
            }
            BroadcastUsers();
        }

        public static ClientInfo GetClientInfo(string username)
        {
            lock (clients)
            {
                return clients.ContainsKey(username) ? clients[username] : null;
            }
        }

        public static TcpClient GetClient(string username)
        {
            lock (clients)
            {
                return clients.ContainsKey(username) ? clients[username].TcpClient : null;
            }
        }

        public static List<string> GetAllUsers()
        {
            lock (clients)
            {
                return new List<string>(clients.Keys);
            }
        }

        public static void BroadcastUsers()
        {
            // Ambil semua user dari DB + tandai online/offline
            var allUsers   = DatabaseHelper.GetAllUsernames();
            List<ClientInfo> infos;
            HashSet<string> onlineSet;

            lock (clients)
            {
                infos     = new List<ClientInfo>(clients.Values);
                onlineSet = new HashSet<string>(clients.Keys);
            }

            // Format: "username:online" atau "username:offline"
            var parts = allUsers.ConvertAll(u => u + ":" + (onlineSet.Contains(u) ? "online" : "offline"));
            string payload = string.Join(",", parts);

            foreach (var info in infos)
            {
                try { info.Writer.WriteLine("ALL_USERS|" + payload); }
                catch { }
            }
        }
    }
}
