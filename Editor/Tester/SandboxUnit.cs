using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal sealed class SandboxUnit : IUnit, IDisposable
    {
        private readonly GameObject gameObject;
        private Faction faction;

        public SandboxUnit(string name, ulong id)
        {
            gameObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            DisplayName = name;
            Id = new UnitId(id);
        }

        public string DisplayName { get; private set; }
        public Transform Transform => gameObject.transform;
        public Faction Faction => faction;
        public TagContainer Tags { get; private set; } = new();
        public EventBus Events { get; } = new();
        public UnitId Id { get; }
        public IUnit Owner => null;
        public bool IsAlive => true;
        public bool IsSpawned => true;
        public StatBlock Stats { get; private set; } = new(null);
        public SandboxResources Resources { get; } = new();
        public UnitStatProfile Profile { get; private set; }

        public event Action<IUnit> OnSpawned { add { } remove { } }
        public event Action<IUnit, DespawnReason> OnDespawned { add { } remove { } }

        public void Load(SandboxSetup setup, string fallbackName)
        {
            Profile = setup.EffectiveProfile;
            DisplayName = setup.Unit != null ? StudioKinds.NameOf(setup.Unit) : fallbackName;
            faction = setup.EffectiveFaction;

            Stats = new StatBlock(Profile);
            foreach(var each in setup.Stats ?? Array.Empty<StatValue>())
            {
                if(each.Stat != null) Stats.GetOrCreate(each.Stat).SetBaseValue(each.Value);
            }

            Tags = new TagContainer();
            foreach(var tag in UnitLibrary.TagsOf(setup.Unit))
            {
                Tags.Add(tag);
            }
            foreach(var tag in setup.Tags ?? Array.Empty<CombatTag>())
            {
                Tags.Add(tag);
            }

            var resourceProfile = setup.Profile != null ? setup.Profile : UnitLibrary.ResourceProfileOf(setup.Unit);
            Resources.Load(resourceProfile, Stats, setup.Resources);
        }

        public bool TryGet<T>(out T capability) where T : class
        {
            if(Stats is T stats)
            {
                capability = stats;
                return true;
            }
            if(Resources is T resources)
            {
                capability = resources;
                return true;
            }
            capability = null;
            return false;
        }

        public void Dispose() => Object.DestroyImmediate(gameObject);
    }

    internal sealed class SandboxResources : IResourceProvider
    {
        private readonly Dictionary<ResourceType, Resource> resources = new();
        private readonly Dictionary<ResourceType, float> starts = new();

        public IEnumerable<IReadonlyResource> All => resources.Values;

        public void Load(UnitStatProfile profile, StatBlock stats, ResourceValue[] overrides)
        {
            foreach(var resource in resources.Values)
            {
                resource.UnbindMaxStat();
            }
            resources.Clear();
            starts.Clear();

            if(profile != null)
            {
                foreach(var entry in profile.Resources ?? Array.Empty<ResourceEntry>())
                {
                    if(entry == null || entry.Type == null || resources.ContainsKey(entry.Type)) continue;
                    var resource = entry.CreateRuntime();
                    if(entry.Type.MaxStat != null) resource.BindMaxStat(stats.GetOrCreate(entry.Type.MaxStat));
                    resources.Add(entry.Type, resource);
                    starts[entry.Type] = entry.StartValue(resource.Max);
                }
            }

            foreach(var each in overrides ?? Array.Empty<ResourceValue>())
            {
                if(each.Resource == null) continue;
                if(!resources.ContainsKey(each.Resource)) resources.Add(each.Resource, new Resource(each.Resource, Mathf.Max(0f, each.Current)));
                starts[each.Resource] = each.Current;
            }
            Reset();
        }

        public void Reset()
        {
            foreach(var pair in starts)
            {
                resources[pair.Key].ResetTo(pair.Value);
            }
        }

        public float Current(ResourceType type) => type != null && resources.TryGetValue(type, out var found) ? found.Current : -1f;
        public float Max(ResourceType type) => type != null && resources.TryGetValue(type, out var found) ? found.Max : -1f;

        public bool TryGet(ResourceType type, out IResource resource)
        {
            if(type != null && resources.TryGetValue(type, out var found))
            {
                resource = found;
                return true;
            }
            resource = null;
            return false;
        }
    }

    internal sealed class TesterRandom : IGameRandom
    {
        private readonly RollMode mode;
        private readonly StringBuilder rolls = new();
        private System.Random random;

        public TesterRandom(RollMode mode, int seed)
        {
            this.mode = mode;
            random = new System.Random(seed);
        }

        public bool Recording { get; set; }

        public void Reseed(int seed) => random = new System.Random(seed);

        public string TakeRolls()
        {
            string text = rolls.ToString();
            rolls.Clear();
            return text;
        }

        public float Value() => mode == RollMode.Random ? (float)random.NextDouble() : 0.5f;

        public float Range(float min, float max) => min + Value() * (max - min);

        public bool Roll(float chance01)
        {
            bool success;
            switch(mode)
            {
                case RollMode.AlwaysSucceed:
                    success = chance01 > 0f;
                    break;
                case RollMode.AlwaysFail:
                    success = chance01 >= 1f;
                    break;
                default:
                    success = chance01 > 0f && (chance01 >= 1f || Value() < chance01);
                    break;
            }
            if(Recording)
            {
                if(rolls.Length > 0) rolls.Append(", ");
                rolls.Append(Mathf.RoundToInt(Mathf.Clamp01(chance01) * 100f)).Append("% roll: ").Append(success ? "yes" : "no");
            }
            return success;
        }
    }
}
