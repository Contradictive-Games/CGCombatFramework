using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class NewQueryTests
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

        private IUnit Enemy(float x, float z) => world.Spawn(enemyTemplate, new Vector3(x, 0f, z));
        private static float Health(IUnit unit) => TestWorld.HealthOf(unit);
        private void Fire(IHitQuery query) =>
            TestRecipes.Deliver(query, new IPayload[] { TestRecipes.Damage(10f) }).Emit(DeliveryContext.FromUnit(player, null));

        [Test]
        public void Chain_JumpsToTheNearestHostile_SkippingAllies()
        {
            var a = Enemy(3f, 0f);
            var b = Enemy(6f, 0f);
            var c = Enemy(9f, 0f);
            var far = Enemy(20f, 0f);
            var ally = world.Spawn(world.CreateTemplate("Ally", faction: world.Players), new Vector3(4.5f, 0f, 1f)); // nearer to a than b is
            world.SyncPhysics();

            Fire(TestRecipes.Configure(new ChainQuery(), ("startAtTarget", false), ("firstRange", 5f), ("jumps", 3), ("jumpRange", 4f)));

            Assert.AreEqual(90f, Health(a), 0.01f);
            Assert.AreEqual(90f, Health(b), 0.01f);
            Assert.AreEqual(90f, Health(c), 0.01f);
            Assert.AreEqual(100f, Health(far), 0.01f, "out of jump range");
            Assert.AreEqual(100f, Health(ally), 0.01f, "the chain may only land on hostiles");
        }

        [Test]
        public void Box_HitsWhatsInFront_WithinItsWidth()
        {
            var front = Enemy(0f, 4f);
            var wide = Enemy(3f, 4f);
            var behind = Enemy(0f, -3f);
            world.SyncPhysics();

            Fire(TestRecipes.Configure(new BoxQuery(), ("length", 6f), ("width", 2f)));

            Assert.AreEqual(90f, Health(front), 0.01f);
            Assert.AreEqual(100f, Health(wide), 0.01f);
            Assert.AreEqual(100f, Health(behind), 0.01f);
        }

        [Test]
        public void Everyone_ReachesUnitsAnywhere()
        {
            var a = Enemy(50f, 0f);
            var b = Enemy(-80f, 30f);
            Fire(new EveryoneQuery()); // no physics sync needed

            Assert.AreEqual(90f, Health(a), 0.01f);
            Assert.AreEqual(90f, Health(b), 0.01f);
            Assert.AreEqual(100f, Health(player), 0.01f, "the source is excluded");
        }
    }
}