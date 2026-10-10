using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace CG.CombatFramework.Tests
{
    public class SystemPerformanceTests
    {
        private readonly struct Ping { }

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

        [Test, Performance]
        public void Stat_RecalculateWithTenModifiers()
        {
            var stat = new Stat(assets.Stat("Damage"), 10f);
            for (int i = 0; i < 10; i++) stat.AddModifier(new StatModifier(0.1f, (StatModifierType)(i % 3), null));
            var probe = new StatModifier(1f, StatModifierType.Flat, null);
            float sink = 0f;

            Measure.Method(() =>
                {
                    stat.AddModifier(probe);
                    sink += stat.Value;
                    stat.TryRemoveModifier(probe);
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
            Assert.Greater(sink, 0f); // keeps the reads from being optimised away
        }

        [Test, Performance]
        public void EventBus_RaiseToTenHandlers()
        {
            var bus = new EventBus();
            int count = 0;
            for (int i = 0; i < 10; i++) bus.Subscribe<Ping>((in Ping _) => count++);

            Measure.Method(() => bus.Raise(new Ping()))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
        }

        [Test, Performance]
        public void DamagePipeline_ArmorAndImmunity()
        {
            var attacker = Unit("Attacker");
            var target = Unit("Target");
            var armor = assets.Stat("Armor");
            target.Stats.GetOrCreate(armor).SetBaseValue(50f);
            var armorStep = new ArmorStep();
            TestAssets.Set(armorStep, "armorStat", armor);
            var immune = new ImmuneWithTagStep();
            TestAssets.Set(immune, "tag", assets.Tag("State.Invulnerable"));
            var pipeline = assets.Make<DamagePipeline>();
            pipeline.Steps.Add(immune);
            pipeline.Steps.Add(armorStep);
            var request = new DamageRequest(40f, null, new EffectContext(attacker, null, target), DamageFlags.None);

            Measure.Method(() => pipeline.Run(in request, target))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
        }

        /// <summary>One frame of status effects across a crowd: a ticking burn, a slow and a stun on each unit.</summary>
        [Test, Performance]
        public void StatusEffects_TickACrowd([Values(100, 1000)] int unitCount)
        {
            var speed = assets.Stat("MoveSpeed", 5f);
            var stunned = assets.Tag("State.Stunned");
            var burn = assets.Effect("Burn", duration: 0f, tickInterval: 0.5f, behaviors: TestRecipes.OnTick(new CountingPayload()));
            var slow = assets.Effect("Slow", duration: 0f, behaviors: TestRecipes.Modifier(speed, -0.3f, StatModifierType.PercentAdd));
            var stun = assets.Effect("Stun", duration: 0f);
            stun.GrantedTags.Add(stunned);

            var crowd = new List<StatusEffectsComponent>(unitCount);
            for (int i = 0; i < unitCount; i++)
            {
                var unit = Unit($"Unit {i}");
                var effects = unit.AddUnitComponent<StatusEffectsComponent>();
                effects.Apply(burn, unit);
                effects.Apply(slow, unit);
                effects.Apply(stun, unit);
                crowd.Add(effects);
            }

            Measure.Method(() =>
                {
                    for (int i = 0; i < crowd.Count; i++) crowd[i].Tick(1f / 60f);
                })
                .WarmupCount(10)
                .MeasurementCount(30)
                .GC()
                .Run();
        }

        [Test, Performance]
        public void Firables_TickAndFireAThousand()
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Gates.Add(TestRecipes.Cooldown(0.1f));
            definition.Emissions.Add(new CountingEmission());
            var owner = Unit("Owner");
            var firables = new List<Firable>(1000);
            for (int i = 0; i < 1000; i++) firables.Add(new Firable(definition, owner));

            Measure.Method(() =>
                {
                    for (int i = 0; i < firables.Count; i++)
                    {
                        firables[i].Tick(1f / 60f);
                        firables[i].Press();
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(30)
                .GC()
                .Run();
        }
    }
}