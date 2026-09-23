using System.Collections.Generic;

namespace CG.RGS.CombatFramework
{
    public sealed class ResourceSet
    {
        readonly Dictionary<ResourceType, Resource> resources;


        public void Add(Resource resource) => resources.Add(resource.Type, resource);
        public bool Has(ResourceType type) => resources.ContainsKey(type);
        public Resource Get(ResourceType type) => resources[type];


        public void Tick(float deltaTime)
        {
            foreach(var resource in resources.Values)
            {
                resource.Tick(deltaTime);
            }
        }
    }
}