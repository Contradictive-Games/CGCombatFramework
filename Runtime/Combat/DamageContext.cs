namespace CG.RGS.CombatFramework
{
    public readonly struct DamageContext
    {
        public readonly Unit Source;
        public readonly Unit Target;

        public readonly float Amount;
        public readonly bool IsCrit;
        public readonly bool CanCrit;
        public readonly bool IsPeriodic;
        public readonly DamageType[] DamageTypes;
        
        public readonly HitResult Result;
        public readonly float Overkill;
        public readonly bool IsKillingBlow;
    }
}