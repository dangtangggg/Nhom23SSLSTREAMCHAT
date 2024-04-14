using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerChatForm
{
    internal class MessagePost
    {
        public string From_Username { get; set; }
        public string To_Username { get; set; }
        public string Message { get; set; }
    }
}
