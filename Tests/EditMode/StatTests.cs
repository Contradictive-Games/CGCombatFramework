using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class StatTests
    {
        private static StatType MakeType(float defaultValue = 0f)
        {
            var type = ScriptableObject.CreateInstance<StatType>();
            type.DefaultValue = defaultValue;
            return type;
        }

        [Test]
        public void NewStat_ReadsItsBaseValue()
        {
            Assert.AreEqual(18.9f, new Stat(MakeType(), 18.9f).Value);
        }
        [Test]
        public void Modifiers_ApplyFlatThenPercentAddThenPercetMult()
        {
            var stat = new Stat(MakeType(), 100f);
            stat.AddModifier(new StatModifier(10f, StatModifierType.Flat, null));
            stat.AddModifier(new StatModifier(0.2f, StatModifierType.PercentAdd, null));
            stat.AddModifier(new StatModifier(0.5f, StatModifierType.PercentMult, null));
            Assert.That(stat.Value, Is.EqualTo(198f).Within(0.001f));
        }

        [Test]
        public void RemoveAllFromSource_RestoresTheValue()
        {
            var source = new object();
            var stat = new Stat(MakeType(), 50f);
            stat.AddModifier(new StatModifier(5f, StatModifierType.Flat, source));
            stat.AddModifier(new StatModifier(5f, StatModifierType.Flat, source));
            Assert.AreEqual(2, stat.RemoveAllModifiersFromSource(source));
            Assert.AreEqual(50f, stat.Value);
        }

        [Test]
        public void StatBlock_WithoutProfile_CreatesStatsAtTheirDefault()
        {
            var block = new StatBlock(null);
            Assert.AreEqual(1.5f, block.GetOrCreate(MakeType(1.5f)).Value);
        }
    }
}
