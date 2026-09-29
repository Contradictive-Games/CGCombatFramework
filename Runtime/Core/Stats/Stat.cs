namespace CG.RGS.CombatFramework
{
    public sealed class Stat : IStat
    {
        public float BaseValue { get; private set; }
        public float MinValue { get; private set; }
        public float MaxValue { get; private set; }

        public float Value
        {
            get;
        }

        private bool isDirty;



        private float CalculateFinalValue()
        {
            isDirty = false;
            return 1f;
        }


        public void AddModifier(StatModifier modifier)
        {
            
        }

        public void RemoveModifier(StatModifier modifier)
        {
            
        }

        public void RemoveAllModifiersFromSource(object source)
        {
            
        }
    }
}