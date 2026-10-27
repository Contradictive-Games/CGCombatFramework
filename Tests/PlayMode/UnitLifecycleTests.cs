using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class UnitLifecycleTests
    {
        private TestWorld world;

        [SetUp] public void SetUp() => world = new TestWorld();
        [TearDown] public void TearDown() => world.Dispose();

        [Test]
        public void Spawn_AssignsAnId_AndRegisters()
        {
            var unit = world.Spawn(world.CreateTemplate());
            Assert.IsTrue(unit.Id.IsValid);
            Assert.IsTrue(CombatServices.Units.TryGet(unit.Id, out var found));
            Assert.AreSame(unit, found);
            Assert.IsTrue(unit.IsAlive);
        }

        [Test]
        public void Despawn_InvalidatesHandles_AndTheReusedObjectGetsANewId()
        {
            var template = world.CreateTemplate();
            var first = world.Spawn(template);
            var handle = first.ToHandle();
            var firstId = first.Id;
            world.Spawner.Despawn(first, DespawnReason.Killed);
            Assert.IsFalse(handle.IsValid);

            var second = world.Spawn(template);
            Assert.AreSame(first, second, "the pool should hand back the same object");
            Assert.AreNotEqual(firstId, second.Id);
            Assert.IsFalse(handle.IsValid, "an old handle must never resolve to the next life");
        }

        [Test]
        public void Despawn_ClearsTagsAndEffects_ForTheNextLife()
        {
            var stunned = world.Assets.Tag("State.Stunned");
            var stun = world.Assets.Effect("Stun", duration: 5f);
            stun.GrantedTags = new[] { stunned };
            var template = world.CreateTemplate();
            var unit = world.Spawn(template);
            unit.TryGet<IStatusEffects>(out var effects);
            effects.Apply(stun, unit);

            world.Spawner.Despawn(unit, DespawnReason.Recycled);
            var again = world.Spawn(template);
            Assert.AreSame(unit, again);
            Assert.IsFalse(again.Tags.Has(stunned));
            Assert.AreEqual(0, effects.Active.Count);
        }

        [Test]
        public void EndSession_DespawnsEverything()
        {
            var template = world.CreateTemplate();
            for (int i = 0; i < 5; i++) world.Spawn(template);
            CombatServices.EndSession();
            Assert.AreEqual(0, CombatServices.Units.Count);
            Assert.AreEqual(0, world.Ticks.Count, "despawned units stop ticking");
        }

        [Test]
        public void DespawnPayload_SendsTheSubjectBackToItsPool()
        {
            var template = world.CreateTemplate();
            var totem = world.Spawn(template);
            new DespawnPayload().Apply(new EffectContext(null, null, totem));
            Assert.IsFalse(totem.IsSpawned);
            Assert.AreEqual(0, CombatServices.Units.Count);
            Assert.AreSame(totem, world.Spawn(template), "pooled, not destroyed");
        }

        [Test]
        public void SpawnUnitPayload_SpawnsAtTheTarget_OwnedByTheSource()
        {
            var player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
            var enemy = world.Spawn(world.CreateTemplate("Enemy", faction: world.Enemies), new Vector3(4f, 0f, 2f));
            var turret = world.CreateTemplate("Turret");
            TestRecipes.Configure(new SpawnUnitPayload(), ("prefab", turret)).Apply(new EffectContext(player, null, enemy));

            IUnit spawned = null;
            foreach (var unit in CombatServices.Units.All)
                if (!ReferenceEquals(unit, player) && !ReferenceEquals(unit, enemy)) spawned = unit;
            Assert.IsNotNull(spawned, "nothing was spawned");
            Assert.That(Vector3.Distance(new Vector3(4f, 0f, 2f), spawned.Transform.position), Is.LessThan(0.001f));
            Assert.AreSame(player, spawned.Owner);
            Assert.AreSame(world.Players, spawned.Faction);
        }
    }
}
