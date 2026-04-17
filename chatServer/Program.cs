using System;
using System.Net;
using System.Net.Sockets;
using UTS_ISA.users;

namespace ChatServer
{
    class Program
    {
        static TcpListener server;

        static void Main(string[] args)
        {
            server = new TcpListener(IPAddress.Any, 6000);
            server.Start();

            Console.WriteLine("Server started on port 6000...");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                ClientHandler handler = new ClientHandler(client);
                handler.Start();
            }
        }
    }
}