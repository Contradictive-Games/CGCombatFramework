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

        public void AddListener(IResourceListener listener);
        public void RemoveListener(IResourceListener listener);
    }
}
