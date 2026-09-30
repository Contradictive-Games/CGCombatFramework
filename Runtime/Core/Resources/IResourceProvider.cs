using System.Collections.Generic;

namespace CG.CombatFramework
{
    public interface IResourceProvider
    {
        public bool TryGet(ResourceType type, out IResource resource);
        public IEnumerable<IReadonlyResource> All { get; }
    }
}