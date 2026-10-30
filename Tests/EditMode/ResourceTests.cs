using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class ResourceTests
    {
        [Test]
        public void Depleted_FiresOnlyOnce()
        {
            var unit = new FakeUnit("Owner");
            try
            {
                var health = new Resource(null, 100f, owner: unit);
                health.ResetTo(100f);
                int depleted = 0;
                unit.Events.Subscribe<ResourceDepletedEvent>((in ResourceDepletedEvent _) => depleted++);
                health.Remove(150f, ChangeReason.Damage);
                health.Remove(10f, ChangeReason.Damage);

                Assert.AreEqual(1, depleted);
                Assert.AreEqual(0f, health.Current);
            }
            finally
            {
                unit.Dispose();
            }
        }

        [Test]
        public void KeepCurrent_DoesNotRefillWhenMaxRises()
        {
            var health = new Resource(null, 100f, policy: MaxChangePolicy.KeepCurrent);
            health.ResetTo(50f);
            health.BaseMax = 150f;
            Assert.AreEqual(50f, health.Current);
        }
    }
}
