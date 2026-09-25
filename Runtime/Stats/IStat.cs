namespace CG.RGS.CombatFramework
{
    public interface IStat
    {
        public float Value { get; }
        public float MaxValue { get; }
        public float MinValue { get; }


        public void AddModifier(StatModifier modifier);
        public void RemoveModifier(StatModifier modifier);
        public void RemoveAllModifiersFromSource(object source);
    }
}