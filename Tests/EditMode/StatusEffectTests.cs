using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class StatusEffectTests
    {
        private TestAssets assets;
        private FakeUnit host;
        private FakeUnit caster;
        private FakeHealth health;
        private StatusEffectsComponent effects;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            caster = new FakeUnit("Caster");
            host = new FakeUnit("Host");
            health = new FakeHealth(100f) { Owner = host };
            host.Add(health); // before the component, so Bind finds it
            effects = host.AddUnitComponent<StatusEffectsComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            host.Dispose();
            caster.Dispose();
            assets.Dispose();
        }

        private void Run(float seconds, float deltaTime = 0.1f)
        {
            int steps = Mathf.CeilToInt(seconds / deltaTime - 0.0001f);
            for (int i = 0; i < steps; i++) effects.Tick(deltaTime);
        }

        [Test]
        public void Apply_GrantsTags_AndExpiryRemovesThem()
        {
            var stunned = assets.Tag("State.Stunned");
            var stun = assets.Effect("Stun", duration: 1f);
            stun.GrantedTags = new[] { stunned };

            effects.Apply(stun, caster);
            Assert.IsTrue(host.Tags.Has(stunned));
            Run(1.05f);
            Assert.IsFalse(host.Tags.Has(stunned));
            Assert.AreEqual(0, effects.Active.Count);
        }

        [Test]
        public void OverlappingEffects_KeepASharedTagUntilBothEnd()
        {
            var stunned = assets.Tag("State.Stunned");
            var shortStun = assets.Effect("Short Stun", duration: 1f);
            var longStun = assets.Effect("Long Stun", duration: 2f);
            shortStun.GrantedTags = new[] { stunned };
            longStun.GrantedTags = new[] { stunned };

            effects.Apply(shortStun, caster);
            effects.Apply(longStun, caster);
            Run(1.05f);
            Assert.IsTrue(host.Tags.Has(stunned), "the short stun ending must not end the long one");
            Run(1f);
            Assert.IsFalse(host.Tags.Has(stunned));
        }

        [Test]
        public void BlockedBy_RefusesTheEffect()
        {
            var immune = assets.Tag("Immune.Stun");
            var stun = assets.Effect("Stun");
            stun.BlockedBy = new[] { immune };
            host.Tags.Add(immune);

            Assert.IsFalse(effects.Apply(stun, caster).IsValid);
            Assert.AreEqual(0, effects.Active.Count);
        }

        [Test]
        public void Ticks_BeforeExpiring()
        {
            var probe = new CountingBehaviour();
            effects.Apply(assets.Effect("Burn", duration: 4f, tickInterval: 1f, behaviors: probe), caster);
            Run(4.2f);
            Assert.AreEqual(4, probe.Ticks);
            Assert.AreEqual(1, probe.Removed);
            Assert.AreEqual(EffectRemoveReason.Expired, probe.LastReason);
        }

        [Test]
        public void Refresh_ResetsTheTimer()
        {
            var slow = assets.Effect("Slow", duration: 2f);
            effects.Apply(slow, caster);
            Run(1.5f);
            effects.Apply(slow, caster);
            Run(1.5f);
            Assert.IsTrue(effects.Has(slow));
            Run(0.6f);
            Assert.IsFalse(effects.Has(slow));
        }

        [Test]
        public void AddDuration_AddsTimeUpToTheCap()
        {
            var haste = assets.Effect("Haste", duration: 3f, stacking: EffectStackingMode.AddDuration, maxStacks: 3);
            haste.MaxDuration = 5f;
            effects.Apply(haste, caster);
            effects.Apply(haste, caster);
            var handle = effects.Apply(haste, caster);
            Assert.AreEqual(5f, handle.Get().Remaining, 0.001f);
            Assert.AreEqual(3, handle.Get().Stacks);
        }

        [Test]
        public void Independent_StacksFallOffOneAtATime()
        {
            var bleed = assets.Effect("Bleed", duration: 2f, stacking: EffectStackingMode.Independent, maxStacks: 5);
            effects.Apply(bleed, caster);
            Run(1f);
            effects.Apply(bleed, caster);
            Assert.AreEqual(2, effects.GetStacks(bleed));
            Run(1.05f);
            Assert.AreEqual(1, effects.GetStacks(bleed));
            Run(1f);
            Assert.IsFalse(effects.Has(bleed));
        }

        [Test]
        public void Independent_WhenFull_RefreshesTheStackClosestToFallingOff()
        {
            var bleed = assets.Effect("Bleed", duration: 2f, stacking: EffectStackingMode.Independent, maxStacks: 2);
            effects.Apply(bleed, caster);
            Run(0.5f);
            effects.Apply(bleed, caster); // timers 1.5, 2.0
            Run(0.5f);                    // timers 1.0, 1.5
            effects.Apply(bleed, caster); // full: the 1.0 stack goes back to 2.0
            Run(1.6f);                    // the 1.5 stack falls off, the refreshed one remains
            Assert.AreEqual(1, effects.GetStacks(bleed));
        }

        [Test]
        public void StackPerSource_KeepsOneInstancePerApplier()
        {
            var poison = assets.Effect("Poison", duration: 6f, tickInterval: 1f);
            poison.StackPerSource = true;
            using var otherCaster = new FakeUnit("Other Caster");

            effects.Apply(poison, caster);
            effects.Apply(poison, otherCaster);
            effects.Apply(poison, caster);
            Assert.AreEqual(2, effects.Active.Count);
        }

        [Test]
        public void StatModifiers_ScaleWithStacks_AndComeOffWhenRemoved()
        {
            var speed = assets.Stat("MoveSpeed", 10f);
            var slow = assets.Effect("Slow", duration: 5f, maxStacks: 3,
                behaviors: TestRecipes.Modifier(speed, -0.1f, StatModifierType.PercentAdd, perStack: true));

            effects.Apply(slow, caster);
            effects.Apply(slow, caster);
            Assert.AreEqual(8f, host.GetStat(speed), 0.001f);

            effects.RemoveAll(slow);
            Assert.AreEqual(10f, host.GetStat(speed), 0.001f);
        }

        [Test]
        public void Death_RemovesEffects_ExceptKeepOnDeath()
        {
            var burn = assets.Effect("Burn", duration: 5f);
            var passive = assets.Effect("Passive", duration: 0f);
            passive.KeepOnDeath = true;
            effects.Apply(burn, caster);
            effects.Apply(passive, host);

            health.Kill(default);
            Assert.IsFalse(effects.Has(burn));
            Assert.IsTrue(effects.Has(passive));
        }

        [Test]
        public void Despawn_RemovesEverything()
        {
            var probe = new CountingBehaviour();
            var passive = assets.Effect("Passive", duration: 0f, behaviors: probe);
            passive.KeepOnDeath = true;
            effects.Apply(passive, host);

            host.Despawn();
            Assert.AreEqual(0, effects.Active.Count);
            Assert.AreEqual(EffectRemoveReason.Despawned, probe.LastReason);
        }

        [Test]
        public void Handles_GoStale_WhenTheirEffectEnds()
        {
            var stun = assets.Effect("Stun");
            var slow = assets.Effect("Slow");
            var stunHandle = effects.Apply(stun, caster);
            effects.Remove(stunHandle);
            var slowHandle = effects.Apply(slow, caster); // likely reuses the stun's pooled instance

            Assert.IsFalse(stunHandle.IsValid);
            Assert.IsFalse(effects.Remove(stunHandle), "a stale handle must not remove the new effect");
            Assert.IsTrue(slowHandle.IsValid);
        }

        [Test]
        public void ATickThatKillsTheHost_CleansUp_AndCreditsTheApplier()
        {
            var burn = assets.Effect("Burn", duration: 10f, tickInterval: 1f,
                behaviors: TestRecipes.OnTick(TestRecipes.Damage(60f, DamageFlags.Periodic)));
            effects.Apply(burn, caster);

            Assert.DoesNotThrow(() => Run(2.05f));
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0, effects.Active.Count);
            var killingBlow = health.Received[1];
            Assert.AreSame(caster, killingBlow.Context.SourceUnit);
            Assert.AreSame(burn, killingBlow.Context.Cause);
            Assert.IsTrue(killingBlow.Has(DamageFlags.Periodic));
        }

        [Test]
        public void Dispel_RemovesByCategoryAndItsChildren()
        {
            var debuff = assets.Tag("Effect.Debuff");
            var poisonCategory = assets.Tag("Effect.Poison", debuff);
            var poison = assets.Effect("Poison");
            poison.Categories = new[] { poisonCategory };
            var haste = assets.Effect("Haste");
            effects.Apply(poison, caster);
            effects.Apply(haste, caster);

            Assert.AreEqual(1, effects.Dispel(debuff));
            Assert.IsFalse(effects.Has(poison));
            Assert.IsTrue(effects.Has(haste));
        }

        [Test]
        public void AppliedEvent_ReportsNewThenRestack()
        {
            var previous = new List<int>();
            host.Events.Subscribe<StatusEffectAppliedEvent>((in StatusEffectAppliedEvent e) => previous.Add(e.PreviousStacks));
            var rage = assets.Effect("Rage", maxStacks: 3);
            effects.Apply(rage, caster);
            effects.Apply(rage, caster);
            CollectionAssert.AreEqual(new[] { 0, 1 }, previous);
        }

        [Test]
        public void RemovedInstances_AreReused()
        {
            var stun = assets.Effect("Stun");
            effects.Remove(effects.Apply(stun, caster));
            int free = StatusEffectPool.FreeCount;
            effects.Apply(stun, caster);
            Assert.AreEqual(free - 1, StatusEffectPool.FreeCount);
        }
    }
}