using System;

namespace CG.CombatFramework
{
    public interface IReadonlyResource
    {
        public ResourceType Type { get; }
        public float Current { get; }
        public float Max { get; }
        public float Normalized { get; }

        public bool IsDepleted { get; }
        public bool IsFull { get; }

        public event Action<ResourceChange> OnChanged;
        public event Action<ResourceChange> OnDepleted;
    }
}
