using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    public sealed class TestWorld : IDisposable
    {
        public readonly TestAssets Assets = new();
        public readonly ManualTickSource Ticks;
        public readonly PooledUnitSpawner Spawner;
        public readonly StatType MaxHealth;
        public readonly StatType Armor;
        public readonly StatType MoveSpeed;
        public readonly ResourceType Health;
        public readonly UnitStatProfile Profile;
        public readonly Faction Players;
        public readonly Faction Enemies;

        private readonly GameObject root;

        /// <param name="realTicker">True: let CombatServices create a real CombatTicker, driven by frames.</param>
        public TestWorld(bool realTicker = false)
        {
            CombatServices.UnpauseAll();
            Ticks = realTicker ? null : new ManualTickSource();
            CombatServices.TickSource = Ticks;
            CombatServices.Random = new SeededGameRandom(1234);
            root = new GameObject("[Test World]");
            Spawner = new PooledUnitSpawner(root.transform);
            CombatServices.Spawner = Spawner;

            MaxHealth = Assets.Stat("MaxHealth", 100f);
            Armor = Assets.Stat("Armor");
            MoveSpeed = Assets.Stat("MoveSpeed", 5f);
            Health = Assets.Make<ResourceType>("Health");
            Health.MaxStat = MaxHealth;

            Profile = Assets.Make<UnitStatProfile>("Profile");
            Profile.Stats = new[]
            {
                new StatEntry { Type = MaxHealth, BaseValue = 100f },
                new StatEntry { Type = Armor, BaseValue = 0f },
                new StatEntry { Type = MoveSpeed, BaseValue = 5f }
            };
            Profile.Resources = new[] { new ResourceEntry { Type = Health, BaseMax = 100f } };

            Players = Assets.Make<Faction>("Players");
            Enemies = Assets.Make<Faction>("Enemies");
            Enemies.HostileTo = new[] { Players };
        }

        /// <summary>
        /// An inactive "prefab" with a collider, stats, resources, health and status effects.
        /// effectsFirst puts the status effect component above stats, the order that used to lose modifiers.
        /// </summary>
        public Unit CreateTemplate(string name = "Test Unit", bool effectsFirst = false, Faction faction = null,
            float colliderRadius = 0.5f, DamagePipeline pipeline = null)
        {
            var go = new GameObject(name);
            go.SetActive(false); // nothing Awakes until a clone is spawned
            go.transform.SetParent(root.transform);
            if (colliderRadius > 0f) go.AddComponent<SphereCollider>().radius = colliderRadius;

            if (effectsFirst) go.AddComponent<StatusEffectsComponent>();
            TestAssets.Set(go.AddComponent<StatsComponent>(), "profile", Profile);
            TestAssets.Set(go.AddComponent<ResourceComponent>(), "profile", Profile);
            var health = go.AddComponent<HealthComponent>();
            TestAssets.Set(health, "healthType", Health);
            if (pipeline != null) TestAssets.Set(health, "pipeline", pipeline);
            if (!effectsFirst) go.AddComponent<StatusEffectsComponent>();

            var unit = go.AddComponent<Unit>();
            TestAssets.Set(unit, "startingTags", Array.Empty<CombatTag>());
            TestAssets.Set(unit, "spawnOnStart", false);
            TestAssets.Set(unit, "faction", faction);
            return unit;
        }

        public IUnit Spawn(Unit template, Vector3 position = default, IReadOnlyList<StatusEffectDefinition> startingEffects = null)
        {
            return Spawner.Spawn(template, new SpawnData(position, Quaternion.identity, startingEffects: startingEffects));
        }

        /// <summary>A collider with no unit: blocks rays and projectiles like level geometry.</summary>
        public GameObject Wall(Vector3 center, Vector3 size)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(root.transform);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        public Projectile CreateProjectileTemplate()
        {
            var go = new GameObject("Projectile");
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            return go.AddComponent<Projectile>();
        }

        /// <summary>
        /// Physics queries see colliders where they were at the last sync. Spawning moves units,
        /// so call this after spawning and before any query, raycast or projectile step.
        /// </summary>
        public void SyncPhysics() => Physics.SyncTransforms();

        public static float HealthOf(IUnit unit) => unit.TryGet<IHealth>(out var health) ? health.Resource.Current : -1f;

        public void Dispose()
        {
            CombatServices.EndSession();
            CombatScheduler.Clear();
            CombatServices.UnpauseAll();
            CombatServices.Events.Clear();
            if (CombatServices.TickSource is Component ticker) Object.DestroyImmediate(ticker.gameObject);
            CombatServices.TickSource = null;
            CombatServices.Spawner = null;
            CombatServices.Random = null;
            Object.DestroyImmediate(root);
            Assets.Dispose();
        }
    }
}