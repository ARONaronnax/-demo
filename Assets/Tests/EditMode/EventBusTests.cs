using System;
using NUnit.Framework;
using Demo.Core;

namespace Demo.Tests
{
    public class EventBusTests
    {
        private struct Ping { public int Value; }
        private struct Pong { public string Text; }

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();
        }

        [Test]
        public void Subscribe_ThenPublish_ReceivesEvent()
        {
            int received = 0;
            Action<Ping> handler = e => received = e.Value;

            EventBus.Subscribe(handler);
            EventBus.Publish(new Ping { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_ThenPublish_DoesNotReceive()
        {
            int callCount = 0;
            Action<Ping> handler = e => callCount++;

            EventBus.Subscribe(handler);
            EventBus.Unsubscribe(handler);
            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Publish_OnlyNotifiesMatchingType()
        {
            int pingCount = 0;
            int pongCount = 0;

            Action<Ping> onPing = e => pingCount++;
            Action<Pong> onPong = e => pongCount++;

            EventBus.Subscribe(onPing);
            EventBus.Subscribe(onPong);
            EventBus.Publish(new Ping { Value = 7 });

            Assert.AreEqual(1, pingCount);
            Assert.AreEqual(0, pongCount);
        }

        [Test]
        public void Publish_WithMultipleSubscribers_NotifiesAll()
        {
            int a = 0;
            int b = 0;
            Action<Ping> ha = e => a++;
            Action<Ping> hb = e => b++;

            EventBus.Subscribe(ha);
            EventBus.Subscribe(hb);
            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
        }

        [Test]
        public void Publish_WhenHandlerUnsubscribesDuringDispatch_DoesNotThrow()
        {
            Action<Ping> handler = null;
            handler = e => EventBus.Unsubscribe(handler);

            EventBus.Subscribe(handler);

            Assert.DoesNotThrow(() => EventBus.Publish(new Ping { Value = 1 }));
        }

        [Test]
        public void Clear_RemovesAllSubscribers()
        {
            int callCount = 0;
            Action<Ping> handler = e => callCount++;

            EventBus.Subscribe(handler);
            EventBus.Clear();
            EventBus.Publish(new Ping { Value = 1 });

            Assert.AreEqual(0, callCount);
        }
    }
}
