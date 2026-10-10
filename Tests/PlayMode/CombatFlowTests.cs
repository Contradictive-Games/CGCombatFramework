using NUnit.Framework;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
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

            Get<IHealth>(victim).ApplyDamage(new DamageRequest(150f, null, new EffectContext(attacker, null, victim), DamageFlags.None));
            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(1, kills);
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

        [Test]
        public void Burning_KillsOverTime_AndCreditsTheApplier()
        {
            var burn = world.Assets.Effect("Burn", duration: 10f, tickInterval: 1f,
                behaviors: TestRecipes.OnTick(TestRecipes.Damage(30f, DamageFlags.Periodic)));
            var template = world.CreateTemplate();
            var attacker = world.Spawn(template);
            var victim = world.Spawn(template);
            int kills = 0;
            attacker.Events.Subscribe<KilledUnitEvent>((in KilledUnitEvent _) => kills++);
            var effects = Get<IStatusEffects>(victim);
            effects.Apply(burn, attacker);

            world.Ticks.Run(3f, 1f);
            Assert.IsTrue(victim.IsAlive, "90 damage after three ticks");
            world.Ticks.Run(1f, 1f);
            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(1, kills);
            Assert.AreEqual(0, effects.Active.Count, "death clears the burn");
        }

        [Test]
        public void Despawn_ClearsTagsAndEffects_ForTheNextLife()
        {
            var stunned = world.Assets.Tag("State.Stunned");
            var stun = world.Assets.Effect("Stun", duration: 5f);
            stun.GrantedTags = new[] { stunned };
            var template = world.CreateTemplate();
            var unit = world.Spawn(template);
            var effects = Get<IStatusEffects>(unit);
            effects.Apply(stun, unit);

            world.Spawner.Despawn(unit, DespawnReason.Recycled);
            var again = world.Spawn(template);
            Assert.AreSame(unit, again);
            Assert.IsFalse(again.Tags.Has(stunned));
            Assert.AreEqual(0, effects.Active.Count);
        }
    }
}