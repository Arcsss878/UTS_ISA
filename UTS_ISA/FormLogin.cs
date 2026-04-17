using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UTS_ISA.users;

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
            try
            {
                TcpClient client = new TcpClient("127.0.0.1", 6000);

                StreamReader reader = new StreamReader(client.GetStream());
                StreamWriter writer = new StreamWriter(client.GetStream());
                writer.AutoFlush = true;

                writer.WriteLine($"LOGIN|{txtUsername.Text}|{txtPassword.Text}");

                string response = reader.ReadLine();

                FormChat chat = new FormChat(client, reader, writer, txtUsername.Text);
                    chat.Show();
                    this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
