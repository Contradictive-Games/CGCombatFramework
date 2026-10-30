using NUnit.Framework;
using Unity.PerformanceTesting;

namespace CG.CombatFramework.Tests
{
    public class EventBusPerformanceTests
    {
        private readonly struct Ping : ICombatEvent { }
        private readonly struct Other : ICombatEvent { }

        private FakeUnit unit;
        private int heard;

        [SetUp]
        public void SetUp()
        {
            unit = new FakeUnit("Listener");
            heard = 0;
        }

        [TearDown] public void TearDown() => unit.Dispose();

        private void OnPing(in Ping _) => heard++;
        private void OnOther(in Other _) => heard++;

        [Test, Performance]
        public void EventBus_RaiseWithNoListeners()
        {
            var bus = new EventBus();
            bus.Subscribe<Other>(OnOther);

            Measure.Method(() => bus.Raise(new Ping()))
                .SampleGroup(new SampleGroup("Event Bus", SampleUnit.Microsecond))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
        }

        [Test, Performance]
        public void EventBus_RaiseToOneHandler()
        {
            var bus = new EventBus();
            bus.Subscribe<Ping>(OnPing);

            Measure.Method(() => bus.Raise(new Ping()))
                .SampleGroup(new SampleGroup("Event Bus", SampleUnit.Microsecond))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
            Assert.Greater(heard, 0);
        }

        [Test, Performance]
        public void GameEvents_RaiseToAUnitAndAQuietGlobalBus()
        {
            unit.Events.Subscribe<Ping>(OnPing);

            Measure.Method(() => GameEvents.Raise(unit, new Ping()))
                .SampleGroup(new SampleGroup("Event Bus", SampleUnit.Microsecond))
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(10_000)
                .GC()
                .Run();
            Assert.Greater(heard, 0);
        }
    }
}
