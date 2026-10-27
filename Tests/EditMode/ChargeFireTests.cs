using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class ChargeFireTests
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

        private Firable Make(bool fireWhenFull = true, float minimumCharge = 0.2f, params IFireGate[] gates)
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Mode = TestRecipes.Configure(new ChargeFire(), ("maxChargeTime", 1f), ("minimumCharge", minimumCharge), ("fireWhenFull", fireWhenFull));
            definition.Gates = gates;
            definition.Emissions = new IEmission[] { shots };
            return new Firable(definition, owner);
        }

        [Test]
        public void Release_FiresWithPowerEqualToTheCharge()
        {
            var firable = Make();
            firable.Press();
            Assert.AreEqual(FiringPhase.Charging, firable.Phase);
            firable.Tick(0.5f);
            firable.Release();
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(0.5f, shots.LastPower, 0.001f);
            Assert.AreEqual(FiringPhase.Idle, firable.Phase);
        }

        [Test]
        public void Release_BelowTheMinimum_Cancels()
        {
            var firable = Make();
            firable.Press();
            firable.Tick(0.1f);
            firable.Release();
            Assert.AreEqual(0, shots.Count);
            Assert.AreEqual(FiringPhase.Idle, firable.Phase);
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void AFullCharge_FiresByItself_WhenAsked(bool fireWhenFull, int expectedShots)
        {
            var firable = Make(fireWhenFull);
            firable.Press();
            firable.Tick(2f);
            Assert.AreEqual(expectedShots, shots.Count);
            if (fireWhenFull) return;

            Assert.AreEqual(1f, firable.Progress01, "holds at full");
            firable.Release();
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(1f, shots.LastPower, 0.001f);
        }

        [Test]
        public void ABlockedGate_StopsTheChargeFromStarting()
        {
            var firable = Make(true, 0.2f, TestRecipes.Cooldown(5f));
            firable.Press();
            firable.Tick(1f);
            firable.Press();
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(FiringPhase.Idle, firable.Phase, "on cooldown, so it never started charging");
        }
    }
}
