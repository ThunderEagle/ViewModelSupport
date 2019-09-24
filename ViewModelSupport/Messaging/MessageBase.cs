using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModelSupport.Messaging
{
    public interface IMessageBase {
        object Sender { get; }
    }

    public abstract class MessageBase:IMessageBase
    {
        protected MessageBase() { }

        protected MessageBase(object sender)
        {
            Sender = sender;
        }
        public object Sender { get; }
    }

    
}
