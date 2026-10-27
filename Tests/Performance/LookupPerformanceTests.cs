using NUnit.Framework;
using Unity.PerformanceTesting;

namespace CG.CombatFramework.Tests
{
    /// <summary>
    /// What a capability lookup on a real Unit costs, next to a cached field. Each sample is 1,000 calls.
    /// GetStat is a TryGet hit plus the cached read, so the lookup's cost is GetStat minus CachedField.
    /// </summary>
    public class LookupPerformanceTests
    {
        private TestWorld world;
        private IUnit unit;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            unit = world.Spawn(world.CreateTemplate());
        }

        [TearDown] public void TearDown() => world.Dispose();

        private static void Run(System.Action thousandCalls, string name)
        {
            Measure.Method(thousandCalls)
                .SampleGroup(new SampleGroup(name, SampleUnit.Microsecond))
                .WarmupCount(5)
                .MeasurementCount(30)
                .GC()
                .Run();
        }

        [Test, Performance]
        public void TryGet_Miss() => Run(() =>
        {
            for (int i = 0; i < 1000; i++) unit.TryGet<IKnockbackReceiver>(out _);
        }, "TryGet miss x1000");

        [Test, Performance]
        public void GetStat() => Run(() =>
        {
            for (int i = 0; i < 1000; i++) unit.GetStat(world.Armor);
        }, "GetStat x1000");

        [Test, Performance]
        public void CachedField()
        {
            unit.TryGet<IStatProvider>(out var stats);
            Run(() =>
            {
                for (int i = 0; i < 1000; i++) stats.GetValue(world.Armor);
            }, "Cached GetValue x1000");
        }
    }
}