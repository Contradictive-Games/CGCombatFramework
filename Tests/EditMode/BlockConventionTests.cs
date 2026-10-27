using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    /// <summary>
    /// Every built-in building block, found by reflection so new blocks are covered without new tests here:
    /// it shows in the type picker with a description, and it runs with the settings it starts with.
    /// </summary>
    public class BlockConventionTests
    {
        private static readonly Type[] SlotTypes =
        {
            typeof(IPayload), typeof(ICondition), typeof(IEmission), typeof(IHitQuery), typeof(IDamageStep),
            typeof(IRegenRule), typeof(IValueScaler), typeof(IFireMode), typeof(IFireGate), typeof(IStatusEffectBehavior)
        };

        private static IEnumerable<Type> Blocks() => typeof(IPayload).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition && SlotTypes.Any(slot => slot.IsAssignableFrom(t)))
            .OrderBy(t => t.Name);

        private TestAssets assets;
        private ManualTickSource ticks;
        private GameObject poolRoot;
        private FakeUnit source;
        private FakeUnit target;
        private StatusEffectsComponent targetEffects;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            ticks = new ManualTickSource();
            CombatServices.TickSource = ticks;
            CombatServices.Random = new SeededGameRandom(1234);
            poolRoot = new GameObject("[Pools]");
            CombatServices.Spawner = new PooledUnitSpawner(poolRoot.transform);

            source = Unit("Source");
            target = Unit("Target");
            target.Transform.position = new Vector3(0f, 0f, 3f);
            targetEffects = target.AddUnitComponent<StatusEffectsComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            CombatScheduler.Clear();
            source.Dispose();
            target.Dispose();
            Object.DestroyImmediate(poolRoot);
            CombatServices.Spawner = null;
            CombatServices.TickSource = null;
            CombatServices.Random = null;
            assets.Dispose();
        }

        private static FakeUnit Unit(string name)
        {
            var unit = new FakeUnit(name);
            unit.Add(new FakeHealth { Owner = unit });
            unit.Add(new FakeResources());
            return unit;
        }

        [Test]
        public void ThereAreBlocksToCheck() => Assert.Greater(Blocks().Count(), 50);

        [TestCaseSource(nameof(Blocks))]
        public void ShowsInTheTypePicker_WithADescription(Type block)
        {
            Assert.IsTrue(block.IsSerializable, "[SerializeReference] needs [Serializable]");
            Assert.IsNotNull(block.GetConstructor(Type.EmptyTypes), "the type picker needs a public parameterless constructor");
            Assert.IsFalse(string.IsNullOrWhiteSpace(block.GetCustomAttribute<TypeMenuAttribute>(false)?.Path), "needs [TypeMenu]");
            var info = block.GetCustomAttribute<BlockInfoAttribute>(false);
            Assert.IsFalse(string.IsNullOrWhiteSpace(info?.Description), "needs [BlockInfo] with a description");
            Assert.IsFalse(string.IsNullOrWhiteSpace(info.Example), "needs a [BlockInfo] Example");
        }

        [Test]
        public void TypeMenuPaths_AreUniqueInEachSlot()
        {
            foreach (var slot in SlotTypes)
            {
                var clashes = Blocks().Where(slot.IsAssignableFrom)
                    .GroupBy(t => t.GetCustomAttribute<TypeMenuAttribute>(false)?.Path)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.Name))}");
                CollectionAssert.IsEmpty(clashes, $"two {slot.Name} blocks share a menu path");
            }
        }

        /// <summary>A block fresh from the type picker, with nothing assigned, must not throw when it runs.</summary>
        [TestCaseSource(nameof(Blocks))]
        public void RunsWithTheSettingsItStartsWith(Type block)
        {
            var instance = Activator.CreateInstance(block);
            Assert.DoesNotThrow(() => Run(instance));
        }

        [Test]
        public void Events_AreReadonlyStructs()
        {
            var events = typeof(IPayload).Assembly.GetTypes().Where(t => t.IsValueType && !t.IsEnum && t.Name.EndsWith("Event")).ToList();
            Assert.Greater(events.Count, 10);
            var mutable = events.Where(t => !t.GetCustomAttributes(false).Any(a => a.GetType().Name == "IsReadOnlyAttribute")).Select(t => t.Name);
            CollectionAssert.IsEmpty(mutable, "events are passed with in, so they must be readonly structs");
        }

        private void Run(object block)
        {
            var ctx = new EffectContext(source, null, target, new HitData(Vector3.zero, Vector3.up, Vector3.forward));
            var delivery = DeliveryContext.FromUnit(source, null);

            if (block is IPayload payload) payload.Apply(in ctx);
            if (block is ICondition condition) condition.Evaluate(in ctx);
            if (block is IEmission emission) emission.Emit(in delivery);
            if (block is IValueScaler scaler) new ScalableValue(10f) { Scalers = new[] { scaler } }.Evaluate(in ctx);
            if (block is IHitQuery query)
            {
                var hits = HitCollector.Rent();
                query.Query(in delivery, hits);
                HitCollector.Return(hits);
            }
            if (block is IDamageStep step)
            {
                var pipeline = assets.Make<DamagePipeline>();
                pipeline.Steps = new[] { step };
                pipeline.Run(new DamageRequest(10f, null, in ctx, DamageFlags.None), target);
            }
            if (block is IRegenRule rule)
            {
                var resource = new Resource(null, 100f, new[] { rule });
                resource.ResetTo(50f);
                resource.Tick(1f);
            }
            if (block is IFireMode mode) Fire(mode, null);
            if (block is IFireGate gate) Fire(null, gate);
            if (block is IStatusEffectBehavior behavior) Wear(behavior, in ctx);
            ticks.Run(2f);
        }

        private void Fire(IFireMode mode, IFireGate gate)
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Mode = mode;
            definition.Gates = gate != null ? new[] { gate } : Array.Empty<IFireGate>();
            var firable = new Firable(definition, source);
            firable.Press();
            firable.Tick(0.5f);
            firable.Release();
            firable.Tick(0.5f);
            firable.Reset();
        }

        private void Wear(IStatusEffectBehavior behavior, in EffectContext ctx)
        {
            var effect = assets.Effect("Probe", duration: 1f, tickInterval: 0.25f, maxStacks: 2, behaviors: behavior);
            targetEffects.Apply(effect, source);
            targetEffects.Apply(effect, source);

            var request = new DamageRequest(10f, null, in ctx, DamageFlags.None);
            var result = new DamageResult(in request, HitResult.Hit, 10f, 0f, 0f, false, true);
            var died = new DeathInfo(target, source, in ctx, result);
            var killed = new DeathInfo(source, target, in ctx, result);
            target.Events.Raise(new DamageDealtEvent(target, source, in result));
            target.Events.Raise(new DamageReceivedEvent(source, target, in result));
            target.Events.Raise(new KilledUnitEvent(in killed));
            target.Events.Raise(new DiedEvent(in died));

            for (int i = 0; i < 6; i++) targetEffects.Tick(0.25f);
            targetEffects.RemoveAll(effect);
        }
    }
}
