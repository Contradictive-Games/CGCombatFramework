using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class PhysicsQueryTests
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
        private IUnit Ally(float x, float z) => world.Spawn(world.CreateTemplate("Ally", faction: world.Players), new Vector3(x, 0f, z));
        private static float Health(IUnit unit) => TestWorld.HealthOf(unit);
        private void Fire(IEmission emission) => emission.Emit(DeliveryContext.FromUnit(player, null));
        private void Strike(IHitQuery query) => Fire(TestRecipes.Deliver(query, new IPayload[] { TestRecipes.Damage(10f) }));

        [Test]
        public void Sphere_HitsHostilesInRange_AndNobodyElse()
        {
            var near = Enemy(2f, 0f);
            var edge = Enemy(0f, 4.3f); // collider edge at 3.8, inside a radius of 4
            var far = Enemy(10f, 0f);
            var ally = Ally(-1f, 0f);
            world.SyncPhysics();

            Fire(TestRecipes.Deliver(TestRecipes.Sphere(4f, QueryCenter.Source),
                new IPayload[] { TestRecipes.Damage(30f) }, TestRecipes.Relation()));

            Assert.AreEqual(70f, Health(near), 0.01f);
            Assert.AreEqual(70f, Health(edge), 0.01f);
            Assert.AreEqual(100f, Health(far), 0.01f, "out of range");
            Assert.AreEqual(100f, Health(ally), 0.01f, "filtered by relation");
            Assert.AreEqual(100f, Health(player), 0.01f, "the source is excluded");
        }

        [Test]
        public void Sphere_MaxTargets_TakesTheNearest()
        {
            var third = Enemy(3f, 0f);
            var first = Enemy(1f, 0f);
            var fourth = Enemy(0f, 3.5f);
            var second = Enemy(0f, -2f);
            world.SyncPhysics();

            var delivery = TestRecipes.Deliver(TestRecipes.Sphere(5f, QueryCenter.Source), new IPayload[] { TestRecipes.Damage(10f) });
            TestAssets.Set(delivery, "maxTargets", 2);
            Fire(delivery);

            Assert.AreEqual(90f, Health(first), 0.01f);
            Assert.AreEqual(90f, Health(second), 0.01f);
            Assert.AreEqual(100f, Health(third), 0.01f);
            Assert.AreEqual(100f, Health(fourth), 0.01f);
        }

        [Test]
        public void Raycast_WithoutPierce_HitsOnlyTheNearest() // regression: results must be sorted by distance
        {
            var far = Enemy(0f, 6f); // spawned first, so unsorted results would likely list it first
            var near = Enemy(0f, 3f);
            world.SyncPhysics();

            Fire(TestRecipes.Deliver(TestRecipes.Raycast(20f), new IPayload[] { TestRecipes.Damage(25f) }));

            Assert.AreEqual(75f, Health(near), 0.01f);
            Assert.AreEqual(100f, Health(far), 0.01f);
        }

        [Test]
        public void Raycast_StopsAtAWall_EvenWithPierce()
        {
            var near = Enemy(0f, 3f);
            var behindWall = Enemy(0f, 6f);
            world.Wall(new Vector3(0f, 0f, 4.5f), new Vector3(4f, 4f, 0.2f));
            world.SyncPhysics();

            Fire(TestRecipes.Deliver(TestRecipes.Raycast(20f, pierce: 5), new IPayload[] { TestRecipes.Damage(25f) }));

            Assert.AreEqual(75f, Health(near), 0.01f);
            Assert.AreEqual(100f, Health(behindWall), 0.01f);
        }

        [Test]
        public void Headshot_GoesFromRayToHitboxToPipeline()
        {
            var pipeline = world.Assets.Make<DamagePipeline>();
            pipeline.Steps = new IDamageStep[] { new HitboxMultiplierStep() };
            var template = world.CreateTemplate("Enemy With Head", faction: world.Enemies, pipeline: pipeline);
            var head = new GameObject("Head");
            head.transform.SetParent(template.transform, false);
            head.transform.localPosition = new Vector3(0f, 1f, 0f);
            head.AddComponent<SphereCollider>().radius = 0.3f;
            head.AddComponent<Hitbox>().DamageMultiplier = 2f; // finds its Unit in Awake, on the spawned clone

            var target = world.Spawn(template, new Vector3(0f, 0f, 5f));
            player.Transform.position = new Vector3(0f, 1f, 0f); // eye level with the head, above the body collider
            world.SyncPhysics();

            Fire(TestRecipes.Deliver(TestRecipes.Raycast(20f), new IPayload[] { TestRecipes.Damage(30f) }));

            Assert.AreEqual(40f, Health(target), 0.01f, "30 damage doubled by the head hitbox");
        }

        [Test]
        public void Cone_HitsWhatsInsideItsAngle()
        {
            var front = Enemy(0f, 2f);
            var side = Enemy(2f, 0f);
            var behind = Enemy(0f, -2f);
            world.SyncPhysics();

            Strike(TestRecipes.Configure(new ConeQuery(), ("angle", 60f), ("range", 3f)));

            Assert.AreEqual(90f, Health(front), 0.01f);
            Assert.AreEqual(100f, Health(side), 0.01f);
            Assert.AreEqual(100f, Health(behind), 0.01f);
        }

        [Test]
        public void Box_HitsWhatsInFront_WithinItsWidth()
        {
            var front = Enemy(0f, 4f);
            var wide = Enemy(3f, 4f);
            var behind = Enemy(0f, -3f);
            world.SyncPhysics();

            Strike(TestRecipes.Configure(new BoxQuery(), ("length", 6f), ("width", 2f)));

            Assert.AreEqual(90f, Health(front), 0.01f);
            Assert.AreEqual(100f, Health(wide), 0.01f);
            Assert.AreEqual(100f, Health(behind), 0.01f);
        }

        [Test]
        public void Chain_JumpsToTheNearestHostile_SkippingAllies()
        {
            var a = Enemy(3f, 0f);
            var b = Enemy(6f, 0f);
            var c = Enemy(9f, 0f);
            var far = Enemy(20f, 0f);
            var ally = Ally(4.5f, 1f); // nearer to a than b is
            world.SyncPhysics();

            Strike(TestRecipes.Configure(new ChainQuery(), ("startAtTarget", false), ("firstRange", 5f), ("jumps", 3), ("jumpRange", 4f)));

            Assert.AreEqual(90f, Health(a), 0.01f);
            Assert.AreEqual(90f, Health(b), 0.01f);
            Assert.AreEqual(90f, Health(c), 0.01f);
            Assert.AreEqual(100f, Health(far), 0.01f, "out of jump range");
            Assert.AreEqual(100f, Health(ally), 0.01f, "the chain may only land on hostiles");
        }

        [Test]
        public void Everyone_ReachesUnitsAnywhere()
        {
            var a = Enemy(50f, 0f);
            var b = Enemy(-80f, 30f);
            Strike(new EveryoneQuery()); // no physics sync needed

            Assert.AreEqual(90f, Health(a), 0.01f);
            Assert.AreEqual(90f, Health(b), 0.01f);
            Assert.AreEqual(100f, Health(player), 0.01f, "the source is excluded");
        }
    }
}
