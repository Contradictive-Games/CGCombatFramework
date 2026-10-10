using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class RelationConditionTests
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

        [Test]
        public void HostileOnly_PassesEnemiesAlone()
        {
            var hostile = TestRecipes.Relation();
            Assert.IsTrue(Check(hostile, player, enemy));
            Assert.IsFalse(Check(hostile, player, ally));
            Assert.IsFalse(Check(hostile, player, player));
        }

        [Test]
        public void Self_IncludesWhatTheSourceOwns()
        {
            using var turret = new FakeUnit("Turret") { Owner = player, Faction = player.Faction };
            var self = TestRecipes.Relation(hostile: false, self: true);
            Assert.IsTrue(Check(self, player, turret));
            Assert.IsTrue(Check(self, turret, player));
            Assert.IsFalse(Check(self, player, ally));
        }

        [Test]
        public void ASourceWhoseLifeEnded_IsUnknown()
        {
            var ctx = new EffectContext(player, null, enemy);
            player.Despawn();
            Assert.IsFalse(TestRecipes.Relation().Evaluate(in ctx));
            Assert.IsTrue(TestRecipes.Relation(unknown: true).Evaluate(in ctx));
        }
    }

    public class KnockbackTests
    {
        private TestAssets assets;
        private FakeUnit source, target;
        private KnockbackProbe probe;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            source = new FakeUnit("Source");
            target = new FakeUnit("Target");
            target.Transform.position = new Vector3(3f, 0f, 0f);
            probe = new KnockbackProbe();
            target.Add(probe);
        }

        [TearDown]
        public void TearDown()
        {
            source.Dispose();
            target.Dispose();
            assets.Dispose();
        }

        private void Push(KnockbackPayload payload) => payload.Apply(new EffectContext(source, null, target));

        private static void AreClose(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.001f), $"expected {expected}, got {actual}");

        [Test]
        public void PushesAwayFromTheSource()
        {
            Push(TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f))));
            AreClose(new Vector3(8f, 0f, 0f), probe.Last);
        }

        [Test]
        public void Lift_TiltsThePushUpward()
        {
            Push(TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f)), ("lift", 1f)));
            AreClose(new Vector3(1f, 1f, 0f).normalized * 8f, probe.Last);
        }

        [Test]
        public void Resistance_ScalesThePushDown()
        {
            var resistance = assets.Stat("KnockbackResistance");
            target.Stats.GetOrCreate(resistance).SetBaseValue(0.5f);
            Push(TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f)), ("resistance", resistance)));
            AreClose(new Vector3(4f, 0f, 0f), probe.Last);
        }

        [Test]
        public void FullResistance_DoesNotCallTheReceiver()
        {
            var resistance = assets.Stat("KnockbackResistance");
            target.Stats.GetOrCreate(resistance).SetBaseValue(1f);
            Push(TestRecipes.Configure(new KnockbackPayload(), ("resistance", resistance)));
            Assert.AreEqual(0, probe.Count);
        }
    }
}