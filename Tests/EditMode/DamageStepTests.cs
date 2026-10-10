using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class DamageStepTests
    {
        private TestAssets assets;
        private FakeUnit attacker;
        private FakeUnit target;
        private DamagePipeline pipeline;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            attacker = new FakeUnit("Attacker");
            target = new FakeUnit("Target");
            pipeline = assets.Make<DamagePipeline>();
        }

        [TearDown]
        public void TearDown()
        {
            attacker.Dispose();
            target.Dispose();
            assets.Dispose();
        }

        private void Use(params IDamageStep[] steps) => pipeline.Steps = steps;

        private DamageCalculation Run(float amount, DamageFlags flags = DamageFlags.None, Hitbox hitbox = null)
        {
            var hit = new HitData(Vector3.zero, Vector3.up, Vector3.forward, hitbox);
            var request = new DamageRequest(amount, null, new EffectContext(attacker, null, target, in hit), flags);
            return pipeline.Run(in request, target);
        }

        private StatType TargetStat(string name, float value)
        {
            var stat = assets.Stat(name);
            target.Stats.GetOrCreate(stat).SetBaseValue(value);
            return stat;
        }

        [Test]
        public void Evade_AtFullChance_CancelsAsEvaded()
        {
            Use(TestRecipes.Configure(new EvadeStep(), ("evadeChance", TargetStat("Evade", 1f)), ("cap", 1f)));
            var calc = Run(50f);
            Assert.AreEqual(HitResult.Evaded, calc.Result);
            Assert.AreEqual(0f, calc.Amount);
        }

        [TestCase(DamageFlags.CannotEvade)]
        [TestCase(DamageFlags.Periodic)]
        public void Evade_IsSkippedBy(DamageFlags flags)
        {
            Use(TestRecipes.Configure(new EvadeStep(), ("evadeChance", TargetStat("Evade", 1f)), ("cap", 1f)));
            Assert.AreEqual(50f, Run(50f, flags).Amount);
        }

        [Test]
        public void HitboxMultiplier_UsesTheHitboxThatWasHit()
        {
            var head = new GameObject("Head").AddComponent<Hitbox>();
            head.DamageMultiplier = 2f;
            try
            {
                Use(new HitboxMultiplierStep());
                Assert.AreEqual(100f, Run(50f, hitbox: head).Amount, 0.001f);
                Assert.AreEqual(50f, Run(50f).Amount, 0.001f, "no hitbox, no multiplier");
            }
            finally { Object.DestroyImmediate(head.gameObject); }
        }

        [Test]
        public void FlatReduction_Subtracts_ButNotFromDamageOverTime()
        {
            Use(TestRecipes.Configure(new FlatReductionStep(), ("reduction", TargetStat("Toughness", 5f))));
            Assert.AreEqual(15f, Run(20f).Amount, 0.001f);
            Assert.AreEqual(0f, Run(3f).Amount, "never below zero");
            Assert.AreEqual(20f, Run(20f, DamageFlags.Periodic).Amount, 0.001f);
        }

        [Test]
        public void MinimumDamage_FloorsAfterReductions_ButNeverRaisesTheRequest()
        {
            Use(TestRecipes.Configure(new FlatReductionStep(), ("reduction", TargetStat("Toughness", 100f))),
                TestRecipes.Configure(new MinimumDamageStep(), ("minimum", 1f)));
            Assert.AreEqual(1f, Run(20f).Amount, 0.001f);
            Assert.AreEqual(0.5f, Run(0.5f).Amount, 0.001f);
        }

        [Test]
        public void ShieldAbsorb_TakesWhatItCan_AndHealthTakesTheRest()
        {
            var shieldType = assets.Make<ResourceType>("Shield");
            var resources = new FakeResources();
            target.Add(resources);
            var shield = resources.Add(shieldType, 100f, 30f);
            Use(TestRecipes.Configure(new ShieldAbsorbStep(), ("shield", shieldType)));

            var calc = Run(50f);
            Assert.AreEqual(20f, calc.Amount, 0.001f);
            Assert.AreEqual(30f, calc.Absorbed, 0.001f);
            Assert.AreEqual(0f, shield.Current);
        }

        [Test]
        public void ShieldAbsorb_RespectsIgnoreShieldAndShare()
        {
            var shieldType = assets.Make<ResourceType>("Shield");
            var resources = new FakeResources();
            target.Add(resources);
            var shield = resources.Add(shieldType, 100f, 100f);
            Use(TestRecipes.Configure(new ShieldAbsorbStep(), ("shield", shieldType), ("absorbShare", 0.5f)));

            Assert.AreEqual(40f, Run(40f, DamageFlags.IgnoreShield).Amount, 0.001f);
            Assert.AreEqual(100f, shield.Current);
            Assert.AreEqual(20f, Run(40f).Amount, 0.001f);
            Assert.AreEqual(80f, shield.Current, 0.001f);
        }
    }
}