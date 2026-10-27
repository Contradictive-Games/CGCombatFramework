using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class ProjectileFlightTests
    {
        private TestWorld world;
        private Unit enemyTemplate;
        private IUnit player;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            enemyTemplate = world.CreateTemplate("Enemy", faction: world.Enemies);
            player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
        }

        [TearDown] public void TearDown() => world.Dispose();

        private IUnit Enemy(float z) => world.Spawn(enemyTemplate, new Vector3(0f, 0f, z));
        private static float Health(IUnit unit) => TestWorld.HealthOf(unit);
        private static Delivery Hurt(float amount) => TestRecipes.Deliver(new UnitQuery(), new IPayload[] { TestRecipes.Damage(amount) });

        private void Fire(ProjectileEmission gun, float seconds)
        {
            gun.Emit(DeliveryContext.FromUnit(player, null));
            world.Ticks.Run(seconds, 1f / 60f);
        }

        [Test]
        public void HitsTheNearestUnitFirst() // regression: Projectile.Tick sorts its hits
        {
            var far = Enemy(5f);
            var near = Enemy(4f);
            world.SyncPhysics();

            Fire(TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 360f, Hurt(40f)), 0.5f); // 6 m per step: the first step's ray crosses both

            Assert.AreEqual(60f, Health(near), 0.01f);
            Assert.AreEqual(100f, Health(far), 0.01f);
        }

        [Test]
        public void StopsAtWalls()
        {
            var behindWall = Enemy(4f);
            world.Wall(new Vector3(0f, 0f, 2f), new Vector3(4f, 4f, 0.2f));
            world.SyncPhysics();

            Fire(TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 30f, Hurt(40f)), 1f);

            Assert.AreEqual(100f, Health(behindWall), 0.01f);
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void LastPierceOnAUnit_RunsOnStop_WhenAsked(bool stopAfterLastUnit, int expectedStops)
        {
            Enemy(4f);
            world.SyncPhysics();
            var stops = new CountingEmission();
            var gun = TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 30f, Hurt(10f));
            TestAssets.Set(gun, "onStop", new IEmission[] { stops });
            TestAssets.Set(gun, "stopAfterLastUnit", stopAfterLastUnit);

            Fire(gun, 0.5f);
            Assert.AreEqual(expectedStops, stops.Count);
        }

        [Test]
        public void Bounce_ComesBackOffAWall()
        {
            var behindPlayer = Enemy(-3f);
            world.Wall(new Vector3(0f, 0f, 3f), new Vector3(6f, 4f, 0.2f));
            world.SyncPhysics();
            var gun = TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 30f, Hurt(40f));
            TestAssets.Set(gun, "bounces", 1);
            TestAssets.Set(gun, "bounceSpeedKept", 1f);

            Fire(gun, 1f);
            Assert.AreEqual(60f, Health(behindPlayer), 0.01f);
        }

        [Test]
        public void Arc_FliesOverWhatsBetween_AndLandsOnTheAimPoint()
        {
            var between = Enemy(5f);
            var atAimPoint = Enemy(10f); // FromUnit with no aim provider aims 10 m ahead
            world.SyncPhysics();
            var gun = TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 10f, Hurt(40f));
            TestAssets.Set(gun, "gravity", 20f);
            TestAssets.Set(gun, "arcToAimPoint", true);

            Fire(gun, 1.5f);
            Assert.AreEqual(100f, Health(between), 0.01f, "the arc peaks at 2.5 m over it");
            Assert.AreEqual(60f, Health(atAimPoint), 0.01f);
        }
    }
}
