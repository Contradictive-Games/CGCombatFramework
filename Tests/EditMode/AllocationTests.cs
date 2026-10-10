using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace CG.CombatFramework.Tests
{
    /// <summary>The hot paths that run every frame or every hit must make zero garbage once warm.</summary>
    public class AllocationTests
    {
        private readonly struct Ping { }

        private static int pings;
        private static void OnPing(in Ping _) => pings++;

        private TestAssets assets;
        private readonly List<FakeUnit> units = new();

        [SetUp] public void SetUp() => assets = new TestAssets();

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in units) unit.Dispose();
            units.Clear();
            assets.Dispose();
        }

        private FakeUnit Unit(string name)
        {
            var unit = new FakeUnit(name);
            units.Add(unit);
            return unit;
        }

        [Test]
        public void EventBus_Raise()
        {
            var bus = new EventBus();
            bus.Subscribe<Ping>(OnPing);
            bus.Raise(new Ping());
            Assert.That(() => bus.Raise(new Ping()), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Stat_ChangeAndRead()
        {
            var stat = new Stat(assets.Stat("Damage"), 10f);
            var modifier = new StatModifier(0.5f, StatModifierType.PercentAdd, null);
            stat.AddModifier(modifier);
            stat.TryRemoveModifier(modifier);
            Assert.That(() =>
            {
                stat.AddModifier(modifier);
                _ = stat.Value;
                stat.TryRemoveModifier(modifier);
                _ = stat.Value;
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void DamagePipeline_AllTenSteps()
        {
            var attacker = Unit("Attacker");
            var target = Unit("Target");
            var pipeline = TestRecipes.FullPipeline(assets, target);
            var request = new DamageRequest(40f, null, new EffectContext(attacker, null, target), DamageFlags.None);
            pipeline.Run(in request, target);
            Assert.That(() => { pipeline.Run(in request, target); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void FirableLoadout_Tick()
        {
            var owner = Unit("Owner");
            var loadout = owner.AddUnitComponent<FirableLoadout>();
            var definition = assets.Make<FirableDefinition>();
            definition.Gates = new IFireGate[] { TestRecipes.Cooldown(0.1f), TestRecipes.Charges(2, 1f) };
            for (int i = 0; i < 3; i++) loadout.Equip(assets.Make<FirableSlot>(), definition);
            loadout.Tick(0.1f);
            Assert.That(() => loadout.Tick(0.1f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Firable_HeldFullAutoWithResourceCost()
        {
            var owner = Unit("Owner");
            var mana = assets.Make<ResourceType>("Mana");
            var resources = new FakeResources();
            owner.Add(resources);
            resources.Add(mana, 1e9f, 1e9f);
            var cost = TestRecipes.Configure(new ResourceCostGate(), ("resource", mana));
            var definition = assets.Make<FirableDefinition>();
            definition.Mode = new FullAutoFire();
            definition.Gates = new IFireGate[] { cost };
            definition.Emissions = new IEmission[] { new CountingEmission() };
            var firable = new Firable(definition, owner);
            firable.Press();
            firable.Tick(0.1f);
            Assert.That(() => firable.Tick(0.1f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Firable_TickAndFire()
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Gates = new IFireGate[] { TestRecipes.Cooldown(0.1f) };
            definition.Emissions = new IEmission[] { new CountingEmission() };
            var firable = new Firable(definition, Unit("Owner"));
            firable.Tick(0.2f);
            firable.Press();
            Assert.That(() =>
            {
                firable.Tick(0.2f);
                firable.Press();
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void StatusEffects_SteadyTick()
        {
            var host = Unit("Host");
            var effects = host.AddUnitComponent<StatusEffectsComponent>();
            var speed = assets.Stat("MoveSpeed", 5f);
            for (int i = 0; i < 20; i++)
            {
                var effect = assets.Effect($"Effect {i}", duration: 0f, tickInterval: 0.05f, behaviors: new IStatusEffectBehavior[]
                {
                    TestRecipes.Modifier(speed, -0.01f, StatModifierType.PercentAdd),
                    TestRecipes.OnTick(new CountingPayload())
                });
                effects.Apply(effect, host);
            }
            effects.Tick(0.1f);
            Assert.That(() => effects.Tick(0.1f), Is.Not.AllocatingGCMemory());
        }
    }
}