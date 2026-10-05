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
    }
}
