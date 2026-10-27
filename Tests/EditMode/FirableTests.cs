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
            definition.Gates = gates;
            definition.Emissions = new IEmission[] { shots };
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
        public void FullAuto_FiresAsFastAsTheGatesAllow_WhileHeld()
        {
            var firable = Make(new FullAutoFire(), TestRecipes.Cooldown(0.1f));
            firable.Press();
            for (int i = 0; i < 4; i++) firable.Tick(0.1f);
            Assert.AreEqual(5, shots.Count);

            firable.Tick(0.05f);
            Assert.AreEqual(0, blocked.Count, "held ticks that hit the cooldown don't report it");
            firable.Release();
            for (int i = 0; i < 4; i++) firable.Tick(0.1f);
            Assert.AreEqual(5, shots.Count);
        }

        [Test]
        public void ConditionGate_BlocksWhileAConditionFails()
        {
            var silenced = assets.Tag("State.Silenced");
            var gate = TestRecipes.Configure(new ConditionGate(),
                ("conditions", new ICondition[] { TestRecipes.Not(TestRecipes.HasTag(silenced, Subject.Source)) }));
            var firable = Make(new SemiAutoFire(), gate);

            owner.Tags.Add(silenced);
            firable.Press();
            Assert.AreEqual(0, shots.Count);
            Assert.AreEqual(FireBlockReason.Condition, blocked[0]);

            owner.Tags.Remove(silenced);
            firable.Press();
            Assert.AreEqual(1, shots.Count);
        }

        [TestCase(false, 2)]
        [TestCase(true, 1)]
        public void ResourceCost_SpendsOnFire_AndBlocksWhenShort(bool cannotDeplete, int expectedShots)
        {
            var mana = assets.Make<ResourceType>("Mana");
            var resources = new FakeResources();
            owner.Add(resources);
            var pool = resources.Add(mana, 100f, 20f);
            var cost = TestRecipes.Configure(new ResourceCostGate(), ("resource", mana), ("cost", new ScalableValue(10f)), ("cannotDeplete", cannotDeplete));
            var firable = Make(new SemiAutoFire(), cost);

            for (int i = 0; i < 3; i++) firable.Press();
            Assert.AreEqual(expectedShots, shots.Count);
            Assert.AreEqual(20f - expectedShots * 10f, pool.Current, 0.001f);
            Assert.AreEqual(FireBlockReason.NotEnoughResource, blocked[0]);
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