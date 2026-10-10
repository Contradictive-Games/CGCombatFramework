using NUnit.Framework;

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
        public void EndSession_DespawnsEverything()
        {
            var template = world.CreateTemplate();
            for (int i = 0; i < 5; i++) world.Spawn(template);
            CombatServices.EndSession();
            Assert.AreEqual(0, CombatServices.Units.Count);
            Assert.AreEqual(0, world.Ticks.Count, "despawned units stop ticking");
        }
    }
}