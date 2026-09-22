namespace CG.RGS.CombatFramework
{
    public readonly struct DamageContext
    {
        public readonly int Value;
        public readonly DamageType Type;
        public readonly float CritDamageMultiplier;
        public readonly float LinearDistance;

        public DamageContext(int value, DamageType type, float linearDistance, float critDamageMultiplier = 1f)
        {
            Value = value;
            Type = type;
            LinearDistance = linearDistance;
            CritDamageMultiplier = critDamageMultiplier;
        }
    }
}