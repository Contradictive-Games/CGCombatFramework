using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

namespace CG.CombatFramework.Tests
{
    public class WorldPerformanceTests
    {
        private TestWorld world;

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            world = null;
        }

        /// <summary>Real frames, real ticker: what 200 burning, slowed units cost, broken down by marker.</summary>
        [UnityTest, Performance]
        public IEnumerator Frame_TwoHundredBurningUnits()
        {
            world = new TestWorld(realTicker: true);
            var burn = world.Assets.Effect("Burn", duration: 0f, tickInterval: 0.5f,
                behaviors: TestRecipes.OnTick(TestRecipes.Damage(0.01f, DamageFlags.Periodic)));
            var slow = world.Assets.Effect("Slow", duration: 0f,
                behaviors: TestRecipes.Modifier(world.MoveSpeed, -0.3f, StatModifierType.PercentAdd));
            var starting = new[] { burn, slow };
            var template = world.CreateTemplate();
            for (int i = 0; i < 200; i++)
                world.Spawn(template, new Vector3(i % 20, 0f, i / 20), starting);

            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CombatProfiler).TypeHandle);
            yield return Measure.Frames()
                .WarmupCount(10)
                .MeasurementCount(120)
                .ProfilerMarkers(CombatProfiler.TickName, CombatProfiler.StatusEffectsName, CombatProfiler.DamageName)
                .Run();
        }

        /// <summary>Pooled spawn and despawn of a 50-unit wave, after prewarming.</summary>
        [Test, Performance]
        public void Spawner_FiftyUnitWave()
        {
            world = new TestWorld();
            var template = world.CreateTemplate();
            world.Spawner.Prewarm(template, 50);
            var wave = new List<IUnit>(50);

            Measure.Method(() =>
                {
                    for (int i = 0; i < 50; i++) wave.Add(world.Spawn(template));
                    for (int i = 0; i < wave.Count; i++) world.Spawner.Despawn(wave[i], DespawnReason.Recycled);
                    wave.Clear();
                })
                .WarmupCount(3)
                .MeasurementCount(20)
                .GC()
                .Run();
        }
    }
}