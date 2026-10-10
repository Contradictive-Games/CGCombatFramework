using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class CombatPerformanceTests
    {
        private TestWorld world;

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            world = null;
        }

        /// <summary>One damage request through every built-in step.</summary>
        [Test, Performance]
        public void DamagePipeline_AllTenSteps()
        {
            using var assets = new TestAssets();
            using var attacker = new FakeUnit("Attacker");
            using var target = new FakeUnit("Target");
            var pipeline = TestRecipes.FullPipeline(assets, target);
            var request = new DamageRequest(40f, null, new EffectContext(attacker, null, target), DamageFlags.None);

            Measure.Method(() => pipeline.Run(in request, target))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
        }

        /// <summary>A nova among 200 units: overlap, resolve, sort, filter, about 20 payloads.</summary>
        [Test, Performance]
        public void SphereDelivery_AmongTwoHundredUnits()
        {
            world = new TestWorld();
            var player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
            var enemy = world.CreateTemplate("Enemy", faction: world.Enemies);
            for (int i = 0; i < 200; i++) world.Spawn(enemy, new Vector3((i % 20 - 10) * 2f, 0f, (i / 20 - 5) * 2f));
            world.SyncPhysics();

            var delivery = TestRecipes.Deliver(TestRecipes.Sphere(5f, QueryCenter.Source),
                new IPayload[] { new CountingPayload() }, TestRecipes.Relation());
            var ctx = DeliveryContext.FromUnit(player, null);

            Measure.Method(() => delivery.Emit(in ctx))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(100)
                .GC()
                .Run();
        }

        /// <summary>One tick of 100 projectiles in flight through open space.</summary>
        [Test, Performance]
        public void Projectiles_HundredInFlight()
        {
            world = new TestWorld();
            var player = world.Spawn(world.CreateTemplate("Player"));
            world.SyncPhysics();
            var gun = TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 20f, new CountingEmission());
            TestAssets.Set(gun, "count", 100);
            TestAssets.Set(gun, "spreadDegrees", 45f);
            TestAssets.Set(gun, "lifetime", 60f);
            ProjectilePool.Prewarm(TestRecipes.PrefabOf(gun), 100);
            var ctx = DeliveryContext.FromUnit(player, null);

            Measure.Method(() => world.Ticks.Step(1f / 60f))
                .SetUp(() => gun.Emit(in ctx))
                .CleanUp(ProjectilePool.ReleaseAll)
                .WarmupCount(3)
                .MeasurementCount(20)
                .GC()
                .Run();
        }
    }
}