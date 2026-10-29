using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public class EventFlowTests
    {
        private TestWorld world;
        private readonly List<string> log = new();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            log.Clear();
        }

        [TearDown] public void TearDown() => world.Dispose();

        private static T Get<T>(IUnit unit) where T : class
        {
            Assert.IsTrue(unit.TryGet<T>(out var capability), $"{unit.DisplayName} has no {typeof(T).Name}");
            return capability;
        }

        private static List<T> Record<T>(EventBus bus) where T : struct, ICombatEvent
        {
            var list = new List<T>();
            bus.Subscribe<T>((in T e) => list.Add(e));
            return list;
        }

        private static DamageRequest Hit(IUnit source, IUnit target, float amount)
        {
            var ctx = new EffectContext(source, null, target);
            return new DamageRequest(amount, null, in ctx, DamageFlags.None);
        }

        [Test]
        public void Damage_NamesTheUnitItIsAbout_AndTheOtherSide()
        {
            var attacker = world.Spawn(world.CreateTemplate("Attacker"));
            var victim = world.Spawn(world.CreateTemplate("Victim"));
            var received = Record<DamageReceivedEvent>(victim.Events);
            var dealt = Record<DamageDealtEvent>(attacker.Events);
            var died = Record<DiedEvent>(victim.Events);
            var killed = Record<KilledUnitEvent>(attacker.Events);

            Get<IHealth>(victim).ApplyDamage(Hit(attacker, victim, 150f));

            Assert.AreSame(victim, received[0].Unit);
            Assert.AreSame(attacker, received[0].Source);
            Assert.AreSame(attacker, dealt[0].Unit);
            Assert.AreSame(victim, dealt[0].Target);
            Assert.AreSame(victim, died[0].Unit);
            Assert.AreSame(attacker, died[0].Killer);
            Assert.AreSame(attacker, killed[0].Unit);
            Assert.AreSame(victim, killed[0].Victim);
        }

        [Test]
        public void KillingBlow_AnnouncesTheEmptyBar_BeforeTheDeath()
        {
            var attacker = world.Spawn(world.CreateTemplate("Attacker"));
            var victim = world.Spawn(world.CreateTemplate("Victim"));
            var events = CombatServices.Events;
            events.Subscribe<ResourceChangedEvent>((in ResourceChangedEvent e) => log.Add("changed"));
            events.Subscribe<ResourceDepletedEvent>((in ResourceDepletedEvent e) => log.Add("depleted"));
            events.Subscribe<DamageReceivedEvent>((in DamageReceivedEvent e) => log.Add("received"));
            events.Subscribe<DamageDealtEvent>((in DamageDealtEvent e) => log.Add("dealt"));
            events.Subscribe<DiedEvent>((in DiedEvent e) => log.Add("died"));
            events.Subscribe<KilledUnitEvent>((in KilledUnitEvent e) => log.Add("killed"));

            Get<IHealth>(victim).ApplyDamage(Hit(attacker, victim, 150f));

            CollectionAssert.AreEqual(new[] { "changed", "depleted", "received", "dealt", "died", "killed" }, log);
        }

        [Test]
        public void Revive_NamesWhoBroughtItBack()
        {
            var healer = world.Spawn(world.CreateTemplate("Healer"));
            var unit = world.Spawn(world.CreateTemplate("Fallen"));
            var health = Get<IHealth>(unit);
            var revived = Record<RevivedEvent>(unit.Events);
            health.Kill(default);

            health.Revive(0.5f, new EffectContext(healer, null, unit));

            Assert.AreEqual(1, revived.Count);
            Assert.AreSame(unit, revived[0].Unit);
            Assert.AreSame(healer, revived[0].Source);
            Assert.AreEqual(50f, revived[0].Health, 0.001f);
        }

        [Test]
        public void RevivePayload_PassesItsSource()
        {
            var healer = world.Spawn(world.CreateTemplate("Healer"));
            var unit = world.Spawn(world.CreateTemplate("Fallen"));
            var revived = Record<RevivedEvent>(unit.Events);
            Get<IHealth>(unit).Kill(default);

            new RevivePayload().Apply(new EffectContext(healer, null, unit));

            Assert.AreSame(healer, revived[0].Source);
        }

        [Test]
        public void RealUnits_StatsAndResources_RaiseOnTheirOwnBus()
        {
            var unit = world.Spawn(world.CreateTemplate());
            var stats = Record<StatChangedEvent>(unit.Events);
            var resources = Record<ResourceChangedEvent>(unit.Events);

            Get<IStatProvider>(unit).GetOrCreate(world.MaxHealth).SetBaseValue(200f);

            Assert.AreEqual(1, stats.Count);
            Assert.AreSame(unit, stats[0].Unit);
            Assert.AreEqual(100f, stats[0].Previous);
            Assert.AreEqual(200f, stats[0].Current);
            Assert.AreEqual(1, resources.Count);
            Assert.AreEqual(ChangeReason.MaxChanged, resources[0].Change.Reason);
            Assert.AreEqual(200f, resources[0].Change.Max);
        }

        [Test]
        public void StartingTags_AreHeldOnce_OnAPooledUnitsFirstLife()
        {
            var elite = world.Assets.Tag("Rank.Elite");
            var template = world.CreateTemplate();
            TestAssets.Set(template, "startingTags", new[] { elite });
            var unit = world.Spawn(template);

            Assert.IsTrue(unit.Tags.HasExact(elite));
            unit.Tags.Remove(elite);
            Assert.IsFalse(unit.Tags.HasExact(elite), "a single remove should clear a tag the unit started with");
        }

        [Test]
        public void Projectile_AnnouncesItsLaunch_AndTheWallThatStoppedIt()
        {
            var player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
            world.Wall(new Vector3(0f, 0f, 3f), new Vector3(4f, 4f, 0.2f));
            world.SyncPhysics();
            var launched = Record<ProjectileLaunchedEvent>(player.Events);
            var stopped = Record<ProjectileStoppedEvent>(player.Events);
            var firable = world.Assets.Make<FirableDefinition>();

            TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 30f, new CountingEmission()).Emit(DeliveryContext.FromUnit(player, firable));
            world.Ticks.Run(1f, 1f / 60f);

            Assert.AreEqual(1, launched.Count);
            Assert.AreSame(player, launched[0].Unit);
            Assert.AreSame(firable, launched[0].Cause);
            Assert.AreEqual(30f, launched[0].Velocity.magnitude, 0.01f);
            Assert.AreEqual(1, stopped.Count);
            Assert.AreSame(launched[0].Projectile, stopped[0].Projectile);
            Assert.AreEqual(ProjectileStopReason.HitWorld, stopped[0].Reason);
            Assert.AreEqual(2.9f, stopped[0].Point.z, 0.05f);
        }

        [Test]
        public void Projectile_AnnouncesBounces()
        {
            var player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
            world.Wall(new Vector3(0f, 0f, 3f), new Vector3(6f, 4f, 0.2f));
            world.SyncPhysics();
            var bounced = Record<ProjectileBouncedEvent>(player.Events);
            var gun = TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 30f, new CountingEmission());
            TestAssets.Set(gun, "bounces", 1);

            gun.Emit(DeliveryContext.FromUnit(player, null));
            world.Ticks.Run(0.5f, 1f / 60f);

            Assert.AreEqual(1, bounced.Count);
            Assert.AreEqual(-1f, bounced[0].Normal.z, 0.01f);
        }

        [Test]
        public void Projectile_RunningOutOfLifetime_StopsAsExpired()
        {
            var player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
            var stopped = Record<ProjectileStoppedEvent>(player.Events);
            var gun = TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 10f, new CountingEmission());
            TestAssets.Set(gun, "lifetime", 0.2f);

            gun.Emit(DeliveryContext.FromUnit(player, null));
            world.Ticks.Run(0.5f, 1f / 60f);

            Assert.AreEqual(1, stopped.Count);
            Assert.AreEqual(ProjectileStopReason.Expired, stopped[0].Reason);
        }

        [Test]
        public void EndSession_AnnouncesItself_ThenWhatItCancels()
        {
            var player = world.Spawn(world.CreateTemplate("Player", faction: world.Players));
            var events = CombatServices.Events;
            events.Subscribe<SessionEndedEvent>((in SessionEndedEvent e) => log.Add("session " + e.Reason));
            events.Subscribe<ScheduleCancelledEvent>((in ScheduleCancelledEvent e) => log.Add("cancelled"));
            events.Subscribe<ProjectileStoppedEvent>((in ProjectileStoppedEvent e) => log.Add("projectile " + e.Reason));
            CombatScheduler.Schedule(5f, new IPayload[] { new CountingPayload() }, new EffectContext(player, null, player));
            TestRecipes.Shoot(world.CreateProjectileTemplate(), speed: 1f, new CountingEmission()).Emit(DeliveryContext.FromUnit(player, null));

            CombatServices.EndSession();

            CollectionAssert.AreEqual(new[] { "session SessionEnd", "cancelled", "projectile Released" }, log);
        }
    }
}
