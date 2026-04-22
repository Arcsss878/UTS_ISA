using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatServer
{
    class Users
    {

        int userId;
        string username;
        string password;
        DateTime createdAt;
        string role;

        public Users(int userId, string username, string password, DateTime createdAt, string role)
        {
            this.UserId = userId;
            this.Username = username;
            this.Password = password;
            this.CreatedAt = createdAt;
            this.Role = role;
        }

        public int UserId { get => userId; set => userId = value; }
        public string Username { get => username; set => username = value; }
        public string Password { get => password; set => password = value; }
        public DateTime CreatedAt { get => createdAt; set => createdAt = value; }
        public string Role { get => role; set => role = value; }
    }
}
