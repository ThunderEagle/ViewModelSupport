using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Internal;
using ViewModelSupport.Messaging;

namespace ViewModelSupportTests
{
    public class TestMessage : MessageBase
    {
        public TestMessage(string message)
        {
            Message = message;
        }
        public string Message { get; set; }
    }


    [TestFixture]
    public class MessengerTests
    {
        [Test]
        public void TestPublishSubscribe()
        {
            Messenger.Default.Subscribe<TestMessage>(this, HandleIt );

            Messenger.Default.Publish(new TestMessage("test"));
        }

        public void HandleIt(TestMessage message)
        {
            Assert.NotNull(message.Message);
        }
    }
}
