using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    /// <summary>Real units and components: the paths a game takes from a trigger pull to a death.</summary>
    public class CombatFlowTests
    {
        private TestWorld world;

        [SetUp] public void SetUp() => world = new TestWorld();
        [TearDown] public void TearDown() => world.Dispose();

        private static T Get<T>(IUnit unit) where T : class
        {
            Assert.IsTrue(unit.TryGet<T>(out var capability), $"{unit.DisplayName} has no {typeof(T).Name}");
            return capability;
        }

        private static DamageRequest Hit(IUnit from, IUnit to, float amount) =>
            new(amount, null, new EffectContext(from, null, to), DamageFlags.None);

        [Test]
        public void Firing_FromALoadout_DamagesWhatItHits()
        {
            var playerTemplate = world.CreateTemplate("Player", faction: world.Players);
            playerTemplate.gameObject.AddComponent<FirableLoadout>();
            var player = world.Spawn(playerTemplate);
            var enemy = world.Spawn(world.CreateTemplate("Enemy", faction: world.Enemies), new Vector3(0f, 0f, 5f));
            world.SyncPhysics();

            var rifle = world.Assets.Make<FirableDefinition>();
            rifle.Gates = new IFireGate[] { TestRecipes.Cooldown(0.5f) };
            rifle.Emissions = new IEmission[]
            {
                TestRecipes.Deliver(TestRecipes.Raycast(20f), new IPayload[] { TestRecipes.Damage(25f) }, TestRecipes.Relation())
            };
            var firable = Get<FirableLoadout>(player).Equip(world.Assets.Make<FirableSlot>(), rifle);

            firable.Press();
            firable.Press();
            Assert.AreEqual(75f, TestWorld.HealthOf(enemy), 0.01f, "the second press is on cooldown");
            world.Ticks.Run(0.6f);
            firable.Press();
            Assert.AreEqual(50f, TestWorld.HealthOf(enemy), 0.01f, "the loadout ticks its cooldown with the unit");
        }

        [Test]
        public void LethalDamage_KillsAndCreditsTheAttacker()
        {
            var template = world.CreateTemplate();
            var attacker = world.Spawn(template);
            var victim = world.Spawn(template, new Vector3(2f, 0f, 0f));
            int kills = 0;
            attacker.Events.Subscribe<KilledUnitEvent>((in KilledUnitEvent e) =>
            {
                if (ReferenceEquals(e.Death.Victim, victim)) kills++;
            });

            Get<IHealth>(victim).ApplyDamage(Hit(attacker, victim, 150f));
            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(1, kills);
        }

        [Test]
        public void Burning_KillsOverTime_AndCreditsTheApplier()
        {
            var burn = world.Assets.Effect("Burn", duration: 10f, tickInterval: 1f,
                behaviors: TestRecipes.OnTick(TestRecipes.Damage(30f, DamageFlags.Periodic)));
            var template = world.CreateTemplate();
            var attacker = world.Spawn(template);
            var victim = world.Spawn(template);
            DeathInfo? death = null;
            attacker.Events.Subscribe<KilledUnitEvent>((in KilledUnitEvent e) => death = e.Death);
            var effects = Get<IStatusEffects>(victim);
            effects.Apply(burn, attacker);

            world.Ticks.Run(3f, 1f);
            Assert.IsTrue(victim.IsAlive, "90 damage after three ticks");
            world.Ticks.Run(1f, 1f);
            Assert.IsFalse(victim.IsAlive);
            Assert.IsTrue(death.HasValue, "the applier gets the kill");
            Assert.AreSame(burn, death.Value.Context.Cause);
            Assert.IsTrue(death.Value.FinalBlow.Value.Request.Has(DamageFlags.Periodic));
            Assert.AreEqual(0, effects.Active.Count, "death clears the burn");
        }

        [Test]
        public void Heal_StopsAtMax_AndDoesNothingToTheDead()
        {
            var unit = world.Spawn(world.CreateTemplate());
            var health = Get<IHealth>(unit);
            health.ApplyDamage(Hit(unit, unit, 30f));

            var healed = health.ApplyHeal(50f, new EffectContext(unit, null, unit));
            Assert.AreEqual(30f, healed.Healed, 0.001f);
            Assert.AreEqual(20f, healed.Overheal, 0.001f);
            Assert.AreEqual(100f, health.Resource.Current, 0.001f);

            health.Kill(default);
            Assert.AreEqual(0f, health.ApplyHeal(50f, default).Healed);
            Assert.AreEqual(0f, health.Resource.Current);
        }

        [Test]
        public void Revive_RestoresAShareOfMax_AndClearsTheDeadTag()
        {
            var dead = world.Assets.Tag("State.Dead");
            var template = world.CreateTemplate();
            TestAssets.Set(template.GetComponent<HealthComponent>(), "deadTag", dead);
            var unit = world.Spawn(template);
            var health = Get<IHealth>(unit);

            health.Kill(default);
            Assert.IsFalse(unit.IsAlive);
            Assert.IsTrue(unit.Tags.Has(dead));

            health.Revive(0.5f);
            Assert.IsTrue(unit.IsAlive);
            Assert.IsFalse(unit.Tags.Has(dead));
            Assert.AreEqual(50f, health.Resource.Current, 0.001f);
        }

        // Regression: resources other than health used to keep regenerating while their unit was dead.
        [TestCase(false, 0f)]
        [TestCase(true, 10f)]
        public void Resources_RegenWhileDead_OnlyWhenAsked(bool regenWhileDead, float manaAfter)
        {
            var mana = world.Assets.Make<ResourceType>("Mana");
            world.Profile.Resources = new[]
            {
                new ResourceEntry { Type = world.Health, BaseMax = 100f, RegenWhileDead = true, RegenRules = new IRegenRule[] { Regen(10f) } },
                new ResourceEntry { Type = mana, BaseMax = 100f, Start = ResourceStart.Empty, RegenWhileDead = regenWhileDead, RegenRules = new IRegenRule[] { Regen(10f) } }
            };
            var unit = world.Spawn(world.CreateTemplate());
            Get<IHealth>(unit).Kill(default);
            world.Ticks.Run(1f, 0.1f);

            Assert.IsTrue(Get<IResourceProvider>(unit).TryGet(mana, out var pool));
            Assert.AreEqual(manaAfter, pool.Current, 0.01f);
            Assert.AreEqual(0f, TestWorld.HealthOf(unit), "health never regenerates while dead");
        }

        private static FlatRegen Regen(float perSecond) => TestRecipes.Configure(new FlatRegen(), ("perSecond", perSecond));

        [Test]
        public void OnDied_Triggers_BeforeDeathClearsTheirEffect()
        {
            var probe = new CountingPayload();
            var martyr = world.Assets.Effect("Martyr", duration: 0f,
                behaviors: TestRecipes.Configure(new OnDiedTrigger(), ("payloads", new IPayload[] { probe })));
            var template = world.CreateTemplate();
            var attacker = world.Spawn(template);
            var victim = world.Spawn(template, startingEffects: new[] { martyr });

            Get<IHealth>(victim).ApplyDamage(Hit(attacker, victim, 150f));
            Assert.AreEqual(1, probe.Count);
            Assert.AreSame(attacker, probe.Last.TargetUnit, "the other unit is the killer");
            Assert.AreEqual(0, Get<IStatusEffects>(victim).Active.Count, "the effect doesn't keep on death");
        }

        [TestCase(false, 1)]
        [TestCase(true, 0)]
        public void OnKill_CountsKillsByWhatTheHostOwns_UnlessDirectOnly(bool directOnly, int expected)
        {
            var probe = new CountingPayload();
            var bounty = world.Assets.Effect("Bounty", duration: 0f,
                behaviors: TestRecipes.Configure(new OnKillTrigger(), ("directOnly", directOnly), ("payloads", new IPayload[] { probe })));
            var template = world.CreateTemplate();
            var player = world.Spawn(template, startingEffects: new[] { bounty });
            var turret = world.Spawner.Spawn(template, new SpawnData(Vector3.zero, Quaternion.identity, owner: player));
            var victim = world.Spawn(template);

            Get<IHealth>(victim).ApplyDamage(Hit(turret, victim, 150f));
            Assert.AreEqual(expected, probe.Count);
        }

        // Regression: StatsComponent used to reset in OnSpawned, wiping modifiers from effects applied before it.
        [TestCase(false)]
        [TestCase(true)]
        public void StartingEffects_SurviveComponentOrder_AcrossLives(bool effectsFirst)
        {
            var brute = world.Assets.Effect("Brute", duration: 0f,
                behaviors: TestRecipes.Modifier(world.MaxHealth, 1f, StatModifierType.PercentAdd));
            var starting = new[] { brute };
            var template = world.CreateTemplate(effectsFirst: effectsFirst);

            var unit = world.Spawn(template, startingEffects: starting);
            var health = Get<IHealth>(unit).Resource;
            Assert.AreEqual(200f, health.Max, 0.001f);
            Assert.AreEqual(200f, health.Current, 0.001f);

            world.Spawner.Despawn(unit, DespawnReason.Killed);
            unit = world.Spawn(template, startingEffects: starting);
            health = Get<IHealth>(unit).Resource;
            Assert.AreEqual(200f, health.Max, 0.001f, "the second life must not stack or lose the bonus");
            Assert.AreEqual(200f, health.Current, 0.001f);
        }
    }
}
