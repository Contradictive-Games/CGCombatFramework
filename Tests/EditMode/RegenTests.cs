using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class RegenTests
    {
        private static Resource Shield(IRegenRule rule, float current)
        {
            var shield = new Resource(null, 100f, new[] { rule });
            shield.ResetTo(current + 1f);
            shield.Remove(1f, ChangeReason.Damage); // a loss just now starts the delay
            return shield;
        }

        private static DelayAfterLoss Delay(float seconds, IRegenRule then) =>
            TestRecipes.Configure(new DelayAfterLoss(), ("delay", seconds), ("then", then));

        private static RampUpRegen Ramp(float seconds, float start, float full) =>
            TestRecipes.Configure(new RampUpRegen(), ("rampSeconds", seconds), ("startPerSecond", start), ("fullPerSecond", full));

        [Test]
        public void DelayThenRamp_RampStartsWhenTheDelayEnds()
        {
            var shield = Shield(Delay(2f, Ramp(2f, 0f, 10f)), 50f);

            shield.Tick(1f);
            Assert.AreEqual(50f, shield.Current, 0.001f, "inside the delay");
            shield.Tick(1f);
            Assert.AreEqual(50f, shield.Current, 0.001f, "the ramp's first moment: 0 per second");
            shield.Tick(1f);
            Assert.AreEqual(55f, shield.Current, 0.001f, "halfway up the ramp");
            shield.Tick(1f);
            Assert.AreEqual(65f, shield.Current, 0.001f, "full rate");
        }

        [Test]
        public void Ramp_StopsAtMax()
        {
            var shield = Shield(Ramp(3f, 10f, 10f), 95f);
            shield.Tick(10f);
            Assert.AreEqual(100f, shield.Current, 0.001f);
        }

        [Test]
        public void DelayThenFlat_RegensEveryTickOnceTheDelayIsOver()
        {
            var flat = TestRecipes.Configure(new FlatRegen(), ("perSecond", 10f));
            var shield = Shield(Delay(1f, flat), 10f);
            for (int i = 0; i < 4; i++) shield.Tick(1f); // the first tick reaches the delay; all four regen
            Assert.AreEqual(50f, shield.Current, 0.001f, "regen gains must not count as a loss");
        }

        [Test]
        public void AHeal_DoesNotRestartTheRamp()
        {
            var shield = Shield(Ramp(2f, 0f, 10f), 50f);
            shield.Tick(1f);                                // halfway: +5
            shield.Add(1f, ChangeReason.Heal);              // a gain, not a loss
            shield.Tick(1f);                                // full rate: +10
            Assert.AreEqual(66f, shield.Current, 0.001f);
        }

        [Test]
        public void Decay_WaitsOutTheGrace_ThenDrainsToTheFloor()
        {
            var decay = TestRecipes.Configure(new DecayRegen(), ("perSecond", 10f), ("floorPercent", 0.2f), ("graceAfterGain", 2f));
            var rage = new Resource(null, 100f, new IRegenRule[] { decay });
            rage.Add(50f, ChangeReason.Heal);

            rage.Tick(1f);
            Assert.AreEqual(50f, rage.Current, 0.001f, "inside the grace");
            rage.Tick(1f);
            Assert.AreEqual(40f, rage.Current, 0.001f);
            rage.Tick(10f);
            Assert.AreEqual(20f, rage.Current, 0.001f, "stops at the floor");
        }

        [Test]
        public void PercentOfMax_RestoresAShareOfMaxEachSecond_UpToMax()
        {
            var regen = TestRecipes.Configure(new PercentOfMaxRegen(), ("percentPerSecond", 2f));
            var health = new Resource(null, 200f, new IRegenRule[] { regen });
            health.ResetTo(100f);
            health.Tick(1f);
            Assert.AreEqual(104f, health.Current, 0.001f);
            health.Tick(100f);
            Assert.AreEqual(200f, health.Current, 0.001f);
        }
    }
}