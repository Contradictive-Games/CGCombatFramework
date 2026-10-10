using System.Collections.Generic;
using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class ChargesGateTests
    {
        private TestAssets assets;
        private FakeUnit owner;
        private CountingEmission shots;
        private readonly List<FireBlockReason> blocked = new();

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            owner = new FakeUnit("Owner");
            shots = new CountingEmission();
            blocked.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            owner.Dispose();
            assets.Dispose();
        }

        private Firable Make(params IFireGate[] gates)
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Gates = gates;
            definition.Emissions = new IEmission[] { shots };
            var firable = new Firable(definition, owner);
            firable.OnBlocked += (_, reason) => blocked.Add(reason);
            return firable;
        }

        private static IChargeCounter Counter(Firable firable)
        {
            Assert.IsTrue(firable.TryGetGate<IChargeCounter>(out var counter));
            return counter;
        }

        [Test]
        public void SpendsEveryCharge_ThenBlocks()
        {
            var firable = Make(TestRecipes.Charges(2, 1f));
            for (int i = 0; i < 3; i++) firable.Press();
            Assert.AreEqual(2, shots.Count);
            Assert.AreEqual(FireBlockReason.NoCharges, blocked[0]);
        }

        [Test]
        public void Refills_OneChargeAtATime()
        {
            var firable = Make(TestRecipes.Charges(2, 1f));
            firable.Press();
            firable.Press();
            firable.Tick(1.5f);
            Assert.AreEqual(1, Counter(firable).Charges);
            Assert.AreEqual(0.5f, Counter(firable).RechargeProgress01, 0.001f);
            firable.Tick(0.5f);
            Assert.AreEqual(2, Counter(firable).Charges);
        }

        [Test]
        public void RechargeRateStat_SpeedsItUp()
        {
            var rate = assets.Stat("RechargeRate", 1f);
            owner.Stats.GetOrCreate(rate).SetBaseValue(2f);
            var firable = Make(TestRecipes.Charges(2, 1f, rate));
            firable.Press();
            firable.Tick(0.5f);
            Assert.AreEqual(2, Counter(firable).Charges);
        }

        [Test]
        public void Reset_RefillsEverything()
        {
            var firable = Make(TestRecipes.Charges(3, 10f));
            firable.Press();
            firable.Press();
            firable.Reset();
            Assert.AreEqual(3, Counter(firable).Charges);
        }
    }
}