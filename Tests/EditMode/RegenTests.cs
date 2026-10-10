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

        [Test]
        public void RampUp_WaitsForTheDelay_ThenRisesToFullRate()
        {
            var rule = TestRecipes.Configure(new RampUpRegen(),
                ("delay", 2f), ("rampSeconds", 2f), ("startPerSecond", 0f), ("fullPerSecond", 10f));
            var shield = Shield(rule, 50f);

            shield.Tick(1f);
            Assert.AreEqual(50f, shield.Current, 0.001f, "still inside the delay");
            shield.Tick(1f);
            Assert.AreEqual(50f, shield.Current, 0.001f, "ramp starts at 0 per second");
            shield.Tick(1f);
            Assert.AreEqual(55f, shield.Current, 0.001f, "halfway up the ramp: 5 per second");
            shield.Tick(1f);
            Assert.AreEqual(65f, shield.Current, 0.001f, "full rate");
        }

        [Test]
        public void RampUp_StopsAtMax()
        {
            var rule = TestRecipes.Configure(new RampUpRegen(), ("delay", 0f), ("fullPerSecond", 10f), ("startPerSecond", 10f));
            var shield = Shield(rule, 95f);
            shield.Tick(10f);
            Assert.AreEqual(100f, shield.Current, 0.001f);
        }

        [Test]
        public void Regen_DoesNotRestartItsOwnDelay()
        {
            var rule = TestRecipes.Configure(new RampUpRegen(),
                ("delay", 1f), ("rampSeconds", 0.01f), ("startPerSecond", 10f), ("fullPerSecond", 10f));
            var shield = Shield(rule, 10f);
            for (int i = 0; i < 4; i++) shield.Tick(1f); // the first tick reaches the delay, all four regen
            Assert.AreEqual(50f, shield.Current, 0.001f, "regen gains must not count as a loss and restart the delay");
        }
    }
}