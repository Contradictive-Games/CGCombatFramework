using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu("CG Combat Framework/Resource Component")]
    public sealed class ResourceComponent : MonoBehaviour, IResourceProvider, IUnitComponent
    {
        [SerializeField] private UnitStatProfile profile;

        private readonly Dictionary<ResourceType, Resource> resources = new();
        private IUnit owner;
        public IEnumerable<IReadonlyResource> All => resources.Values;

        public bool TryGet(ResourceType type, out IResource resource)
        {
            bool found = resources.TryGetValue(type, out var res);
            resource = res;
            return found;
        }

        public void Initialize(IUnit owner)
        {
            this.owner = owner;
            if(profile == null) return;
            foreach(var entry in profile.Resources)
            {
                if(entry?.Type != null) resources[entry.Type] = entry.CreateRuntime();
            }
        }

        public void Bind()
        {
            if(profile == null) return;
            owner.TryGet<IStatProvider>(out var stats);
            foreach(var entry in profile.Resources)
            {
                if(entry?.Type == null) continue;
                var resource = resources[entry.Type];
                if(stats != null && entry.Type.MaxStat != null)
                {
                    resource.BindMaxStat(stats.GetOrCreate(entry.Type.MaxStat));
                }
                resource.Set(entry.StartValue(resource.Max));
            }
        }

        public void Unbind()
        {
            foreach(var resource in resources.Values)
            {
                resource.UnbindMaxStat();
            }
        }

        public void Tick(float deltaTime)
        {
            foreach(var resource in resources.Values)
            {
                resource.Tick(deltaTime);
            }
        }

        public void OnSpawned(in SpawnData data)
        {
            throw new System.NotImplementedException();
        }

        public void OnDespawned(DespawnReason reason)
        {
            throw new System.NotImplementedException();
        }
    }

}