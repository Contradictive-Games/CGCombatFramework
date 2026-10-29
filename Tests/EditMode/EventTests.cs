using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class EventTests
    {
        private TestAssets assets;
        private FakeUnit owner;
        private FakeUnit target;
        private readonly List<string> log = new();
        private readonly List<Action> cleanup = new();

        [SetUp]
        public void SetUp()
        {
            assets = new TestAssets();
            owner = new FakeUnit("Owner");
            target = new FakeUnit("Target");
            log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var undo in cleanup)
            {
                undo();
            }
            cleanup.Clear();
            CombatScheduler.Clear();
            CombatServices.UnpauseAll();
            CombatServices.TickSource = null;
            owner.Dispose();
            target.Dispose();
            assets.Dispose();
        }

        private static List<T> Record<T>(IUnit unit) where T : struct, ICombatEvent
        {
            var list = new List<T>();
            unit.Events.Subscribe<T>((in T e) => list.Add(e));
            return list;
        }

        private List<T> RecordGlobal<T>() where T : struct, ICombatEvent
        {
            var list = new List<T>();
            GameEventHandler<T> handler = (in T e) => list.Add(e);
            CombatServices.Events.Subscribe(handler);
            cleanup.Add(() => CombatServices.Events.Unsubscribe(handler));
            return list;
        }

        private Resource Owned(float max, float current, params IRegenRule[] regen)
        {
            var resource = new Resource(assets.Make<ResourceType>("Shield"), max, regen, owner: owner);
            resource.ResetTo(current);
            return resource;
        }

        private static FlatRegen Regen(float perSecond) => TestRecipes.Configure(new FlatRegen(), ("perSecond", perSecond));

        private StatusEffectsComponent Effects(FakeUnit unit)
        {
            unit.Add(new FakeHealth { Owner = unit });
            return unit.AddUnitComponent<StatusEffectsComponent>();
        }

        private Firable Make(IFireMode mode, params IFireGate[] gates)
        {
            var definition = assets.Make<FirableDefinition>();
            definition.Mode = mode;
            definition.Gates = gates;
            definition.Emissions = new IEmission[] { new CountingEmission() };
            return new Firable(definition, owner);
        }

        private static ChargeFire Charge(bool fireWhenFull = false) =>
            TestRecipes.Configure(new ChargeFire(), ("maxChargeTime", 1f), ("minimumCharge", 0.2f), ("fireWhenFull", fireWhenFull));

        [Test]
        public void Resource_Depleting_RaisesChangedThenDepleted_BeforeItsListeners()
        {
            var shield = Owned(100f, 40f);
            var changed = Record<ResourceChangedEvent>(owner);
            owner.Events.Subscribe<ResourceChangedEvent>((in ResourceChangedEvent e) => log.Add("changed"));
            owner.Events.Subscribe<ResourceDepletedEvent>((in ResourceDepletedEvent e) => log.Add("depleted"));
            shield.OnDepleted += _ => log.Add("listener");

            shield.Remove(60f, ChangeReason.Damage);

            CollectionAssert.AreEqual(new[] { "changed", "depleted", "listener" }, log);
            Assert.AreSame(owner, changed[0].Unit);
            Assert.AreSame(shield, changed[0].Change.Resource);
            Assert.AreEqual(40f, changed[0].Change.Old);
            Assert.AreEqual(0f, changed[0].Change.New);
        }

        [Test]
        public void Resource_Filling_RaisesFilledOnce_FromBelow()
        {
            var shield = Owned(100f, 50f);
            var filled = Record<ResourceFilledEvent>(owner);
            shield.Add(80f, ChangeReason.Heal);
            shield.Add(10f, ChangeReason.Heal);
            Assert.AreEqual(1, filled.Count);
            Assert.AreEqual(100f, filled[0].Change.New);
        }

        [Test]
        public void Regen_StartsOnItsFirstChange_AndStopsWhenFull()
        {
            var shield = Owned(100f, 85f, Regen(10f));
            var started = Record<ResourceRegenStartedEvent>(owner);
            var stopped = Record<ResourceRegenStoppedEvent>(owner);

            shield.Tick(1f);
            shield.Tick(1f);
            Assert.AreEqual(1, started.Count);
            Assert.AreSame(shield, started[0].Resource);
            Assert.AreEqual(0, stopped.Count, "the tick that filled it still changed it");

            shield.Tick(1f);
            Assert.AreEqual(1, stopped.Count);
            Assert.AreEqual(1, started.Count);
        }

        [Test]
        public void Regen_StopsWhenPaused()
        {
            var shield = Owned(100f, 0f, Regen(10f));
            var stopped = Record<ResourceRegenStoppedEvent>(owner);
            shield.Tick(1f);
            shield.RegenPaused = true;
            shield.Tick(1f);
            Assert.AreEqual(1, stopped.Count);
        }

        [Test]
        public void Unowned_ResourcesAndStats_RaiseNothing()
        {
            var resources = RecordGlobal<ResourceChangedEvent>();
            var stats = RecordGlobal<StatChangedEvent>();
            var bare = new Resource(null, 100f);
            bare.ResetTo(50f);
            var stat = new Stat(assets.Stat("Damage"), 10f);
            stat.SetBaseValue(20f);
            Assert.AreEqual(0, resources.Count);
            Assert.AreEqual(0, stats.Count);
        }

        [Test]
        public void Stat_RaisesPreviousAndCurrent_OnlyWhenTheValueMoves()
        {
            var stat = owner.Stats.GetOrCreate(assets.Stat("Damage", 10f));
            var changed = Record<StatChangedEvent>(owner);
            stat.AddModifier(new StatModifier(5f, StatModifierType.Flat, null));
            stat.AddModifier(new StatModifier(0f, StatModifierType.Flat, null));
            Assert.AreEqual(1, changed.Count);
            Assert.AreSame(stat, changed[0].Stat);
            Assert.AreEqual(10f, changed[0].Previous);
            Assert.AreEqual(15f, changed[0].Current);
        }

        [Test]
        public void Stat_ResourceMaxIsRescaled_BeforeTheEvent()
        {
            var stat = owner.Stats.GetOrCreate(assets.Stat("MaxShield", 100f));
            var shield = new Resource(assets.Make<ResourceType>("Shield"), 0f, owner: owner);
            shield.BindMaxStat(stat);
            shield.ResetTo(100f);
            float seen = -1f;
            owner.Events.Subscribe<StatChangedEvent>((in StatChangedEvent e) => seen = shield.Max);
            stat.SetBaseValue(150f);
            Assert.AreEqual(150f, seen);
        }

        [Test]
        public void StacksChanged_RaisesForGains_BeforeApplied()
        {
            var effects = Effects(target);
            var poison = assets.Effect("Poison", duration: 1f, stacking: EffectStackingMode.Independent, maxStacks: 3);
            target.Events.Subscribe<StatusEffectStacksChangedEvent>((in StatusEffectStacksChangedEvent e) => log.Add("stacks " + e.Previous + ">" + e.Effect.Stacks));
            target.Events.Subscribe<StatusEffectAppliedEvent>((in StatusEffectAppliedEvent e) => log.Add("applied " + e.PreviousStacks));
            effects.Apply(poison, owner);
            effects.Apply(poison, owner);
            CollectionAssert.AreEqual(new[] { "applied 0", "stacks 1>2", "applied 1" }, log);
        }

        [Test]
        public void StacksChanged_RaisesWhenIndependentStacksFallOff()
        {
            var effects = Effects(target);
            var poison = assets.Effect("Poison", duration: 1f, stacking: EffectStackingMode.Independent, maxStacks: 3);
            effects.Apply(poison, owner);
            effects.Tick(0.5f);
            effects.Apply(poison, owner);
            var changed = Record<StatusEffectStacksChangedEvent>(target);
            effects.Tick(0.6f);
            Assert.AreEqual(1, changed.Count);
            Assert.AreSame(target, changed[0].Unit);
            Assert.AreEqual(2, changed[0].Previous);
            Assert.AreEqual(1, changed[0].Effect.Stacks);
        }

        [Test]
        public void Ticked_RaisesEveryInterval()
        {
            var effects = Effects(target);
            var ticks = Record<StatusEffectTickedEvent>(target);
            effects.Apply(assets.Effect("Burn", duration: 2f, tickInterval: 0.5f), owner);
            for (int i = 0; i < 11; i++)
            {
                effects.Tick(0.1f);
            }
            Assert.AreEqual(2, ticks.Count);
            Assert.AreSame(target, ticks[0].Unit);
        }

        [Test]
        public void Triggered_RaisesBeforeItsPayloadsRun()
        {
            var effects = Effects(owner);
            var probe = new CountingPayload();
            var trigger = TestRecipes.Configure(new OnDamageDealtTrigger(), ("payloads", new IPayload[] { probe }));
            effects.Apply(assets.Effect("Item", duration: 0f, behaviors: trigger), owner);
            int payloadsFirst = -1;
            IUnit aimed = null;
            owner.Events.Subscribe<StatusEffectTriggeredEvent>((in StatusEffectTriggeredEvent e) =>
            {
                payloadsFirst = probe.Count;
                aimed = e.Target;
            });

            var ctx = new EffectContext(owner, null, target);
            var request = new DamageRequest(20f, null, in ctx, DamageFlags.None);
            owner.Events.Raise(new DamageDealtEvent(owner, target, new DamageResult(in request, HitResult.Hit, 20f, 0f, 0f, false, false)));

            Assert.AreEqual(0, payloadsFirst);
            Assert.AreEqual(1, probe.Count);
            Assert.AreSame(target, aimed);
        }

        [Test]
        public void FullAuto_Release_EndsWithReleased_AndReportsTheTimeHeld()
        {
            var firable = Make(new FullAutoFire());
            var phases = Record<FiringPhaseChangedEvent>(owner);
            firable.Press();
            firable.Tick(0.25f);
            firable.Tick(0.25f);
            firable.Release();

            Assert.AreEqual(2, phases.Count);
            Assert.AreEqual(FiringPhase.Idle, phases[0].Previous);
            Assert.AreEqual(FiringPhase.Firing, phases[0].Current);
            Assert.AreEqual(FiringEndReason.None, phases[0].EndReason);
            Assert.AreSame(owner, phases[1].Unit);
            Assert.AreSame(firable, phases[1].Firable);
            Assert.AreEqual(FiringPhase.Idle, phases[1].Current);
            Assert.AreEqual(FiringEndReason.Released, phases[1].EndReason);
            Assert.AreEqual(0.5f, phases[1].Elapsed, 0.0001f);
        }

        [Test]
        public void FullAuto_Cancel_EndsWithCancelled()
        {
            var firable = Make(new FullAutoFire());
            var phases = Record<FiringPhaseChangedEvent>(owner);
            firable.Press();
            firable.Cancel();
            Assert.AreEqual(FiringEndReason.Cancelled, phases[phases.Count - 1].EndReason);
        }

        [Test]
        public void Burst_EndsWithCompleted()
        {
            var firable = Make(TestRecipes.Configure(new BurstFire(), ("shots", 3), ("interval", 0.1f)));
            var phases = Record<FiringPhaseChangedEvent>(owner);
            firable.Press();
            firable.Tick(0.1f);
            firable.Tick(0.1f);
            Assert.AreEqual(FiringPhase.Idle, firable.Phase);
            Assert.AreEqual(2, phases.Count);
            Assert.AreEqual(FiringPhase.Bursting, phases[0].Current);
            Assert.AreEqual(FiringEndReason.Completed, phases[1].EndReason);
        }

        [TestCase(0.5f, FiringEndReason.Fired)]
        [TestCase(0.1f, FiringEndReason.Cancelled)]
        public void Charge_Release_ReportsWhetherItFired(float held, FiringEndReason expected)
        {
            var firable = Make(Charge());
            var phases = Record<FiringPhaseChangedEvent>(owner);
            firable.Press();
            firable.Tick(held);
            firable.Release();
            Assert.AreEqual(FiringPhase.Charging, phases[0].Current);
            Assert.AreEqual(expected, phases[phases.Count - 1].EndReason);
        }

        [Test]
        public void Charged_RaisesOnceWhenFull()
        {
            var firable = Make(Charge());
            var charged = Record<FirableChargedEvent>(owner);
            firable.Press();
            firable.Tick(0.6f);
            Assert.AreEqual(0, charged.Count);
            firable.Tick(0.6f);
            firable.Tick(0.6f);
            Assert.AreEqual(1, charged.Count);
            Assert.AreSame(firable, charged[0].Firable);
        }

        [Test]
        public void Loadout_AnnouncesEquipAndUnequip()
        {
            var loadout = owner.AddUnitComponent<FirableLoadout>();
            var slot = assets.Make<FirableSlot>();
            var equipped = Record<FirableEquippedEvent>(owner);
            var unequipped = Record<FirableUnequippedEvent>(owner);

            var firable = loadout.Equip(slot, assets.Make<FirableDefinition>());
            loadout.Unequip(slot);

            Assert.AreEqual(1, equipped.Count);
            Assert.AreSame(slot, equipped[0].Slot);
            Assert.AreSame(firable, equipped[0].Firable);
            Assert.AreEqual(1, unequipped.Count);
            Assert.AreSame(firable, unequipped[0].Firable);
        }

        [Test]
        public void Cooldown_AnnouncesItsStartAndEnd()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Cooldown(1f));
            var started = Record<CooldownStartedEvent>(owner);
            var ended = Record<CooldownEndedEvent>(owner);

            firable.Press();
            Assert.AreEqual(1, started.Count);
            Assert.AreSame(firable, started[0].Firable);
            Assert.AreEqual(1f, started[0].Duration, 0.0001f);

            firable.Tick(0.5f);
            Assert.AreEqual(0, ended.Count);
            firable.Tick(0.5f);
            firable.Tick(0.5f);
            Assert.AreEqual(1, ended.Count);
            Assert.AreSame(firable, ended[0].Firable);
        }

        [Test]
        public void Cooldown_FinishedEarly_StillAnnouncesTheEnd()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Cooldown(5f));
            var ended = Record<CooldownEndedEvent>(owner);
            firable.Press();
            Assert.IsTrue(firable.TryGetGate<ICooldown>(out var cooldown));
            cooldown.Reduce(10f);
            cooldown.Reduce(10f);
            Assert.AreEqual(1, ended.Count);
        }

        [Test]
        public void Charges_AnnounceSpendAndRefill()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Charges(2, 1f));
            var changed = Record<ChargesChangedEvent>(owner);
            firable.Press();
            firable.Tick(1f);
            Assert.AreEqual(2, changed.Count);
            Assert.AreSame(firable, changed[0].Firable);
            Assert.AreEqual(1, changed[0].Charges);
            Assert.AreEqual(2, changed[1].Charges);
            Assert.AreEqual(2, changed[1].MaxCharges);
        }

        [Test]
        public void Ammo_AnnouncesEachShot()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Ammo(3));
            var ammo = Record<AmmoChangedEvent>(owner);
            firable.Press();
            firable.Press();
            Assert.AreEqual(2, ammo.Count);
            Assert.AreSame(firable, ammo[1].Firable);
            Assert.AreEqual(1, ammo[1].Current);
            Assert.AreEqual(3, ammo[1].Capacity);
        }

        [Test]
        public void Reload_CompletesWithAFullMagazine_BeforeAmmoChanged()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Ammo(2));
            Assert.IsTrue(firable.TryGetGate<IReloadable>(out var magazine));
            var started = Record<ReloadStartedEvent>(owner);
            firable.Press();
            owner.Events.Subscribe<ReloadCompletedEvent>((in ReloadCompletedEvent e) => log.Add("reloaded " + magazine.Current));
            owner.Events.Subscribe<AmmoChangedEvent>((in AmmoChangedEvent e) => log.Add("ammo " + e.Current));

            Assert.IsTrue(magazine.Reload());
            Assert.AreEqual(1, started.Count);
            Assert.AreSame(firable, started[0].Firable);
            firable.Tick(5f);

            CollectionAssert.AreEqual(new[] { "reloaded 2", "ammo 2" }, log);
        }

        [Test]
        public void AddAmmo_FillingTheMagazine_CompletesTheReload()
        {
            var firable = Make(new SemiAutoFire(), TestRecipes.Ammo(2));
            Assert.IsTrue(firable.TryGetGate<IReloadable>(out var magazine));
            firable.Press();
            magazine.Reload();
            var completed = Record<ReloadCompletedEvent>(owner);
            magazine.AddAmmo(0);
            Assert.AreEqual(1, completed.Count);
            Assert.IsFalse(magazine.IsReloading);
        }

        [Test]
        public void Knockback_AnnouncesTheImpulseItApplied()
        {
            target.Transform.position = new Vector3(3f, 0f, 0f);
            var probe = new KnockbackProbe();
            target.Add(probe);
            var pushes = Record<KnockbackEvent>(target);
            TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f))).Apply(new EffectContext(owner, null, target));
            Assert.AreEqual(1, pushes.Count);
            Assert.AreSame(target, pushes[0].Unit);
            Assert.AreSame(owner, pushes[0].Source);
            Assert.AreEqual(probe.Last, pushes[0].Impulse);
        }

        [Test]
        public void Knockback_WithNoReceiver_RaisesNothing()
        {
            var pushes = Record<KnockbackEvent>(target);
            TestRecipes.Configure(new KnockbackPayload(), ("force", new ScalableValue(8f))).Apply(new EffectContext(owner, null, target));
            Assert.AreEqual(0, pushes.Count);
        }

        [Test]
        public void Teleport_Swap_AnnouncesBothMoves()
        {
            owner.Transform.position = new Vector3(1f, 0f, 0f);
            target.Transform.position = new Vector3(0f, 0f, 5f);
            var ownerMoves = Record<TeleportedEvent>(owner);
            var targetMoves = Record<TeleportedEvent>(target);
            TestRecipes.Configure(new TeleportPayload(), ("to", TeleportTo.SwapWithTarget)).Apply(new EffectContext(owner, null, target));

            Assert.AreEqual(1, ownerMoves.Count);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), ownerMoves[0].From);
            Assert.AreEqual(new Vector3(0f, 0f, 5f), ownerMoves[0].To);
            Assert.AreSame(owner, ownerMoves[0].Source);
            Assert.AreEqual(1, targetMoves.Count);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), targetMoves[0].To);
        }

        [Test]
        public void Schedule_AnnouncesItsKey_ThenResolvesBeforeTheWorkRuns()
        {
            var ticks = new ManualTickSource();
            CombatServices.TickSource = ticks;
            var scheduled = Record<ScheduledEvent>(owner);
            var probe = new CountingPayload();
            int runFirst = -1;
            int resolvedKey = 0;
            owner.Events.Subscribe<ScheduleResolvedEvent>((in ScheduleResolvedEvent e) =>
            {
                runFirst = probe.Count;
                resolvedKey = e.Key;
            });
            var hit = new EffectContext(owner, null, target, new HitData(new Vector3(2f, 0f, 0f), Vector3.up, Vector3.forward));

            int key = CombatScheduler.Schedule(1f, new IPayload[] { probe }, in hit);
            Assert.Greater(key, 0);
            Assert.AreEqual(1, scheduled.Count);
            Assert.AreEqual(key, scheduled[0].Key);
            Assert.AreEqual(1f, scheduled[0].Delay);
            Assert.AreEqual(new Vector3(2f, 0f, 0f), scheduled[0].Origin);

            ticks.Run(1.1f, 0.1f);
            Assert.AreEqual(key, resolvedKey);
            Assert.AreEqual(0, runFirst);
            Assert.AreEqual(1, probe.Count);
        }

        [Test]
        public void Schedule_Clear_AnnouncesEachCancellation()
        {
            CombatServices.TickSource = new ManualTickSource();
            var cancelled = Record<ScheduleCancelledEvent>(owner);
            var hit = new EffectContext(owner, null, target);
            int first = CombatScheduler.Schedule(1f, new IPayload[] { new CountingPayload() }, in hit);
            int second = CombatScheduler.Schedule(2f, new IPayload[] { new CountingPayload() }, in hit);

            CombatScheduler.Clear();

            Assert.AreEqual(2, cancelled.Count);
            CollectionAssert.AreEquivalent(new[] { first, second }, new[] { cancelled[0].Key, cancelled[1].Key });
            Assert.AreEqual(ScheduleCancelReason.Cleared, cancelled[0].Reason);
        }

        [Test]
        public void Pause_AnnouncesOnlyWhenItFlips()
        {
            var changes = RecordGlobal<PauseChangedEvent>();
            var menu = new object();
            var cutscene = new object();
            CombatServices.Pause(menu);
            CombatServices.Pause(cutscene);
            CombatServices.Unpause(menu);
            CombatServices.Unpause(cutscene);
            Assert.AreEqual(2, changes.Count);
            Assert.IsTrue(changes[0].IsPaused);
            Assert.IsFalse(changes[1].IsPaused);
        }
    }
}
