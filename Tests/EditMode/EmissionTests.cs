using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class EmissionTests
    {
        private TestAssets assets;
        private ManualTickSource ticks;
        private FakeUnit source;
        private RecordingEmission probe;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            ticks = new ManualTickSource();
            CombatServices.TickSource = ticks;
            source = new FakeUnit("Source");
            probe = new RecordingEmission();
        }

        [TearDown]
        public void TearDown()
        {
            CombatScheduler.Clear();
            CombatServices.TickSource = null;
            source.Dispose();
            assets.Dispose();
        }

        private DeliveryContext Aim(IUnit target = null, IGameRandom random = null) =>
            new(new EffectContext(source, null, target, default, random: random), Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f));

        private static void AreClose(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.001f), $"expected {expected}, got {actual}");

        [Test]
        public void Multi_RunsEveryEmission_FromTheSameAim()
        {
            var other = new RecordingEmission();
            TestRecipes.Configure(new MultiEmission(), ("emissions", new IEmission[] { probe, other })).Emit(Aim());
            Assert.AreEqual(1, probe.Received.Count);
            Assert.AreEqual(1, other.Received.Count);
            AreClose(probe.Last.Direction, other.Last.Direction);
        }

        [Test]
        public void Repeat_RunsItsEmissionsCountTimes()
        {
            TestRecipes.Repeat(3, probe).Emit(Aim());
            Assert.AreEqual(3, probe.Received.Count);
        }

        [Test]
        public void Radial_Ring_SpreadsEvenlyAllTheWayAround()
        {
            TestRecipes.Configure(new RadialEmission(), ("count", 4), ("arc", 360f), ("emissions", new IEmission[] { probe })).Emit(Aim());
            Assert.AreEqual(4, probe.Received.Count);
            AreClose(Vector3.forward, probe.Received[0].Direction);
            AreClose(Vector3.right, probe.Received[1].Direction);
            AreClose(Vector3.back, probe.Received[2].Direction);
            AreClose(Vector3.left, probe.Received[3].Direction);
            AreClose(new Vector3(10f, 0f, 0f), probe.Received[1].AimPoint);
        }

        [Test]
        public void Radial_Fan_IsCenteredOnTheAim()
        {
            TestRecipes.Configure(new RadialEmission(), ("count", 3), ("arc", 90f), ("emissions", new IEmission[] { probe })).Emit(Aim());
            AreClose(Quaternion.AngleAxis(-45f, Vector3.up) * Vector3.forward, probe.Received[0].Direction);
            AreClose(Vector3.forward, probe.Received[1].Direction);
            AreClose(Quaternion.AngleAxis(45f, Vector3.up) * Vector3.forward, probe.Received[2].Direction);
        }

        [Test]
        public void WeightedRandom_PicksOneOption_AndNeverAZeroWeight()
        {
            var never = new RecordingEmission();
            var light = new RecordingEmission();
            var heavy = new RecordingEmission();
            var random = TestRecipes.Configure(new WeightedRandomEmission(), ("options", new[]
            {
                new WeightedEmission { Weight = 0f, Emissions = new IEmission[] { never } },
                new WeightedEmission { Weight = 1f, Emissions = new IEmission[] { light } },
                new WeightedEmission { Weight = 3f, Emissions = new IEmission[] { heavy } }
            }));
            var rng = new SeededGameRandom(7);
            for (int i = 0; i < 400; i++) random.Emit(Aim(random: rng));

            Assert.AreEqual(0, never.Received.Count);
            Assert.AreEqual(400, light.Received.Count + heavy.Received.Count, "exactly one option per emit");
            Assert.That(heavy.Received.Count, Is.InRange(250, 350), "about three in four");
        }

        [Test]
        public void Delayed_RunsAfterTheDelay_FromWhereItWasAimed()
        {
            var delayed = TestRecipes.Configure(new DelayedEmission(), ("delay", 1f), ("emissions", new IEmission[] { probe }));
            delayed.Emit(Aim());
            source.Transform.position = new Vector3(50f, 0f, 0f);

            ticks.Step(0.5f);
            Assert.AreEqual(0, probe.Received.Count);
            ticks.Step(0.6f);
            Assert.AreEqual(1, probe.Received.Count);
            AreClose(Vector3.zero, probe.Last.Origin);
            ticks.Step(5f);
            Assert.AreEqual(1, probe.Received.Count, "runs once");
        }

        [Test]
        public void MoveOrigin_ToTheAimPoint_WithOffsetAndDownward()
        {
            TestRecipes.Configure(new EmitFromEmission(), ("from", QueryCenter.AimPoint), ("offset", new Vector3(0f, 15f, 0f)),
                ("direction", EmitDirection.Down), ("emissions", new IEmission[] { probe })).Emit(Aim());
            AreClose(new Vector3(0f, 15f, 10f), probe.Last.Origin);
            AreClose(Vector3.down, probe.Last.Direction);
        }

        [Test]
        public void MoveOrigin_TowardTheTarget()
        {
            using var target = new FakeUnit("Target");
            target.Transform.position = new Vector3(5f, 0f, 0f);
            TestRecipes.Configure(new EmitFromEmission(), ("from", QueryCenter.Origin), ("direction", EmitDirection.TowardTarget),
                ("emissions", new IEmission[] { probe })).Emit(Aim(target));
            AreClose(Vector3.right, probe.Last.Direction);
        }

        [Test]
        public void PayloadsAtPoint_RunAtTheOrigin_WithNoTarget()
        {
            using var target = new FakeUnit("Target");
            var payload = new CountingPayload();
            var here = new DeliveryContext(new EffectContext(source, null, target), new Vector3(3f, 0f, 4f), Vector3.forward, Vector3.zero);
            TestRecipes.Configure(new PayloadsEmission(), ("payloads", new IPayload[] { payload })).Emit(in here);

            Assert.AreEqual(1, payload.Count);
            Assert.IsNull(payload.Last.TargetUnit);
            Assert.AreSame(source, payload.Last.SourceUnit);
            AreClose(new Vector3(3f, 0f, 4f), payload.Last.Hit.Point);
        }
    }
}
