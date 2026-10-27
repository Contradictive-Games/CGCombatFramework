using NUnit.Framework;
using UnityEngine;

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

        private void Use(params IDamageStep[] steps) => pipeline.Steps = steps;

        private DamageCalculation Run(float amount, DamageFlags flags = DamageFlags.None, Hitbox hitbox = null, DamageType type = null, IUnit from = null)
        {
            var hit = new HitData(Vector3.zero, Vector3.up, Vector3.forward, hitbox);
            var ctx = new EffectContext(from ?? attacker, null, target, in hit, random: new SeededGameRandom(1));
            return pipeline.Run(new DamageRequest(amount, type, in ctx, flags), target);
        }

        private StatType TargetStat(string name, float value)
        {
            var stat = assets.Stat(name);
            target.Stats.GetOrCreate(stat).SetBaseValue(value);
            return stat;
        }

        private StatType AttackerStat(string name, float value)
        {
            var stat = assets.Stat(name);
            attacker.Stats.GetOrCreate(stat).SetBaseValue(value);
            return stat;
        }

        private ShieldAbsorbStep Shield(ResourceType shieldType, params (string field, object value)[] settings)
        {
            var step = TestRecipes.Configure(new ShieldAbsorbStep(), ("shield", shieldType));
            foreach (var (field, value) in settings) TestAssets.Set(step, field, value);
            return step;
        }

        [Test]
        public void ACancelledHit_StopsTheStepsAfterIt()
        {
            var invulnerable = assets.Tag("State.Invulnerable");
            target.Tags.Add(invulnerable);
            var after = new CountingStep();
            Use(TestRecipes.Configure(new ImmuneWithTagStep(), ("tag", invulnerable)), after);

            var calc = Run(50f);
            Assert.AreEqual(HitResult.Immune, calc.Result);
            Assert.AreEqual(0f, calc.Amount);
            Assert.AreEqual(0, after.Count);
        }

        [Test]
        public void Result_NeverGoesNegative()
        {
            Use(new FlatStep { Delta = -100f });
            Assert.AreEqual(0f, Run(30f).Amount);
        }

        [Test]
        public void Armor_EqualToHalfAt_HalvesDamage_UnlessIgnored()
        {
            Use(TestRecipes.Configure(new ArmorStep(), ("armorStat", TargetStat("Armor", 100f)), ("halfAt", 100f)));
            Assert.AreEqual(25f, Run(50f).Amount, 0.001f);
            Assert.AreEqual(50f, Run(50f, DamageFlags.IgnoreArmor).Amount, 0.001f);
        }

        [Test]
        public void Immune_ToTheDamageTypesImmunityTag()
        {
            var fireproof = assets.Tag("State.Fireproof");
            target.Tags.Add(fireproof);
            var fire = assets.Make<DamageType>("Fire");
            fire.ImmunityTag = fireproof;
            Use(TestRecipes.Configure(new ImmuneWithTagStep(), ("tag", assets.Tag("State.Invulnerable"))));

            Assert.AreEqual(HitResult.Immune, Run(50f, type: fire).Result);
            Assert.AreEqual(HitResult.Hit, Run(50f).Result, "untyped damage still lands");
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
        public void Crit_MultipliesByTheCritDamageStat_OrTheDefault()
        {
            var chance = AttackerStat("CritChance", 1f);
            Use(TestRecipes.Configure(new CritStrikeStep(), ("critChanceStat", chance), ("critDamageStat", AttackerStat("CritDamage", 2.5f))));
            var calc = Run(40f);
            Assert.IsTrue(calc.IsCrit);
            Assert.AreEqual(100f, calc.Amount, 0.001f);

            Use(TestRecipes.Configure(new CritStrikeStep(), ("critChanceStat", chance)));
            Assert.AreEqual(60f, Run(40f).Amount, 0.001f, "no crit damage stat: Default Crit Damage 1.5");
        }

        [Test]
        public void Crit_IsSkippedByCannotCrit_AndTheNoCritTag()
        {
            var sturdy = assets.Tag("State.Sturdy");
            Use(TestRecipes.Configure(new CritStrikeStep(), ("critChanceStat", AttackerStat("CritChance", 1f)), ("noCritTag", sturdy)));
            Assert.IsFalse(Run(40f, DamageFlags.CannotCrit).IsCrit);
            target.Tags.Add(sturdy);
            var calc = Run(40f);
            Assert.IsFalse(calc.IsCrit);
            Assert.AreEqual(40f, calc.Amount);
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
        public void Resistance_UsesTheDamageTypesStat_UpToTheCap()
        {
            var fire = assets.Make<DamageType>("Fire");
            fire.ResistanceStat = TargetStat("FireResistance", 0.5f);
            var frost = assets.Make<DamageType>("Frost");
            frost.ResistanceStat = TargetStat("FrostResistance", 2f);
            Use(new DamageResistanceStep());

            Assert.AreEqual(10f, Run(20f, type: fire).Amount, 0.001f);
            Assert.AreEqual(2f, Run(20f, type: frost).Amount, 0.001f, "capped at 90%");
            Assert.AreEqual(20f, Run(20f).Amount, 0.001f, "untyped damage has no resistance");
        }

        [Test]
        public void FriendlyFire_CancelsBetweenFriends_AndSelfDamageFollowsTheSetting()
        {
            var players = assets.Make<Faction>("Players");
            var enemies = assets.Make<Faction>("Enemies");
            enemies.HostileTo = new[] { players };
            target.Faction = players;
            Use(new FriendlyFireStep());

            attacker.Faction = players;
            Assert.AreEqual(HitResult.Immune, Run(10f).Result, "friendly");
            attacker.Faction = enemies;
            Assert.AreEqual(HitResult.Hit, Run(10f).Result, "hostile");
            Assert.AreEqual(HitResult.Hit, Run(10f, from: target).Result, "self damage is allowed by default");

            Use(TestRecipes.Configure(new FriendlyFireStep(), ("allowSelfDamage", false)));
            Assert.AreEqual(HitResult.Immune, Run(10f, from: target).Result);
        }

        [Test]
        public void FlatReduction_Subtracts_ButNotFromDamageOverTime()
        {
            Use(TestRecipes.Configure(new FlatReductionStep(), ("reduction", TargetStat("Toughness", 5f))));
            Assert.AreEqual(15f, Run(20f).Amount, 0.001f);
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
            Use(Shield(shieldType));

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
            Use(Shield(shieldType, ("absorbShare", 0.5f)));

            Assert.AreEqual(40f, Run(40f, DamageFlags.IgnoreShield).Amount, 0.001f);
            Assert.AreEqual(100f, shield.Current);
            Assert.AreEqual(20f, Run(40f).Amount, 0.001f);
            Assert.AreEqual(80f, shield.Current, 0.001f);
        }

        // Regression: hits on an empty shield used to leave its Delay After Loss counting from the break.
        [TestCase(false, 0f)]
        [TestCase(true, 75f)]
        public void ShieldAbsorb_HitsOnAnEmptyShield_RestartItsDelay(bool delayFromBreakOnly, float shieldAfter)
        {
            var shieldType = assets.Make<ResourceType>("Shield");
            var resources = new FakeResources();
            target.Add(resources);
            var recharge = TestRecipes.Configure(new DelayAfterLoss(), ("delay", 2f), ("then", TestRecipes.Configure(new FlatRegen(), ("perSecond", 50f))));
            var shield = resources.Add(shieldType, 100f, 100f, recharge);
            Use(Shield(shieldType, ("delayFromBreakOnly", delayFromBreakOnly)));

            Run(100f);
            shield.Tick(1.5f);
            Assert.AreEqual(10f, Run(10f).Amount, 0.001f, "an empty shield takes nothing");
            shield.Tick(1.5f); // 3s since the break, 1.5s since the last hit
            Assert.AreEqual(shieldAfter, shield.Current, 0.001f);
        }
    }
}
