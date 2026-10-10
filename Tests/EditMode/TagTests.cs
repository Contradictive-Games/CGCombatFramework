using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class TagTests
    {
        [Test]
        public void Tags_AreReferenceCounted()
        {
            var tags = new TagContainer();
            var stunned = ScriptableObject.CreateInstance<CombatTag>();
            tags.Add(stunned);
            tags.Add(stunned);
            tags.Remove(stunned);
            Assert.IsTrue(tags.HasExact(stunned));
            tags.Remove(stunned);
            Assert.IsFalse(tags.HasExact(stunned));
        }

        [Test]
        public void Has_MatchesChildTags()
        {
            var state = ScriptableObject.CreateInstance<CombatTag>();
            var stunned = ScriptableObject.CreateInstance<CombatTag>();
            stunned.Parent = state;
            var tags = new TagContainer();
            tags.Add(stunned);
            Assert.IsTrue(tags.Has(state));
        }
    }
}