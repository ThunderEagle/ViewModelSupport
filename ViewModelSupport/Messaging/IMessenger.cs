using System;
using System.Collections.Generic;
using System.Text;

namespace ViewModelSupport.Messaging
{
    public interface IMessenger
    {
        void Publish<T>(T message) where T : MessageBase;
        void Subscribe<T>(object target, Action<T> handler) where T:MessageBase;
        void Unsubscribe<T>(object target);
        void Unsubscribe(object target);
    }
}
