using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class BurstFireTests
    {
        private TestAssets assets;
        private FakeUnit owner;
        private CountingEmission shots;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            owner = new FakeUnit("Owner");
            shots = new CountingEmission();
        }

        [TearDown]
        public void TearDown()
        {
            owner.Dispose();
            assets.Dispose();
        }

        private Firable Make(int shotCount, float interval, params IFireGate[] gates)
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Mode = TestRecipes.Configure(new BurstFire(), ("shots", shotCount), ("interval", interval));
            definition.Gates = gates;
            definition.Emissions = new IEmission[] { shots };
            return new Firable(definition, owner);
        }

        [Test]
        public void Shots_AreSpacedByTheInterval()
        {
            var firable = Make(3, 0.25f);
            firable.Press();
            Assert.AreEqual(1, shots.Count, "first shot on press");
            firable.Tick(0.125f);
            Assert.AreEqual(1, shots.Count, "half an interval: nothing yet");
            firable.Tick(0.125f);
            Assert.AreEqual(2, shots.Count);
            firable.Tick(0.25f);
            Assert.AreEqual(3, shots.Count);
            firable.Tick(1f);
            Assert.AreEqual(3, shots.Count, "the burst is over");
            Assert.AreEqual(FiringPhase.Idle, firable.Phase);
        }

        [Test]
        public void ABurst_PaysItsGatesOnce()
        {
            var firable = Make(3, 0.25f, TestRecipes.Ammo(10));
            firable.Press();
            firable.Tick(1f);
            Assert.IsTrue(firable.TryGetGate<IReloadable>(out var magazine));
            Assert.AreEqual(3, shots.Count);
            Assert.AreEqual(9, magazine.Current);
        }

        [Test]
        public void ALongFrame_FiresTheShotsItOwesTogether()
        {
            var firable = Make(3, 0.25f);
            firable.Press();
            firable.Tick(0.6f); // more than two intervals in one update
            Assert.AreEqual(3, shots.Count);
        }
    }
}