using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatServer
{
    class Messages
    {

        string messagesId;
        string sender;
        string receiver;
        string message;
        DateTime sendedAt;

        public Messages(string messagesId, string sender, string receiver, string messages, DateTime sendedAt)
        {
            this.MessagesId = messagesId;
            this.Sender = sender;
            this.Receiver = receiver;
            this.Message = message;
            this.SendedAt = sendedAt;
        }

        public string MessagesId { get => messagesId; set => messagesId = value; }
        public string Sender { get => sender; set => sender = value; }
        public string Receiver { get => receiver; set => receiver = value; }
        public string Message { get => message; set => message = value; }
        public DateTime SendedAt { get => sendedAt; set => sendedAt = value; }
    }
}
