using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class EventBusTests
    {
        private readonly struct Ping : ICombatEvent
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

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void GameEvents_ReachTheGlobalBus_OnlyWhenTheUnitsBusForwards(bool forwards, int expectedGlobal)
        {
            var unit = new FakeUnit("Unit", forwards);
            int local = 0;
            int global = 0;
            GameEventHandler<Ping> onLocal = (in Ping p) => local++;
            GameEventHandler<Ping> onGlobal = (in Ping p) => global++;
            unit.Events.Subscribe(onLocal);
            CombatServices.Events.Subscribe(onGlobal);
            try
            {
                GameEvents.Raise(unit, new Ping(1));
            }
            finally
            {
                CombatServices.Events.Unsubscribe(onGlobal);
                unit.Dispose();
            }

            Assert.AreEqual(1, local);
            Assert.AreEqual(expectedGlobal, global);
        }

        [Test]
        public void GameEvents_WithNoUnit_GoGlobalOnly()
        {
            int global = 0;
            GameEventHandler<Ping> onGlobal = (in Ping p) => global++;
            CombatServices.Events.Subscribe(onGlobal);
            try
            {
                GameEvents.Raise(null, new Ping(1));
                GameEvents.RaiseGlobal(new Ping(1));
            }
            finally
            {
                CombatServices.Events.Unsubscribe(onGlobal);
            }

            Assert.AreEqual(2, global);
        }
    }
}