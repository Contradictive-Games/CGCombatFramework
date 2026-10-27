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

            Assert.AreEqual(2, ctx.Nested(c).ProcDepth);
            Assert.AreEqual(7, ctx.ForStacks(7).Stacks);
        }
    }
}
