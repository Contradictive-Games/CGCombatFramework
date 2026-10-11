namespace CG.CombatFramework
{
    public interface IStat : IReadonlyStat
    {
        public void SetBaseValue(float value);
        public void AddModifier(StatModifier modifier);
        public bool TryRemoveModifier(StatModifier modifier);
        public int RemoveAllModifiersFromSource(object source);
    }
}