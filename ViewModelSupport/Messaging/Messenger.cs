using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace ViewModelSupport.Messaging
{
    public class Messenger : IMessenger
    {
        private readonly ConcurrentDictionary<Type, List<WeakReference>> _subscribers;

        private static readonly object _defaultLock = new object();
        private static IMessenger _defaultInstance;

        public static IMessenger Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    lock (_defaultLock)
                    {
                        if (_defaultInstance == null)
                        {
                            _defaultInstance = new Messenger();
                        }
                    }
                }
                return _defaultInstance;
            }
        }

        public Messenger()
        {
            _subscribers = new ConcurrentDictionary<Type, List<WeakReference>>();
        }


        public void Publish<T>(T message) where T : MessageBase
        {
            var subscriptions = _subscribers[typeof(T)];
            foreach (var subscription in subscriptions)
            {
                if (subscription.Target is MessageHandler<T> messageHandler && messageHandler.Target.IsAlive)
                {
                    messageHandler.Handler?.Invoke(message);
                }
            }
        }

        public void Subscribe<T>(object target, Action<T> handler) where T : MessageBase
        {
            var messageType = typeof(T);
            if (!_subscribers.ContainsKey(messageType))
            {
                _subscribers.TryAdd(messageType, new List<WeakReference>());
            }
            var list = _subscribers[messageType];
            var messageHandler = new MessageHandler<T>(target, handler);
            list.Add(new WeakReference(messageHandler));
        }

        public void Unsubscribe<T>(object target)
        {
            var messageType = typeof(T);


            var subscription = _subscribers[messageType];

            subscription.RemoveAll(s => ((MessageHandler<MessageBase>)s.Target).Target == target);
            if (subscription.Count == 0)
            {
                _subscribers.TryRemove(messageType, out subscription);
            }

        }

        public void Unsubscribe(object target)
        {
            foreach (var handler in _subscribers)
            {
                handler.Value.RemoveAll(s => ((MessageHandler<MessageBase>)s.Target).Target == target);
            }
        }


        public void Reset()
        {
            _subscribers.Clear();
        }

        private class MessageHandler<T> where T : MessageBase
        {
            public MessageHandler(object target, Action<T> handler)
            {
                Target = new WeakReference(target);
                Handler = handler;
            }

            public WeakReference Target { get; }


            public Action<T> Handler { get; }
        }
    }
}
