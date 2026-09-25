using System;

namespace CG.RGS.CombatFramework
{
    public interface IResource
    {
        public ResourceType ResourceType { get; }
        public float MaxValue { get; }
        public float CurrentValue { get; }

        public event Action<IResource, float> OnChange;
        public event Action<IResource> OnDeplete;
    }
}