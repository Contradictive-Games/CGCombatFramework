using System.Collections.Generic;

namespace CG.CombatFramework.Tests
{
    /// <summary>An IResourceProvider holding real Resource objects, with no component or profile.</summary>
    public sealed class FakeResources : IResourceProvider
    {
        private readonly Dictionary<ResourceType, Resource> resources = new();

        public IEnumerable<IReadonlyResource> All => resources.Values;

        public Resource Add(ResourceType type, float max, float current, params IRegenRule[] regen)
        {
            var resource = new Resource(type, max, regen);
            resource.ResetTo(current);
            resources[type] = resource;
            return resource;
        }

        public bool TryGet(ResourceType type, out IResource resource)
        {
            if (type != null && resources.TryGetValue(type, out var found))
            {
                resource = found;
                return true;
            }
            resource = null;
            return false;
        }
    }
}