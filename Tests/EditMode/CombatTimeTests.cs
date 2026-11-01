using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class CombatTimeTests
    {
        [TearDown]
        public void TearDown()
        {
            CombatScheduler.Clear();
            CombatServices.UnpauseAll();
            CombatServices.TickSource = null;
        }

        [Test]
        public void Time_AdvancesWithTheTickSource_AndPausesWithCombat()
        {
            var ticks = new ManualTickSource();
            CombatServices.TickSource = ticks;
            float start = CombatServices.Time;

            ticks.Step(0.5f);
            Assert.AreEqual(start + 0.5f, CombatServices.Time, 0.0001f);

            CombatServices.Pause(this);
            ticks.Step(0.5f);
            Assert.AreEqual(start + 0.5f, CombatServices.Time, 0.0001f);
        }

        [Test]
        public void Time_MovesWithTheTickSource_WhenItIsSwappedOrCleared()
        {
            var first = new ManualTickSource();
            var second = new ManualTickSource();
            CombatServices.TickSource = first;
            CombatServices.TickSource = second;
            CombatServices.TickSource = second;
            float start = CombatServices.Time;

            first.Step(1f);
            Assert.AreEqual(start, CombatServices.Time, "the old source no longer drives the clock");
            Assert.AreEqual(0, first.Count);
            Assert.AreEqual(1, second.Count, "assigning the same source twice registers the clock once");

            second.Step(1f);
            Assert.AreEqual(start + 1f, CombatServices.Time, 0.0001f);

            CombatServices.TickSource = null;
            second.Step(1f);
            Assert.AreEqual(start + 1f, CombatServices.Time, 0.0001f);
            Assert.AreEqual(0, second.Count);
        }

        [Test]
        public void Schedule_IsDueOnCombatTime_AndWaitsWhilePaused()
        {
            var ticks = new ManualTickSource();
            CombatServices.TickSource = ticks;
            ticks.Step(5f);
            var probe = new CountingPayload();
            CombatScheduler.Schedule(1f, new IPayload[] { probe }, new EffectContext(null, null));

            ticks.Step(0.9f);
            Assert.AreEqual(0, probe.Count);

            CombatServices.Pause(this);
            ticks.Step(5f);
            Assert.AreEqual(0, probe.Count);

            CombatServices.Unpause(this);
            ticks.Step(0.2f);
            Assert.AreEqual(1, probe.Count);
        }
    }
}
