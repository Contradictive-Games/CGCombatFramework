using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class PayloadTests
    {
        private TestAssets assets;
        private FakeUnit attacker;
        private FakeUnit target;
        private FakeHealth health;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            attacker = new FakeUnit("Attacker");
            target = new FakeUnit("Target");
            health = new FakeHealth { Owner = target };
            target.Add(health);
        }

        [TearDown]
        public void TearDown()
        {
            attacker.Dispose();
            target.Dispose();
            assets.Dispose();
        }

        private EffectContext Hit() => new(attacker, null, target);

        [Test]
        public void Damage_SendsItsAmountAndFlags()
        {
            TestRecipes.Damage(25f, DamageFlags.Periodic).Apply(Hit());
            Assert.AreEqual(25f, health.Received[0].Amount);
            Assert.IsTrue(health.Received[0].Has(DamageFlags.Periodic));
        }

        [Test]
        public void ApplyStatusEffect_StacksOnTheTarget_AndCreditsTheSource()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var poison = assets.Effect("Poison", maxStacks: 5);
            var payload = new ApplyStatusEffectPayload();
            TestAssets.Set(payload, "effect", poison);
            TestAssets.Set(payload, "stacks", 2);

            payload.Apply(Hit());
            Assert.AreEqual(2, effects.GetStacks(poison));
            Assert.AreSame(attacker, effects.Active[0].SourceUnit);
        }

        [Test]
        public void HasStatusEffect_ChecksMinimumStacks()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var bleed = assets.Effect("Bleed", maxStacks: 5);
            var condition = new HasStatusEffectCondition();
            TestAssets.Set(condition, "effect", bleed);
            TestAssets.Set(condition, "minStacks", 3);

            effects.Apply(bleed, attacker, 2);
            Assert.IsFalse(condition.Evaluate(Hit()));
            effects.Apply(bleed, attacker);
            Assert.IsTrue(condition.Evaluate(Hit()));
        }
    }
}