using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Resource Component")]
    public sealed class ResourceComponent : MonoBehaviour, IResourceProvider, IUnitComponent, ITickable
    {
        [SerializeField] private UnitStatProfile profile;

        private readonly Dictionary<ResourceType, Resource> resources = new();
        private readonly HashSet<ResourceType> regenWhileDead = new();
        private IUnit owner;

        public IEnumerable<IReadonlyResource> All => resources.Values;

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

        public void Initialize(IUnit owner)
        {
            this.owner = owner;
            if(profile == null) return;
            foreach(var entry in profile.Resources)
            {
                if(entry == null || entry.Type == null || resources.ContainsKey(entry.Type)) continue;
                resources.Add(entry.Type, entry.CreateRuntime());
                if(entry.RegenWhileDead) regenWhileDead.Add(entry.Type);
            }
        }

        public void Bind()
        {
            if(profile == null || !owner.TryGet<IStatProvider>(out var stats)) return;
            foreach(var entry in profile.Resources)
            {
                if(entry == null || entry.Type == null || entry.Type.MaxStat == null) continue;
                resources[entry.Type].BindMaxStat(stats.GetOrCreate(entry.Type.MaxStat));
            }
        }

        public void Unbind()
        {
            foreach(var resource in resources.Values)
            {
                resource.UnbindMaxStat();
            }
        }


        public void OnSpawned(in SpawnData data)
        {
            if(profile == null) return;
            foreach(var entry in profile.Resources)
            {
                if(entry == null || entry.Type == null) continue;
                var resource = resources[entry.Type];
                resource.ResetTo(entry.StartValue(resource.Max));
            }
        }

        public void OnDespawned(DespawnReason reason) { }

        public void Tick(float deltaTime)
        {
            bool dead = owner != null && !owner.IsAlive;
            foreach(var pair in resources)
            {
                if(dead && !regenWhileDead.Contains(pair.Key)) continue;
                pair.Value.Tick(deltaTime);
            }
        }
    }

}