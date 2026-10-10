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
        public IEnumerator Effects_ExpireOnRealFrames()
        {
            var haste = world.Assets.Effect("Haste", duration: 0.2f);
            var unit = world.Spawn(world.CreateTemplate());
            unit.TryGet<IStatusEffects>(out var effects);
            effects.Apply(haste, unit);

            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(effects.Has(haste));
        }

        [UnityTest]
        public IEnumerator Pause_FreezesEffectTimers()
        {
            var haste = world.Assets.Effect("Haste", duration: 0.2f);
            var unit = world.Spawn(world.CreateTemplate());
            unit.TryGet<IStatusEffects>(out var effects);
            effects.Apply(haste, unit);

            CombatServices.Pause(this);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(effects.Has(haste), "paused time must not count");

            CombatServices.Unpause(this);
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(effects.Has(haste));
        }
    }
}