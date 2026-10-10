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

        private readonly GameObject root;
        private readonly List<GameObject> templates = new();

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
            Profile.Stats.Add(new StatEntry { Type = MaxHealth, BaseValue = 100f });
            Profile.Stats.Add(new StatEntry { Type = Armor, BaseValue = 0f });
            Profile.Stats.Add(new StatEntry { Type = MoveSpeed, BaseValue = 5f });
            Profile.Resources.Add(new ResourceEntry { Type = Health, BaseMax = 100f });
        }

        /// <summary>
        /// An inactive "prefab" with stats, resources, health and status effects.
        /// effectsFirst puts the status effect component above stats, the order that used to lose modifiers.
        /// </summary>
        public Unit CreateTemplate(string name = "Test Unit", bool effectsFirst = false, Faction faction = null)
        {
            var go = new GameObject(name);
            go.SetActive(false); // nothing Awakes until a clone is spawned
            go.transform.SetParent(root.transform);
            templates.Add(go);

            if (effectsFirst) go.AddComponent<StatusEffectsComponent>();
            TestAssets.Set(go.AddComponent<StatsComponent>(), "profile", Profile);
            TestAssets.Set(go.AddComponent<ResourceComponent>(), "profile", Profile);
            TestAssets.Set(go.AddComponent<HealthComponent>(), "healthType", Health);
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

        public void Dispose()
        {
            CombatServices.EndSession();
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