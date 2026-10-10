using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class DamagePipelineTests
    {
        private sealed class CountingStep : IDamageStep
        {
            public int Count;
            public void Process(ref DamageCalculation calc) => Count++;
        }

        private sealed class FlatStep : IDamageStep
        {
            public float Delta;
            public void Process(ref DamageCalculation calc) => calc.Amount += Delta;
        }

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

        private DamageCalculation Run(float amount, DamageFlags flags = DamageFlags.None)
        {
            var request = new DamageRequest(amount, null, new EffectContext(attacker, null, target), flags);
            return pipeline.Run(in request, target);
        }

        private void AddArmor(float value)
        {
            var armor = assets.Stat("Armor");
            target.Stats.GetOrCreate(armor).SetBaseValue(value);
            var step = new ArmorStep();
            TestAssets.Set(step, "armorStat", armor);
            TestAssets.Set(step, "halfAt", 100f);
            pipeline.Steps.Add(step);
        }

        [Test]
        public void Armor_EqualToHalfAt_HalvesDamage()
        {
            AddArmor(100f);
            Assert.AreEqual(25f, Run(50f).Amount, 0.001f);
        }

        [Test]
        public void IgnoreArmor_SkipsTheArmorStep()
        {
            AddArmor(100f);
            Assert.AreEqual(50f, Run(50f, DamageFlags.IgnoreArmor).Amount, 0.001f);
        }

        [Test]
        public void ImmuneTag_CancelsAndStopsLaterSteps()
        {
            var invulnerable = assets.Tag("State.Invulnerable");
            target.Tags.Add(invulnerable);
            var immune = new ImmuneWithTagStep();
            TestAssets.Set(immune, "tag", invulnerable);
            var after = new CountingStep();
            pipeline.Steps.Add(immune);
            pipeline.Steps.Add(after);

            var calc = Run(50f);
            Assert.AreEqual(HitResult.Immune, calc.Result);
            Assert.AreEqual(0f, calc.Amount);
            Assert.AreEqual(0, after.Count);
        }

        [Test]
        public void Result_NeverGoesNegative()
        {
            pipeline.Steps.Add(new FlatStep { Delta = -100f });
            Assert.AreEqual(0f, Run(30f).Amount);
        }
    }
}