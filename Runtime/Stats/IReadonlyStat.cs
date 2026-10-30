namespace CG.CombatFramework
{
    public interface IReadonlyStat
    {
        public StatType Type { get; }
        public float BaseValue { get; }

        public float Value { get; }

        public void AddListener(IStatListener listener);
        public void RemoveListener(IStatListener listener);
    }
}