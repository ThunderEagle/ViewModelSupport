using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModelSupport.Messaging
{
    public class MessageBase
    {
        public MessageBase() { }

        public MessageBase(object sender)
        {
            Sender = sender;
        }
        public object Sender { get; }
    }

    
}
