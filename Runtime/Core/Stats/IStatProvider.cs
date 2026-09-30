namespace CG.CombatFramework
{
    public interface IStatProvider
    {
        public bool TryGetStat(StatType type, out IStat stat);
        public float GetValue(StatType type, float fallback = 0f);
        public IStat GetOrCreate(StatType type);
    }
}