using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class StatTests
    {
        private TestAssets assets;

        [SetUp] public void SetUp() => assets = new TestAssets();
        [TearDown] public void TearDown() => assets.Dispose();

        [Test]
        public void Modifiers_ApplyFlatThenPercentAddThenPercentMult()
        {
            var stat = new Stat(assets.Stat("Damage"), 100f);
            stat.AddModifier(new StatModifier(10f, StatModifierType.Flat, null));
            stat.AddModifier(new StatModifier(0.2f, StatModifierType.PercentAdd, null));
            stat.AddModifier(new StatModifier(0.5f, StatModifierType.PercentMult, null));
            Assert.That(stat.Value, Is.EqualTo(198f).Within(0.001f));
        }

        [Test]
        public void RemoveAllFromSource_RestoresTheBaseValue()
        {
            var source = new object();
            var stat = new Stat(assets.Stat("Damage"), 50f);
            stat.AddModifier(new StatModifier(5f, StatModifierType.Flat, source));
            stat.AddModifier(new StatModifier(5f, StatModifierType.Flat, source));
            Assert.AreEqual(60f, stat.Value);
            Assert.AreEqual(2, stat.RemoveAllModifiersFromSource(source));
            Assert.AreEqual(50f, stat.Value);
        }

        [Test]
        public void StatBlock_WithoutProfile_CreatesStatsAtTheirDefault()
        {
            var block = new StatBlock(null);
            Assert.AreEqual(1.5f, block.GetOrCreate(assets.Stat("Haste", 1.5f)).Value);
        }
    }
}
