using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public class LifecycleTests
    {
        private TestWorld world;
        private CombatTag arriving;
        private readonly List<string> log = new();

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            arriving = world.Assets.Tag("State.Arriving");
            log.Clear();
        }

        [TearDown] public void TearDown() => world.Dispose();

        private Unit Template(float arrival = 0f, float departure = 0f, CombatTag[] arrivalTags = null, DamagePipeline pipeline = null)
        {
            var template = world.CreateTemplate(pipeline: pipeline);
            if (arrival > 0f) template.gameObject.AddComponent<TestArrival>().Duration = arrival;
            if (departure > 0f) template.gameObject.AddComponent<TestDeparture>().Duration = departure;
            TestAssets.Set(template, "arrivalTags", arrivalTags ?? Array.Empty<CombatTag>());
            return template;
        }

        private static T Part<T>(IUnit unit) where T : Component => ((Unit)unit).GetComponent<T>();

        private void Step(float seconds) => world.Ticks.Run(seconds, 0.1f);

        private static DamageRequest Hit(IUnit source, IUnit target, float amount)
        {
            var ctx = new EffectContext(source, null, target);
            return new DamageRequest(amount, null, in ctx, DamageFlags.None);
        }

        private void LogLifecycle()
        {
            var events = CombatServices.Events;
            events.Subscribe<UnitSpawnedEvent>((in UnitSpawnedEvent e) => log.Add("spawned"));
            events.Subscribe<UnitArrivingEvent>((in UnitArrivingEvent e) => log.Add("arriving"));
            events.Subscribe<UnitArrivedEvent>((in UnitArrivedEvent e) => log.Add("arrived"));
            events.Subscribe<UnitDespawnedEvent>((in UnitDespawnedEvent e) => log.Add("despawned"));
            events.Subscribe<UnitDepartingEvent>((in UnitDepartingEvent e) => log.Add("departing"));
            events.Subscribe<UnitReleasedEvent>((in UnitReleasedEvent e) => log.Add("released"));
        }

        private static List<IUnit> Query(IHitQuery query, IUnit source, IUnit target = null)
        {
            var effect = new EffectContext(source, null, target);
            Vector3 origin = source.Transform.position;
            var ctx = new DeliveryContext(in effect, origin, Vector3.forward, origin + Vector3.forward * 5f);
            var hits = HitCollector.Rent();
            var found = new List<IUnit>();
            try
            {
                query.Query(in ctx, hits);
                for (int i = 0; i < hits.Hits.Count; i++)
                {
                    found.Add(hits.Hits[i].Unit);
                }
            }
            finally
            {
                HitCollector.Return(hits);
            }
            return found;
        }

        [Test]
        public void Arrival_HoldsItsTags_ForTheLongestSequence()
        {
            var template = Template(arrival: 0.5f, arrivalTags: new[] { arriving });
            template.gameObject.AddComponent<TestArrival>().Duration = 1f;
            var unit = world.Spawn(template);
            Assert.IsTrue(unit.IsArriving);
            Assert.IsTrue(unit.Tags.Has(arriving));

            Step(0.7f);
            Assert.IsTrue(unit.IsArriving, "the 1s sequence is still playing");
            Step(0.4f);
            Assert.IsFalse(unit.IsArriving);
            Assert.IsFalse(unit.Tags.Has(arriving));
        }

        [Test]
        public void Arrival_AnnouncesArriving_WithItsTagsAndDuration_ThenArrived()
        {
            LogLifecycle();
            var seenTags = new List<CombatTag>();
            float seenDuration = 0f;
            CombatServices.Events.Subscribe<UnitArrivingEvent>((in UnitArrivingEvent e) =>
            {
                seenTags.AddRange(e.Tags);
                seenDuration = e.Duration;
            });

            world.Spawn(Template(arrival: 1f, arrivalTags: new[] { arriving }));
            Step(1.1f);

            CollectionAssert.AreEqual(new[] { "spawned", "arriving", "arrived" }, log);
            CollectionAssert.AreEqual(new[] { arriving }, seenTags);
            Assert.AreEqual(1f, seenDuration);
        }

        [Test]
        public void Arrival_PausesWithCombat()
        {
            var unit = world.Spawn(Template(arrival: 0.5f));
            var key = new object();
            CombatServices.Pause(key);
            Step(2f);
            Assert.IsTrue(unit.IsArriving);
            CombatServices.Unpause(key);
            Step(0.6f);
            Assert.IsFalse(unit.IsArriving);
        }

        [Test]
        public void Despawn_MidArrival_StopsTheSequence_AndNeverArrives()
        {
            LogLifecycle();
            var unit = world.Spawn(Template(arrival: 1f, arrivalTags: new[] { arriving }));
            var arrival = Part<TestArrival>(unit);

            world.Spawner.Despawn(unit, DespawnReason.Recycled);

            Assert.AreEqual(1, arrival.Stopped);
            Assert.IsFalse(unit.IsArriving);
            Assert.IsFalse(unit.Tags.Has(arriving));
            CollectionAssert.DoesNotContain(log, "arrived");
        }

        [Test]
        public void NoArrivalTime_ArrivesAtOnce_WithoutItsTags()
        {
            LogLifecycle();
            var unit = world.Spawn(Template(arrivalTags: new[] { arriving }));
            Assert.IsFalse(unit.IsArriving);
            Assert.IsFalse(unit.Tags.Has(arriving));
            CollectionAssert.AreEqual(new[] { "spawned", "arrived" }, log);
        }

        [Test]
        public void SpawnTags_ReplaceTheDefaults_AndEmptyOnesUseThem()
        {
            var dropPod = world.Assets.Tag("Arrival.DropPod");
            var template = Template(arrival: 1f, arrivalTags: new[] { arriving });

            var first = world.Spawner.Spawn(template, new SpawnData(Vector3.zero, Quaternion.identity, arrivalTags: new[] { dropPod }));
            CollectionAssert.AreEqual(new[] { dropPod }, Part<TestArrival>(first).LastTags);
            Assert.IsTrue(first.Tags.Has(dropPod));
            Assert.IsFalse(first.Tags.Has(arriving));

            var second = world.Spawner.Spawn(template, new SpawnData(Vector3.one, Quaternion.identity, arrivalTags: Array.Empty<CombatTag>()));
            CollectionAssert.AreEqual(new[] { arriving }, Part<TestArrival>(second).LastTags);
        }

        [Test]
        public void SpawnUnitPayload_PassesItsArrivalTags()
        {
            var dropPod = world.Assets.Tag("Arrival.DropPod");
            var template = Template(arrival: 1f);
            var caster = world.Spawn(world.CreateTemplate("Caster"));

            TestRecipes.Configure(new SpawnUnitPayload(), ("prefab", template), ("arrivalTags", new[] { dropPod }))
                .Apply(new EffectContext(caster, null, caster));

            IUnit spawned = null;
            foreach (var unit in CombatServices.Units.All)
            {
                if (!ReferenceEquals(unit, caster)) spawned = unit;
            }
            Assert.IsNotNull(spawned);
            CollectionAssert.AreEqual(new[] { dropPod }, Part<TestArrival>(spawned).LastTags);
        }

        [UnityTest]
        public IEnumerator SceneUnits_DoNotArrive_UnlessArriveOnStartIsOn() => SceneUnit(false);

        [UnityTest]
        public IEnumerator SceneUnits_Arrive_WithArriveOnStart() => SceneUnit(true);

        private IEnumerator SceneUnit(bool arriveOnStart)
        {
            var template = Template(arrival: 1f);
            TestAssets.Set(template, "spawnOnStart", true);
            TestAssets.Set(template, "arriveOnStart", arriveOnStart);
            var unit = Object.Instantiate(template);
            unit.gameObject.SetActive(true);
            try
            {
                yield return null;
                Assert.IsTrue(unit.IsSpawned);
                Assert.AreEqual(arriveOnStart, unit.IsArriving);
            }
            finally
            {
                Object.Destroy(unit.gameObject);
            }
        }

        [Test]
        public void ImmuneWithTag_KeepsAnArrivingUnitSafe()
        {
            var pipeline = world.Assets.Make<DamagePipeline>();
            pipeline.Steps = new IDamageStep[] { TestRecipes.Configure(new ImmuneWithTagStep(), ("tag", arriving)) };
            var unit = world.Spawn(Template(arrival: 1f, arrivalTags: new[] { arriving }, pipeline: pipeline));
            var attacker = world.Spawn(world.CreateTemplate("Attacker"));
            Assert.IsTrue(unit.TryGet<IHealth>(out var health));

            health.ApplyDamage(Hit(attacker, unit, 30f));
            Assert.AreEqual(100f, TestWorld.HealthOf(unit), 0.01f);

            Step(1.1f);
            health.ApplyDamage(Hit(attacker, unit, 30f));
            Assert.AreEqual(70f, TestWorld.HealthOf(unit), 0.01f);
        }

        [Test]
        public void Departure_KeepsTheObjectActive_UntilItsReleased()
        {
            var unit = (Unit)world.Spawn(Template(departure: 1f));
            world.Spawner.Despawn(unit, DespawnReason.Killed);
            Assert.IsTrue(unit.IsDeparting);
            Assert.IsFalse(unit.IsSpawned);
            Assert.IsTrue(unit.gameObject.activeSelf);

            Step(1.1f);
            Assert.IsFalse(unit.IsDeparting);
            Assert.IsFalse(unit.gameObject.activeSelf);
        }

        [Test]
        public void Pool_KeepsADepartingUnit_UntilItsReleased()
        {
            var template = Template(departure: 1f);
            var first = world.Spawn(template);
            world.Spawner.Despawn(first, DespawnReason.Recycled);

            var second = world.Spawn(template, Vector3.one);
            Assert.AreNotSame(first, second);

            Step(1.1f);
            Assert.AreSame(first, world.Spawn(template, Vector3.one * 2f));
        }

        [Test]
        public void Despawn_AnnouncesDespawned_ThenDeparting_ThenReleased()
        {
            var unit = world.Spawn(Template(departure: 1f));
            LogLifecycle();
            world.Spawner.Despawn(unit, DespawnReason.Killed);
            CollectionAssert.AreEqual(new[] { "despawned", "departing" }, log);
            Step(1.1f);
            CollectionAssert.AreEqual(new[] { "despawned", "departing", "released" }, log);
        }

        [Test]
        public void Views_UnbindOnlyAtRelease()
        {
            var template = Template(departure: 1f);
            template.gameObject.AddComponent<UnitViewBinder>();
            template.gameObject.AddComponent<TestView>();
            var unit = world.Spawn(template);
            var view = Part<TestView>(unit);
            Assert.AreEqual(1, view.Binds);

            world.Spawner.Despawn(unit, DespawnReason.Killed);
            Assert.AreEqual(0, view.Unbinds, "still bound while the departure plays");
            Step(1.1f);
            Assert.AreEqual(1, view.Unbinds);
        }

        [TestCase(DespawnReason.SessionEnd)]
        [TestCase(DespawnReason.Unloaded)]
        public void SomeReasons_SkipTheDeparture(DespawnReason reason)
        {
            var unit = (Unit)world.Spawn(Template(departure: 1f));
            var departure = Part<TestDeparture>(unit);
            world.Spawner.Despawn(unit, reason);
            Assert.IsFalse(unit.IsDeparting);
            Assert.AreEqual(0, departure.Begun);
            Assert.IsFalse(unit.gameObject.activeSelf);
        }

        [Test]
        public void EndSession_FinishesDepartures_AndStopsThem()
        {
            var unit = (Unit)world.Spawn(Template(departure: 5f));
            var departure = Part<TestDeparture>(unit);
            world.Spawner.Despawn(unit, DespawnReason.Recycled);

            CombatServices.EndSession();

            Assert.IsFalse(unit.IsDeparting);
            Assert.AreEqual(1, departure.Stopped);
            Assert.IsFalse(unit.gameObject.activeSelf);
        }

        [Test]
        public void SpawningADepartingUnit_FinishesItsDepartureFirst()
        {
            var unit = (Unit)world.Spawn(Template(departure: 5f));
            var departure = Part<TestDeparture>(unit);
            world.Spawner.Despawn(unit, DespawnReason.Recycled);
            LogLifecycle();

            unit.Spawn(new SpawnData(Vector3.zero, Quaternion.identity));

            Assert.AreEqual(1, departure.Stopped);
            Assert.IsTrue(unit.IsSpawned);
            Assert.IsFalse(unit.IsDeparting);
            Assert.IsTrue(unit.gameObject.activeSelf, "a unit spawned again isn't handed back to the pool");
            CollectionAssert.AreEqual(new[] { "released", "spawned", "arrived" }, log);

            world.Spawner.Despawn(unit, DespawnReason.Recycled);
            Step(5.1f);
            Assert.IsFalse(unit.gameObject.activeSelf);
        }

        [Test]
        public void Departure_CanReadHowTheUnitDied()
        {
            var unit = world.Spawn(Template(departure: 1f));
            var killer = world.Spawn(world.CreateTemplate("Killer"));
            Assert.IsTrue(unit.TryGet<IHealth>(out var health));
            health.ApplyDamage(Hit(killer, unit, 500f));

            world.Spawner.Despawn(unit, DespawnReason.Killed);

            var departure = Part<TestDeparture>(unit);
            Assert.IsTrue(departure.SawDeath);
            Assert.AreSame(killer, departure.Killer);
            Assert.AreEqual(DespawnReason.Killed, departure.LastReason);
        }

        [Test]
        public void Queries_SkipADepartingUnit_AndRaycastsPassThroughIt()
        {
            var departing = world.Spawn(Template(departure: 5f), new Vector3(0f, 0f, 2f));
            var behind = world.Spawn(world.CreateTemplate("Behind"), new Vector3(0f, 0f, 4f));
            var source = world.Spawn(world.CreateTemplate("Source"));
            world.Spawner.Despawn(departing, DespawnReason.Killed);
            world.SyncPhysics();

            CollectionAssert.AreEqual(new[] { behind }, Query(TestRecipes.Raycast(10f), source));
            CollectionAssert.AreEqual(new[] { behind }, Query(TestRecipes.Sphere(5f), source));
        }

        private Unit Mortal(float delay)
        {
            var template = world.CreateTemplate();
            TestAssets.Set(template.gameObject.AddComponent<DespawnOnDeath>(), "delay", delay);
            return template;
        }

        [Test]
        public void DespawnOnDeath_DespawnsAfterItsDelay()
        {
            var unit = world.Spawn(Mortal(1f));
            Assert.IsTrue(unit.TryGet<IHealth>(out var health));
            health.Kill(default);
            Assert.IsTrue(unit.IsSpawned);
            Step(0.5f);
            Assert.IsTrue(unit.IsSpawned);
            Step(0.6f);
            Assert.IsFalse(unit.IsSpawned);
        }

        [Test]
        public void DespawnOnDeath_ARevive_CancelsIt()
        {
            var unit = world.Spawn(Mortal(1f));
            Assert.IsTrue(unit.TryGet<IHealth>(out var health));
            health.Kill(default);
            Step(0.5f);
            health.Revive(1f);
            Step(1f);
            Assert.IsTrue(unit.IsSpawned);
        }

        [Test]
        public void DespawnOnDeath_AnnouncesTheSchedule()
        {
            var unit = world.Spawn(Mortal(2f));
            var scheduled = new List<DespawnScheduledEvent>();
            unit.Events.Subscribe<DespawnScheduledEvent>((in DespawnScheduledEvent e) => scheduled.Add(e));
            Assert.IsTrue(unit.TryGet<IHealth>(out var health));
            health.Kill(default);

            Assert.AreEqual(1, scheduled.Count);
            Assert.AreSame(unit, scheduled[0].Unit);
            Assert.AreEqual(2f, scheduled[0].Delay);
            Assert.AreEqual(DespawnReason.Killed, scheduled[0].Reason);
        }

        [Test]
        public void DespawnOnDeath_WithNoDelay_WaitsForTheNextTick()
        {
            var unit = world.Spawn(Mortal(0f));
            Assert.IsTrue(unit.TryGet<IHealth>(out var health));
            health.Kill(default);
            Assert.IsTrue(unit.IsSpawned, "never inside the killing blow");
            world.Ticks.Step(1f / 60f);
            Assert.IsFalse(unit.IsSpawned);
        }

        [TestCase("Sphere")]
        [TestCase("Box")]
        [TestCase("Cone")]
        [TestCase("Unit")]
        [TestCase("Chain")]
        [TestCase("Everyone")]
        public void Queries_SkipCorpsesByDefault_AndFindThemWhenAsked(string kind)
        {
            var source = world.Spawn(world.CreateTemplate("Source", faction: world.Players));
            var corpse = world.Spawn(world.CreateTemplate("Corpse", faction: world.Enemies), new Vector3(0f, 0f, 2f));
            Assert.IsTrue(corpse.TryGet<IHealth>(out var health));
            health.Kill(default);
            world.SyncPhysics();

            Assert.AreEqual(0, Query(Corpses(kind, true), source, corpse).Count, "Ignore Dead is on by default");
            CollectionAssert.AreEqual(new[] { corpse }, Query(Corpses(kind, false), source, corpse));
        }

        private static IHitQuery Corpses(string kind, bool ignoreDead)
        {
            IHitQuery query = kind switch
            {
                "Sphere" => TestRecipes.Sphere(5f),
                "Box" => new BoxQuery(),
                "Cone" => TestRecipes.Configure(new ConeQuery(), ("range", 5f)),
                "Unit" => new UnitQuery(),
                "Chain" => new ChainQuery(),
                _ => new EveryoneQuery()
            };
            TestAssets.Set(query, "ignoreDead", ignoreDead);
            return query;
        }
    }
}
