using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class WhileTests
    {
        private TestAssets assets;
        private FakeUnit host;
        private Resource life;
        private StatusEffectsComponent effects;
        private StatType damage;
        private StatusEffectDefinition berserk;
        private StatusEffectDefinition brand;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            host = new FakeUnit("Host");
            var healthType = assets.Make<ResourceType>("Health");
            var resources = new FakeResources();
            life = resources.Add(healthType, 100f, 100f);
            host.Add(resources);
            effects = host.AddUnitComponent<StatusEffectsComponent>();

            damage = assets.Stat("DamageMultiplier", 1f);
            berserk = assets.Effect("Berserk", duration: 0f,
                behaviors: TestRecipes.Modifier(damage, 0.5f, StatModifierType.PercentAdd));
            var lowHealth = TestRecipes.Configure(new ResourceThresholdCondition(), ("resource", healthType), ("fraction", 0.3f));
            var whileLow = TestRecipes.Configure(new WhileBehavior(),
                ("conditions", new ICondition[] { lowHealth }), ("effect", berserk));
            brand = assets.Effect("Berserker's Brand", duration: 0f, tickInterval: 0.1f, behaviors: whileLow);
        }

        [TearDown]
        public void TearDown()
        {
            host.Dispose();
            assets.Dispose();
        }

        [Test]
        public void GrantsTheEffect_OnlyWhileTheConditionHolds()
        {
            effects.Apply(brand, host);
            Assert.IsFalse(effects.Has(berserk));

            life.Set(20f);
            effects.Tick(0.1f);
            Assert.IsTrue(effects.Has(berserk));
            Assert.AreEqual(1.5f, host.GetStat(damage), 0.001f);

            life.Set(80f);
            effects.Tick(0.1f);
            Assert.IsFalse(effects.Has(berserk));
            Assert.AreEqual(1f, host.GetStat(damage), 0.001f);
        }

        [Test]
        public void RemovingTheOwner_RemovesWhatItGranted()
        {
            life.Set(20f);
            effects.Apply(brand, host);
            Assert.IsTrue(effects.Has(berserk), "checked on apply, not only on tick");
            effects.RemoveAll(brand);
            Assert.IsFalse(effects.Has(berserk));
        }
    }
}