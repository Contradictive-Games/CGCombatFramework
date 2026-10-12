using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class ContextTests
    {
        [Test]
        public void Retarget_ChangesOnlyTheTarget_AndNestedGoesDeeper()
        {
            using var a = new FakeUnit("A");
            using var b = new FakeUnit("B");
            using var c = new FakeUnit("C");
            var ctx = new EffectContext(a, null, b, default, power: 2f, stacks: 3, procDepth: 1, amount: 40f);

            var moved = ctx.Retarget(c, default);
            Assert.AreSame(a, moved.SourceUnit);
            Assert.AreSame(c, moved.TargetUnit);
            Assert.AreEqual(2f, moved.Power);
            Assert.AreEqual(3, moved.Stacks);
            Assert.AreEqual(1, moved.ProcDepth);
            Assert.AreEqual(40f, moved.Amount);

            Assert.AreEqual(2, ctx.Nested(c, default).ProcDepth);
            Assert.AreEqual(7, ctx.ForStacks(7).Stacks);
        }
    }

    public class SchedulerTests
    {
        private ManualTickSource ticks;

        [SetUp]
        public void SetUp()
        {
            ticks = new ManualTickSource();
            CombatServices.TickSource = ticks;
            CombatScheduler.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CombatScheduler.Clear();
            CombatServices.UnpauseAll();
            CombatServices.TickSource = null;
        }

        [Test]
        public void RunsOnceAfterTheDelay()
        {
            var probe = new CountingPayload();
            CombatScheduler.Schedule(1f, new IPayload[] { probe }, new EffectContext(null, null));
            ticks.Step(0.5f);
            Assert.AreEqual(0, probe.Count);
            ticks.Step(0.6f);
            Assert.AreEqual(1, probe.Count);
            ticks.Step(5f);
            Assert.AreEqual(1, probe.Count);
        }

        [Test]
        public void PausedTime_DoesNotCount()
        {
            var probe = new CountingPayload();
            CombatScheduler.Schedule(1f, new IPayload[] { probe }, new EffectContext(null, null));
            CombatServices.Pause(this);
            ticks.Step(5f);
            Assert.AreEqual(0, probe.Count);
            CombatServices.Unpause(this);
            ticks.Step(1.1f);
            Assert.AreEqual(1, probe.Count);
        }
    }
}