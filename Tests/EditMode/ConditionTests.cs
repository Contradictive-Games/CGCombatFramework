using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class ConditionTests
    {
        private TestAssets assets;
        private FakeUnit player, ally, enemy;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            var players = assets.Make<Faction>("Players");
            var enemies = assets.Make<Faction>("Enemies");
            enemies.HostileTo = new[] { players };
            player = new FakeUnit("Player") { Faction = players };
            ally = new FakeUnit("Ally") { Faction = players };
            enemy = new FakeUnit("Enemy") { Faction = enemies };
        }

        [TearDown]
        public void TearDown()
        {
            player.Dispose();
            ally.Dispose();
            enemy.Dispose();
            assets.Dispose();
        }

        private static bool Check(ICondition condition, IUnit source, IUnit target) =>
            condition.Evaluate(new EffectContext(source, null, target));

        private bool Check(ICondition condition) => Check(condition, player, enemy);

        private static ICondition[] Fixed(params bool[] results)
        {
            var conditions = new ICondition[results.Length];
            for (int i = 0; i < results.Length; i++) conditions[i] = new FixedCondition(results[i]);
            return conditions;
        }

        [Test]
        public void Relation_HostileOnly_PassesEnemiesAlone()
        {
            var hostile = TestRecipes.Relation();
            Assert.IsTrue(Check(hostile, player, enemy));
            Assert.IsFalse(Check(hostile, player, ally));
            Assert.IsFalse(Check(hostile, player, player));
        }

        [Test]
        public void Relation_Self_IncludesWhatTheSourceOwns()
        {
            using var turret = new FakeUnit("Turret") { Owner = player, Faction = player.Faction };
            var self = TestRecipes.Relation(hostile: false, self: true);
            Assert.IsTrue(Check(self, player, turret));
            Assert.IsTrue(Check(self, turret, player));
            Assert.IsFalse(Check(self, player, ally));
        }

        [Test]
        public void Relation_ASourceWhoseLifeEnded_IsUnknown()
        {
            var ctx = new EffectContext(player, null, enemy);
            player.Despawn();
            Assert.IsFalse(TestRecipes.Relation().Evaluate(in ctx));
            Assert.IsTrue(TestRecipes.Relation(unknown: true).Evaluate(in ctx));
        }

        [Test]
        public void AllOf_NeedsEveryCondition_AndAnEmptyListPasses()
        {
            Assert.IsTrue(Check(TestRecipes.Configure(new AllOfCondition(), ("conditions", Fixed()))));
            Assert.IsTrue(Check(TestRecipes.Configure(new AllOfCondition(), ("conditions", Fixed(true, true)))));
            Assert.IsFalse(Check(TestRecipes.Configure(new AllOfCondition(), ("conditions", Fixed(true, false)))));
        }

        [Test]
        public void AnyOf_NeedsOneCondition_AndAnEmptyListPasses()
        {
            Assert.IsTrue(Check(TestRecipes.Configure(new AnyOfCondition(), ("conditions", Fixed()))));
            Assert.IsTrue(Check(TestRecipes.Configure(new AnyOfCondition(), ("conditions", Fixed(false, true)))));
            Assert.IsFalse(Check(TestRecipes.Configure(new AnyOfCondition(), ("conditions", Fixed(false, false)))));
        }

        [Test]
        public void Chance_PassesAboutAsOftenAsItsChance()
        {
            int Passes(float chance)
            {
                var condition = TestRecipes.Configure(new ChanceCondition(), ("chance", chance));
                var random = new SeededGameRandom(7);
                int passed = 0;
                for (int i = 0; i < 1000; i++)
                    if (condition.Evaluate(new EffectContext(player, null, enemy, default, random: random))) passed++;
                return passed;
            }

            Assert.AreEqual(0, Passes(0f));
            Assert.AreEqual(1000, Passes(1f));
            Assert.That(Passes(0.25f), Is.InRange(200, 300));
        }

        [Test]
        public void Distance_PassesBetweenMinAndMax()
        {
            enemy.Transform.position = new Vector3(5f, 0f, 0f);
            Assert.IsTrue(Check(TestRecipes.Configure(new DistanceCondition(), ("minDistance", 2f), ("maxDistance", 6f))));
            Assert.IsFalse(Check(TestRecipes.Configure(new DistanceCondition(), ("minDistance", 6f), ("maxDistance", 10f))));
            Assert.IsFalse(Check(TestRecipes.Configure(new DistanceCondition(), ("maxDistance", 4f))));
        }

        [Test]
        public void HitZone_MatchesTheHitboxZoneAndItsParents()
        {
            var body = assets.Tag("Zone.Body");
            var head = assets.Tag("Zone.Body.Head", body);
            var legs = assets.Tag("Zone.Body.Legs", body);
            var hitbox = new GameObject("Head").AddComponent<Hitbox>();
            hitbox.Zone = head;
            try
            {
                var onHead = new EffectContext(player, null, enemy, new HitData(Vector3.zero, Vector3.up, Vector3.forward, hitbox));
                Assert.IsTrue(TestRecipes.Configure(new HitZoneCondition(), ("zoneTag", head)).Evaluate(in onHead));
                Assert.IsTrue(TestRecipes.Configure(new HitZoneCondition(), ("zoneTag", body)).Evaluate(in onHead));
                Assert.IsFalse(TestRecipes.Configure(new HitZoneCondition(), ("zoneTag", legs)).Evaluate(in onHead));
                Assert.IsFalse(Check(TestRecipes.Configure(new HitZoneCondition(), ("zoneTag", body))), "no hitbox, no zone");
            }
            finally { Object.DestroyImmediate(hitbox.gameObject); }
        }

        [Test]
        public void IsAlive_FailsForTheDeadAndTheDespawned()
        {
            var health = new FakeHealth();
            enemy.Add(health);
            var alive = new IsAliveCondition();
            Assert.IsTrue(Check(alive));

            health.Kill(default);
            Assert.IsFalse(Check(alive), "dead");
            health.Revive();
            enemy.Despawn();
            Assert.IsFalse(alive.Evaluate(new EffectContext(player, null, enemy)), "despawned");
        }

        [Test]
        public void ProcDepth_PassesOnlyBelowItsLimit()
        {
            var directOnly = new ProcDepthCondition();
            Assert.IsTrue(directOnly.Evaluate(new EffectContext(player, null, enemy, default, procDepth: 0)));
            Assert.IsFalse(directOnly.Evaluate(new EffectContext(player, null, enemy, default, procDepth: 1)));
        }

        [Test]
        public void StatCompare_ChecksTheSubjectsStat()
        {
            var combo = assets.Stat("Combo");
            player.Stats.GetOrCreate(combo).SetBaseValue(5f);
            Assert.IsTrue(Check(TestRecipes.Configure(new StatCompareCondition(), ("stat", combo), ("value", 5f))));
            Assert.IsFalse(Check(TestRecipes.Configure(new StatCompareCondition(), ("stat", combo), ("value", 6f))));
            Assert.IsTrue(Check(TestRecipes.Configure(new StatCompareCondition(), ("stat", combo), ("value", 6f), ("comparison", ThresholdComparison.Below))));
        }

        [Test]
        public void ResourceThreshold_ComparesAgainstAFractionOfMax()
        {
            var health = assets.Make<ResourceType>("Health");
            var resources = new FakeResources();
            enemy.Add(resources);
            resources.Add(health, 100f, 30f);
            Assert.IsFalse(Check(TestRecipes.Configure(new ResourceThresholdCondition(), ("resource", health), ("fraction", 0.3f))), "30% is not below 30%");
            Assert.IsTrue(Check(TestRecipes.Configure(new ResourceThresholdCondition(), ("resource", health), ("fraction", 0.3f), ("comparison", ThresholdComparison.AtOrAbove))));
        }

        [Test]
        public void HasStatusEffect_ChecksMinimumStacks()
        {
            var effects = enemy.AddUnitComponent<StatusEffectsComponent>();
            var bleed = assets.Effect("Bleed", maxStacks: 5);
            var condition = TestRecipes.Configure(new HasStatusEffectCondition(), ("effect", bleed), ("minStacks", 3));

            effects.Apply(bleed, player, 2);
            Assert.IsFalse(Check(condition));
            effects.Apply(bleed, player);
            Assert.IsTrue(Check(condition));
        }
    }
}
