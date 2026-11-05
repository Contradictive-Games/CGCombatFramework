using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [UnitPart("Resources", Description = "Health, shields, mana and other pools from the profile. A pool's max can follow a stat.", Color = "#E5534B", Icon = "resource", Order = -90)]
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Resource Component")]
    public sealed class ResourceComponent : MonoBehaviour, IResourceProvider, IUnitComponent, ITickable
    {
        [SerializeField] private UnitStatProfile profile;

        private readonly Dictionary<ResourceType, Resource> resources = new(ReferenceComparer<ResourceType>.Instance);
        private Resource[] ticked = System.Array.Empty<Resource>();
        private bool[] regenWhileDead = System.Array.Empty<bool>();
        private IUnit owner;

        public IEnumerable<IReadonlyResource> All => resources.Values;

        public bool TryGet(ResourceType type, out IResource resource)
        {
            if(!(type is null) && resources.TryGetValue(type, out var found))
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
            var created = new List<Resource>();
            var whileDead = new List<bool>();
            foreach(var entry in profile.Resources)
            {
                if(entry == null || entry.Type == null || resources.ContainsKey(entry.Type)) continue;
                var resource = entry.CreateRuntime(owner);
                resources.Add(entry.Type, resource);
                created.Add(resource);
                whileDead.Add(entry.RegenWhileDead);
            }
            ticked = created.ToArray();
            regenWhileDead = whileDead.ToArray();
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
            for(int i = 0; i < ticked.Length; i++)
            {
                if(dead && !regenWhileDead[i])
                {
                    ticked[i].StopRegen();
                    continue;
                }
                ticked[i].Tick(deltaTime);
            }
        }
    }

}