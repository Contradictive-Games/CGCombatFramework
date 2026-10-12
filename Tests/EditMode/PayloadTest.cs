using NUnit.Framework;

namespace CG.CombatFramework.Tests
{
    public class PayloadTests
    {
        private TestAssets assets;
        private FakeUnit owner;
        private FakeUnit target;
        private FakeHealth health;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            owner = new FakeUnit("Owner");
            target = new FakeUnit("Target");
            health = new FakeHealth { Owner = target };
            target.Add(health);
        }

        [TearDown]
        public void TearDown()
        {
            owner.Dispose();
            target.Dispose();
            assets.Dispose();
        }

        private EffectContext Hit() => new(owner, null, target);

        [Test]
        public void Damage_SendsItsAmountAndFlags()
        {
            TestRecipes.Damage(25f, DamageFlags.Periodic).Apply(Hit());
            Assert.AreEqual(25f, health.Received[0].Amount);
            Assert.IsTrue(health.Received[0].Has(DamageFlags.Periodic));
        }

        [Test]
        public void ApplyStatusEffect_StacksOnTheTarget_AndCreditsTheSource()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var poison = assets.Effect("Poison", maxStacks: 5);
            var payload = new ApplyStatusEffectPayload();
            TestAssets.Set(payload, "effect", poison);
            TestAssets.Set(payload, "stacks", 2);

            payload.Apply(Hit());
            Assert.AreEqual(2, effects.GetStacks(poison));
            Assert.AreSame(owner, effects.Active[0].SourceUnit);
        }

        [Test]
        public void HasStatusEffect_ChecksMinimumStacks()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var bleed = assets.Effect("Bleed", maxStacks: 5);
            var condition = new HasStatusEffectCondition();
            TestAssets.Set(condition, "effect", bleed);
            TestAssets.Set(condition, "minStacks", 3);

            effects.Apply(bleed, owner, 2);
            Assert.IsFalse(condition.Evaluate(Hit()));
            effects.Apply(bleed, owner);
            Assert.IsTrue(condition.Evaluate(Hit()));
        }

        private EffectContext OnTarget() => new(owner, null, target);
        private EffectContext OnSelf() => new(owner, null, owner);

        [Test]
        public void ConsumeStacks_RunsWithTheStacksRemoved()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var bleed = assets.Effect("Bleed", maxStacks: 5);
            for (int i = 0; i < 4; i++) effects.Apply(bleed, owner);
            var probe = new CountingPayload();
            var rupture = TestRecipes.Configure(new ConsumeStacksPayload(), ("effect", bleed), ("payloads", new IPayload[] { probe }));

            rupture.Apply(OnTarget());
            Assert.AreEqual(4, probe.Last.Stacks);
            Assert.IsFalse(effects.Has(bleed));
        }

        [Test]
        public void ConsumeStacks_BelowTheMinimum_KeepsThem()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var bleed = assets.Effect("Bleed", maxStacks: 5);
            effects.Apply(bleed, owner);
            var probe = new CountingPayload();
            TestRecipes.Configure(new ConsumeStacksPayload(), ("effect", bleed), ("minimumStacks", 3), ("payloads", new IPayload[] { probe }))
                .Apply(OnTarget());
            Assert.AreEqual(0, probe.Count);
            Assert.AreEqual(1, effects.GetStacks(bleed));
        }

        private Firable Equip(params IFireGate[] gates)
        {
            var loadout = owner.AddUnitComponent<FirableLoadout>();
            var definition = assets.Make<FirableDefinition>();
            definition.Gates = gates;
            definition.Emissions = new IEmission[] { new CountingEmission() };
            return loadout.Equip(assets.Make<FirableSlot>(), definition);
        }

        [Test]
        public void ReduceCooldown_ShortensThenFinishes()
        {
            var firable = Equip(TestRecipes.Cooldown(10f));
            firable.Press();
            Assert.IsTrue(firable.TryGetGate<ICooldown>(out var cooldown));

            TestRecipes.Configure(new ReduceCooldownPayload(), ("seconds", new ScalableValue(4f))).Apply(OnSelf());
            Assert.AreEqual(6f, cooldown.Remaining, 0.001f);

            TestRecipes.Configure(new ReduceCooldownPayload(), ("finish", true)).Apply(OnSelf());
            Assert.AreEqual(0f, cooldown.Remaining);
            Assert.IsTrue(firable.IsReady);
        }

        [Test]
        public void AddAmmo_AddsThenFills()
        {
            var firable = Equip(TestRecipes.Ammo(6));
            for (int i = 0; i < 3; i++) firable.Press();
            Assert.IsTrue(firable.TryGetGate<IReloadable>(out var magazine));

            TestRecipes.Configure(new AddAmmoPayload(), ("amount", 2)).Apply(OnSelf());
            Assert.AreEqual(5, magazine.Current);
            TestRecipes.Configure(new AddAmmoPayload(), ("amount", 0)).Apply(OnSelf());
            Assert.AreEqual(6, magazine.Current);
        }
    }
}