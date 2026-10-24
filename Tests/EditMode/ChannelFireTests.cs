using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class ChannelFireTests
    {
        private TestAssets assets;
        private FakeUnit owner;
        private CountingEmission pulses;
        private readonly List<ChannelEvent> events = new();

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            owner = new FakeUnit("Owner");
            pulses = new CountingEmission();
            events.Clear();
            owner.Events.Subscribe<ChannelEvent>(Record);
        }

        [TearDown]
        public void TearDown()
        {
            owner.Dispose();
            assets.Dispose();
        }

        private void Record(in ChannelEvent evt) => events.Add(evt);
        private ChannelEvent LastEvent => events[events.Count - 1];

        private Firable Make(ChannelFire channel, IFireGate[] gates)
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Mode = channel;
            definition.Gates = gates;
            definition.Emissions = new IEmission[] { pulses };
            return new Firable(definition, owner);
        }

        private static void Tick(Firable firable, int times, float deltaTime = 0.25f)
        {
            for (int i = 0; i < times; i++) firable.Tick(deltaTime);
        }

        [Test]
        public void PulsesOnStart_ThenEveryInterval()
        {
            var firable = Make(TestRecipes.Channel(), default);
            firable.Press();
            Tick(firable, 4);
            Assert.AreEqual(5, pulses.Count);
            Assert.AreEqual(FiringPhase.Channeling, firable.Phase);
            Assert.IsTrue(events[0].Started);
        }

        [Test]
        public void Release_EndsTheChannel()
        {
            var firable = Make(TestRecipes.Channel(), default);
            firable.Press();
            Tick(firable, 1);
            firable.Release();
            Tick(firable, 4);
            Assert.AreEqual(2, pulses.Count);
            Assert.AreEqual(FiringPhase.Idle, firable.Phase);
            Assert.AreEqual(ChannelEndReason.Release, LastEvent.EndReason);
        }

        [Test]
        public void Completes_AtMaxDuration()
        {
            var firable = Make(TestRecipes.Channel(maxDuration: 1f), default);
            firable.Press();
            Tick(firable, 4);
            Assert.AreEqual(5, pulses.Count, "the pulse on the final update still runs");
            Assert.AreEqual(ChannelEndReason.Completed, LastEvent.EndReason);
            Assert.AreEqual(FiringPhase.Idle, firable.Phase);
        }

        [Test]
        public void EveryPulse_EndsWhenItCantPay()
        {
            var firable = Make(TestRecipes.Channel(commit: ChannelCommit.EveryPulse), new[] { TestRecipes.Ammo(3) });
            firable.Press();
            Tick(firable, 5);
            Assert.AreEqual(3, pulses.Count);
            Assert.AreEqual(ChannelEndReason.Interrupted, LastEvent.EndReason);
        }

        [Test]
        public void OnEnd_StartsTheCooldownWhenTheChannelEnds()
        {
            var firable = Make(TestRecipes.Channel(commit: ChannelCommit.OnEnd), new[] { TestRecipes.Cooldown(5f) });
            firable.Press();
            Tick(firable, 2);
            Assert.AreEqual(0f, firable.Gates[0].Fill01, "nothing is paid while channeling");
            firable.Release();
            Assert.AreEqual(1f, firable.Gates[0].Fill01);
        }

        [Test]
        public void Cancel_NeverPaysOnEnd()
        {
            var firable = Make(TestRecipes.Channel(commit: ChannelCommit.OnEnd), new[] { TestRecipes.Cooldown(5f) });
            firable.Press();
            firable.Cancel();
            Assert.AreEqual(0f, firable.Gates[0].Fill01);
            Assert.AreEqual(ChannelEndReason.Cancelled, LastEvent.EndReason);
        }

        [Test]
        public void Breaks_WhenAKeepChannelingConditionFails()
        {
            var stunned = assets.Tag("State.Stunned");
            var notStunned = TestRecipes.HasTag(stunned, Subject.Source, invert: true);
            var firable = Make(TestRecipes.Channel(keepChannelingWhile: new[] { notStunned }), default);
            firable.Press();
            owner.Tags.Add(stunned);
            Tick(firable, 1);
            Assert.AreEqual(1, pulses.Count);
            Assert.AreEqual(ChannelEndReason.Interrupted, LastEvent.EndReason);
        }

        [Test]
        public void Power_FollowsTheCurve()
        {
            var channel = TestRecipes.Channel();
            TestAssets.Set(channel, "powerOverTime", AnimationCurve.Linear(0f, 1f, 1f, 2f));
            var firable = Make(channel, default);
            firable.Press();
            Assert.AreEqual(1f, pulses.LastPower, 0.001f);
            Tick(firable, 4);
            Assert.AreEqual(2f, pulses.LastPower, 0.001f);
        }
    }
}
