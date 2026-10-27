using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CG.CombatFramework.Tests
{
    /// <summary>Real frames and the real CombatTicker. Slower, so only the things a manual tick source can't prove.</summary>
    public class RealTickerTests
    {
        private TestWorld world;

        [SetUp] public void SetUp() => world = new TestWorld(realTicker: true);
        [TearDown] public void TearDown() => world.Dispose();

        [UnityTest]
        public IEnumerator Pause_FreezesEffectTimersAndScheduledPayloads()
        {
            var haste = world.Assets.Effect("Haste", duration: 0.2f);
            var unit = world.Spawn(world.CreateTemplate());
            unit.TryGet<IStatusEffects>(out var effects);
            effects.Apply(haste, unit);
            var delayed = new CountingPayload();
            TestRecipes.Configure(new DelayPayload(), ("delay", 0.2f), ("payloads", new IPayload[] { delayed }))
                .Apply(new EffectContext(unit, null, unit));

            CombatServices.Pause(this);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(effects.Has(haste), "paused time must not count");
            Assert.AreEqual(0, delayed.Count);

            CombatServices.Unpause(this);
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(effects.Has(haste), "expires on real frames");
            Assert.AreEqual(1, delayed.Count);
        }
    }
}
