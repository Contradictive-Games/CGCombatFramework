using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace CG.CombatFramework.Tests
{
    public class PhysicsAllocationTests
    {
        private TestWorld world;
        private IUnit player;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
            var enemy = world.CreateTemplate("Enemy", faction: world.Enemies);
            for (int i = 0; i < 20; i++) world.Spawn(enemy, new Vector3(i % 5 - 2f, 0f, i / 5 + 2f));
            world.SyncPhysics();
        }

        [TearDown] public void TearDown() => world.Dispose();

        [Test]
        public void SphereDelivery_Emit()
        {
            var delivery = TestRecipes.Deliver(TestRecipes.Sphere(6f, QueryCenter.Source),
                new IPayload[] { new CountingPayload() }, TestRecipes.Relation());
            var ctx = DeliveryContext.FromUnit(player, null);
            delivery.Emit(in ctx);
            Assert.That(() => delivery.Emit(in ctx), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ProjectileFlight_Step()
        {
            var gun = TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 5f, new CountingEmission());
            TestAssets.Set(gun, "count", 50);
            TestAssets.Set(gun, "spreadDegrees", 45f);
            ProjectilePool.Prewarm(TestRecipes.PrefabOf(gun), 50);
            gun.Emit(DeliveryContext.FromUnit(player, null));
            world.Ticks.Step(1f / 60f);
            Assert.That(() => world.Ticks.Step(1f / 60f), Is.Not.AllocatingGCMemory());
        }
    }
}