using System;
using System.Collections.Generic;
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

        private readonly struct Pong : ICombatEvent { }

        private int counted;
        private void Count(in Ping p) => counted += p.Value;

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

        [Test]
        public void Handlers_RunInSubscriptionOrder()
        {
            var bus = new EventBus();
            var order = new List<int>();
            bus.Subscribe<Ping>((in Ping p) => order.Add(1));
            bus.Subscribe<Ping>((in Ping p) => order.Add(2));
            bus.Subscribe<Ping>((in Ping p) => order.Add(3));
            bus.Raise(new Ping(0));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void EachEventType_KeepsItsOwnHandlers()
        {
            var bus = new EventBus();
            int pings = 0;
            int pongs = 0;
            bus.Subscribe<Ping>((in Ping p) => pings++);
            bus.Subscribe<Pong>((in Pong p) => pongs++);
            bus.Raise(new Pong());
            Assert.AreEqual(0, pings);
            Assert.AreEqual(1, pongs);
        }

        [Test]
        public void Unsubscribing_DuringARaise_FinishesThatRaise_ThenStops()
        {
            var bus = new EventBus();
            int heard = 0;
            GameEventHandler<Ping> late = (in Ping p) => heard++;
            bus.Subscribe<Ping>((in Ping p) => bus.Unsubscribe(late));
            bus.Subscribe(late);
            bus.Raise(new Ping(0));
            bus.Raise(new Ping(0));
            Assert.AreEqual(1, heard);
        }

        [Test]
        public void Subscribing_DuringARaise_WaitsForTheNextRaise()
        {
            var bus = new EventBus();
            int heard = 0;
            bool added = false;
            GameEventHandler<Ping> extra = (in Ping p) => heard++;
            bus.Subscribe<Ping>((in Ping p) =>
            {
                if (added) return;
                added = true;
                bus.Subscribe(extra);
            });
            bus.Raise(new Ping(0));
            Assert.AreEqual(0, heard);
            bus.Raise(new Ping(0));
            Assert.AreEqual(1, heard);
        }

        [Test]
        public void AThrowingHandler_IsLoggedOnce_AndTheRestStillRun()
        {
            var bus = new EventBus();
            int after = 0;
            bus.Subscribe<Ping>((in Ping p) => throw new InvalidOperationException("boom"));
            bus.Subscribe<Ping>((in Ping p) => after++);

            using (var logs = new CapturedLogs())
            {
                Assert.DoesNotThrow(() => bus.Raise(new Ping(0)));
                Assert.AreEqual(1, after);
                Assert.AreEqual(1, logs.Exceptions.Count);
                Assert.AreEqual(0, logs.Errors.Count);
                var logged = logs.Exceptions[0] as EventHandlerException;
                Assert.IsNotNull(logged);
                Assert.AreEqual(typeof(Ping), logged.EventType);
                Assert.IsInstanceOf<InvalidOperationException>(logged.InnerException);
                StringAssert.Contains("Ping handler", logged.Message);
            }
        }

        [Test]
        public void Growth_IsReportedOnlyWhenHandlersPileUpBetweenLives()
        {
            var bus = new EventBus();
            GameEventHandler<Ping> steady = (in Ping p) => { };
            bus.Subscribe(steady);
            Assert.IsNull(bus.GrowthSinceLastLife(), "the first life only records");
            Assert.IsNull(bus.GrowthSinceLastLife(), "a steady listener isn't a leak");

            bus.Subscribe<Ping>(Count);
            bus.Subscribe<Pong>((in Pong p) => { });
            string grown = bus.GrowthSinceLastLife();
            StringAssert.Contains("Ping 1 → 2", grown);
            StringAssert.Contains("Pong 0 → 1", grown);
            StringAssert.Contains("EventBusTests.Count", grown);

            bus.Unsubscribe<Ping>(Count);
            Assert.IsNull(bus.GrowthSinceLastLife(), "fewer handlers is fine");
        }

        [Test]
        public void Unsubscribe_MatchesByEquality_AndRemovesOneCopy()
        {
            counted = 0;
            var bus = new EventBus();
            bus.Subscribe<Ping>(Count);
            bus.Subscribe<Ping>(Count);
            Assert.AreEqual(2, bus.SubscriberCount);

            bus.Unsubscribe<Ping>(Count);
            bus.Raise(new Ping(1));
            Assert.AreEqual(1, counted);
            Assert.AreEqual(1, bus.SubscriberCount);

            bus.Unsubscribe<Ping>(Count);
            bus.Unsubscribe<Ping>(Count);
            bus.Unsubscribe<Pong>((in Pong p) => { });
            Assert.AreEqual(0, bus.SubscriberCount);
        }

        [Test]
        public void Clear_DropsEveryHandler()
        {
            var bus = new EventBus();
            int heard = 0;
            bus.Subscribe<Ping>((in Ping p) => heard++);
            bus.Clear();
            bus.Raise(new Ping(0));
            Assert.AreEqual(0, heard);
            Assert.AreEqual(0, bus.SubscriberCount);
        }
    }
}