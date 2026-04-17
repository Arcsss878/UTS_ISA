using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UTS_ISA
{
    public partial class FormChat : Form
    {

        TcpClient client;
        StreamReader reader;
        StreamWriter writer;
        Thread thread;

        string username;

        public FormChat()
        {
            InitializeComponent();
        }
        public FormChat(TcpClient c, StreamReader r, StreamWriter w, string user)
        {
            InitializeComponent();

            client = c;
            reader = r;
            writer = w;
            username = user;

            thread = new Thread(ReceiveMessage);
            thread.Start();
        }

        private void ReceiveMessage()
        {
            while (true)
            {
                try
                {
                    string data = reader.ReadLine();
                    string[] parts = data.Split('|');

                    Invoke(new Action(() =>
                    {
                        if (parts[0] == "MSG")
                        {
                            txtChat.AppendText(parts[1] + ": " + parts[2] + Environment.NewLine);
                        }
                        else if (parts[0] == "USERS")
                        {
                            lstRiwayat.Items.Clear();
                            string[] users = parts[1].Split(',');

                            foreach (var u in users)
                            {
                                if (u != username)
                                    lstUser.Items.Add(u);
                            }
                        }
                    }));
                }
                catch { 
                    break; 
                }
            }
        }
        private void btnKirim_Click(object sender, EventArgs e)
        {
            if (lstUser.SelectedItem == null)
            {
                MessageBox.Show("Pilih user dulu!");
                return;
            }

            string target = lstUser.SelectedItem.ToString();
            string msg = lstRiwayat.Text;

            writer.WriteLine($"CHAT|{username}|{target}|{msg}");

            txtChat.AppendText("Me -> " + target + ": " + msg + Environment.NewLine);

            txtChat.Clear();
        }
    }
}
