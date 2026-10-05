using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class EventBusTests
    {
        private readonly struct Ping
        {
            public readonly int Value;
            public Ping(int value) => Value = value;
        }

        [Test]
        public void Raise_Subscribers_UntilUnsubscribed()
        {
            var bus = new EventBus();
            int total = 0;
            GameEventHandler<Ping> handler = (in Ping p) => total += p.Value;

            bus.Subscribe(handler);
            bus.Raise(new Ping(2));
            bus.Unsubscribe(handler);
            bus.Raise(new Ping(5));

            Assert.AreEqual(2, total);
        }
    }
}