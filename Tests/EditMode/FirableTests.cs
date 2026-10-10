using System.Collections.Generic;
using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class FirableTests
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
            CombatServices.UnpauseAll();
            owner.Dispose();
            assets.Dispose();
        }

        private Firable Make(IFireMode mode, params IFireGate[] gates)
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Mode = mode;
            definition.Gates.AddRange(gates);
            definition.Emissions.Add(shots);
            var firable = new Firable(definition, owner);
            firable.OnBlocked += (_, reason) => blocked.Add(reason);
            return firable;
        }

        [Test]
        public void NoGates_FiresOnPress() // regression: CheckReady used to start at NoAmmo
        {
            Make(new SemiAutoFire()).Press();
            Assert.AreEqual(1, shots.Count);
        }

        [Test]
        public void Cooldown_BlocksUntilItRunsOut()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Cooldown(1f));
            firable.Press();
            firable.Press();
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(FireBlockReason.Cooldown, blocked[0]);

            firable.Tick(0.5f);
            firable.Press();
            Assert.AreEqual(1, shots.Count);

            firable.Tick(0.5f);
            firable.Press();
            Assert.AreEqual(2, shots.Count);
        }

        [Test]
        public void Ammo_EmptiesThenBlocks()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Ammo(3));
            for (int i = 0; i < 4; i++) firable.Press();
            Assert.AreEqual(3, shots.Count);
            Assert.AreEqual(FireBlockReason.NoAmmo, blocked[0]);
        }

        [Test]
        public void Gates_AreAllCheckedBeforeAnyIsPaid()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Cooldown(1f), TestRecipes.Ammo(1));
            firable.Press();  // fires: cooldown starts, magazine empties
            firable.Tick(1f); // cooldown over
            firable.Press();  // blocked by ammo
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(0f, firable.Gates[0].Fill01, "the cooldown was paid for a shot that never happened");
        }

        [Test]
        public void Paused_BlocksWithPausedReason()
        {
            var firable = Make(new SemiAutoFire());
            CombatServices.Pause(this);
            Assert.IsFalse(firable.TryFire());
            Assert.AreEqual(FireBlockReason.Paused, blocked[0]);
        }

        [Test]
        public void DeadOwner_BlocksWithDeadReason()
        {
            var health = new FakeHealth();
            owner.Add(health);
            health.Kill(default);
            Assert.IsFalse(Make(new SemiAutoFire()).TryFire());
            Assert.AreEqual(FireBlockReason.Dead, blocked[0]);
        }

        [Test]
        public void Firing_RaisesFiredEventOnTheOwnersBus()
        {
            int fired = 0;
            owner.Events.Subscribe<FiredEvent>((in FiredEvent _) => fired++);
            Make(new SemiAutoFire()).Press();
            Assert.AreEqual(1, fired);
        }
    }
}