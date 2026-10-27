using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class PayloadTests
    {
        private TestAssets assets;
        private ManualTickSource ticks;
        private FakeUnit owner;
        private FakeUnit target;
        private FakeHealth health;

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            ticks = new ManualTickSource();
            CombatServices.TickSource = ticks;
            owner = new FakeUnit("Owner");
            target = new FakeUnit("Target");
            health = new FakeHealth { Owner = target };
            target.Add(health);
        }

        [TearDown]
        public void TearDown()
        {
            CombatScheduler.Clear();
            CombatServices.TickSource = null;
            owner.Dispose();
            target.Dispose();
            assets.Dispose();
        }

        private EffectContext Hit() => new(owner, null, target);
        private EffectContext OnSelf() => new(owner, null, owner);

        private static void AreClose(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.001f), $"expected {expected}, got {actual}");

        [Test]
        public void Damage_SendsItsAmountAndFlags()
        {
            TestRecipes.Damage(25f, DamageFlags.Periodic).Apply(Hit());
            Assert.AreEqual(25f, health.Received[0].Amount);
            Assert.IsTrue(health.Received[0].Has(DamageFlags.Periodic));
        }

        [Test]
        public void Kill_KillsTheSubject()
        {
            new KillPayload().Apply(Hit());
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void Revive_OnlyBringsBackTheDead_AtItsShare()
        {
            var revive = TestRecipes.Configure(new RevivePayload(), ("health", 0.5f));
            revive.Apply(Hit());
            Assert.AreEqual(100f, health.Current, "the living are left alone");

            health.Kill(default);
            revive.Apply(Hit());
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(50f, health.Current, 0.001f);
        }

        [TestCase(false, 60f)]
        [TestCase(true, 40f)]
        public void ModifyResource_RestoresOrDrains(bool drain, float expected)
        {
            var mana = assets.Make<ResourceType>("Mana");
            var resources = new FakeResources();
            target.Add(resources);
            var pool = resources.Add(mana, 100f, 50f);
            TestRecipes.Configure(new ModifyResourcePayload(), ("resource", mana), ("drain", drain)).Apply(Hit());
            Assert.AreEqual(expected, pool.Current, 0.001f);
        }

        [Test]
        public void ApplyStatusEffect_StacksOnTheTarget_AndCreditsTheSource()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var poison = assets.Effect("Poison", maxStacks: 5);
            TestRecipes.Configure(new ApplyStatusEffectPayload(), ("effect", poison), ("stacks", 2)).Apply(Hit());
            Assert.AreEqual(2, effects.GetStacks(poison));
            Assert.AreSame(owner, effects.Active[0].SourceUnit);
        }

        [Test]
        public void RemoveEffect_RemovesEveryStack()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var burn = assets.Effect("Burn", stacking: EffectStackingMode.Independent, maxStacks: 5);
            for (int i = 0; i < 3; i++) effects.Apply(burn, owner);
            TestRecipes.Configure(new RemoveEffectPayload(), ("effect", burn)).Apply(Hit());
            Assert.IsFalse(effects.Has(burn));
        }

        [Test]
        public void Dispel_RemovesUpToMax_OfTheCategory()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var debuff = assets.Tag("Effect.Debuff");
            foreach (var name in new[] { "Poison", "Bleed", "Curse" })
            {
                var each = assets.Effect(name);
                each.Categories = new[] { debuff };
                effects.Apply(each, owner);
            }
            var haste = assets.Effect("Haste");
            effects.Apply(haste, owner);

            TestRecipes.Configure(new DispelEffectsPayload(), ("category", debuff), ("max", 2)).Apply(Hit());
            Assert.AreEqual(2, effects.Active.Count, "one debuff left, and the haste");
            Assert.IsTrue(effects.Has(haste));
        }

        [Test]
        public void ConsumeStacks_RunsWithTheStacksRemoved()
        {
            var effects = target.AddUnitComponent<StatusEffectsComponent>();
            var bleed = assets.Effect("Bleed", maxStacks: 5);
            for (int i = 0; i < 4; i++) effects.Apply(bleed, owner);
            var probe = new CountingPayload();
            var rupture = TestRecipes.Configure(new ConsumeStacksPayload(), ("effect", bleed), ("payloads", new IPayload[] { probe }));

            rupture.Apply(Hit());
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
                .Apply(Hit());
            Assert.AreEqual(0, probe.Count);
            Assert.AreEqual(1, effects.GetStacks(bleed));
        }

        [TestCase(true, 1, 0)]
        [TestCase(false, 0, 1)]
        public void Conditional_RunsThenOrOtherwise(bool pass, int expectedThen, int expectedOtherwise)
        {
            var then = new CountingPayload();
            var otherwise = new CountingPayload();
            TestRecipes.Configure(new ConditionalPayload(), ("conditions", new ICondition[] { new FixedCondition(pass) }),
                ("then", new IPayload[] { then }), ("otherwise", new IPayload[] { otherwise })).Apply(Hit());
            Assert.AreEqual(expectedThen, then.Count);
            Assert.AreEqual(expectedOtherwise, otherwise.Count);
        }

        [Test]
        public void Delay_RunsOnceAfterItsDelay()
        {
            var probe = new CountingPayload();
            TestRecipes.Configure(new DelayPayload(), ("delay", 1f), ("payloads", new IPayload[] { probe })).Apply(Hit());
            ticks.Step(0.5f);
            Assert.AreEqual(0, probe.Count);
            ticks.Step(0.6f);
            Assert.AreEqual(1, probe.Count);
            Assert.AreSame(target, probe.Last.TargetUnit);
            ticks.Step(5f);
            Assert.AreEqual(1, probe.Count);
        }

        // Regression: Emit From Target used to start at the target's forward vector instead of its position.
        [Test]
        public void EmitAt_Target_StartsAtTheTarget_OneTriggerLevelDeeper()
        {
            target.Transform.SetPositionAndRotation(new Vector3(4f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));
            var probe = new RecordingEmission();
            TestRecipes.Configure(new EmitAtPayload(), ("emitFrom", EmitFrom.Target), ("emissions", new IEmission[] { probe })).Apply(Hit());

            AreClose(new Vector3(4f, 0f, 2f), probe.Last.Origin);
            AreClose(Vector3.right, probe.Last.Direction);
            Assert.AreEqual(1, probe.Last.Effect.ProcDepth);
        }

        [Test]
        public void EmitAt_HitPoint_StartsWhereTheHitLanded()
        {
            var probe = new RecordingEmission();
            var hit = new EffectContext(owner, null, target, new HitData(new Vector3(2f, 0f, 7f), Vector3.up, Vector3.left));
            TestRecipes.Configure(new EmitAtPayload(), ("emitFrom", EmitFrom.HitPoint), ("emissions", new IEmission[] { probe })).Apply(in hit);
            AreClose(new Vector3(2f, 0f, 7f), probe.Last.Origin);
            AreClose(Vector3.left, probe.Last.Direction);
        }

        [Test]
        public void Teleport_ToTheHitPoint()
        {
            var hit = new EffectContext(owner, null, target, new HitData(new Vector3(4f, 0f, 4f), Vector3.up, Vector3.forward));
            new TeleportPayload().Apply(in hit);
            AreClose(new Vector3(4f, 0f, 4f), owner.Transform.position);
        }

        [Test]
        public void Teleport_BehindTheTarget()
        {
            target.Transform.position = new Vector3(0f, 0f, 5f);
            TestRecipes.Configure(new TeleportPayload(), ("to", TeleportTo.BegindTarget), ("distanceBehind", 1.5f)).Apply(Hit());
            AreClose(new Vector3(0f, 0f, 3.5f), owner.Transform.position);
        }

        [Test]
        public void Teleport_SwapsWithTheTarget()
        {
            owner.Transform.position = new Vector3(1f, 0f, 0f);
            target.Transform.position = new Vector3(0f, 0f, 5f);
            TestRecipes.Configure(new TeleportPayload(), ("to", TeleportTo.SwapWithTarget)).Apply(Hit());
            AreClose(new Vector3(0f, 0f, 5f), owner.Transform.position);
            AreClose(new Vector3(1f, 0f, 0f), target.Transform.position);
        }

        private KnockbackProbe Knockable()
        {
            target.Transform.position = new Vector3(3f, 0f, 0f);
            var probe = new KnockbackProbe();
            target.Add(probe);
            return probe;
        }

        [Test]
        public void Knockback_PushesAwayFromTheSource()
        {
            var probe = Knockable();
            TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f))).Apply(Hit());
            AreClose(new Vector3(8f, 0f, 0f), probe.Last);
        }

        [Test]
        public void Knockback_Lift_TiltsThePushUpward()
        {
            var probe = Knockable();
            TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f)), ("lift", 1f)).Apply(Hit());
            AreClose(new Vector3(1f, 1f, 0f).normalized * 8f, probe.Last);
        }

        [Test]
        public void Knockback_Resistance_ScalesThePushDown_AndFullResistanceSkipsIt()
        {
            var probe = Knockable();
            var resistance = assets.Stat("KnockbackResistance");
            var push = TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f)), ("resistance", resistance));

            target.Stats.GetOrCreate(resistance).SetBaseValue(0.5f);
            push.Apply(Hit());
            AreClose(new Vector3(4f, 0f, 0f), probe.Last);

            target.Stats.GetOrCreate(resistance).SetBaseValue(1f);
            push.Apply(Hit());
            Assert.AreEqual(1, probe.Count, "full resistance never reaches the receiver");
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
