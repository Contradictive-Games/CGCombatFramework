namespace CG.CombatFramework
{
    public readonly struct DamageResult
    {
        public readonly DamageRequest Request;
        public readonly HitResult Result;
        public readonly float Dealt;
        public readonly float Absorbed;
        public readonly float Overkill;
        public readonly bool IsCrit;
        public readonly bool IsKillingBlow;

        public DamageResult(in DamageRequest req, HitResult result, float dealt, float absorbed, float overkill, bool isCrit, bool isKillingBlow)
        {
            Request = req;
            Result = result;
            Dealt = dealt;
            Absorbed = absorbed;
            Overkill = overkill;
            IsCrit = isCrit;
            IsKillingBlow = isKillingBlow;
        }

        public static DamageResult Ignored(in DamageRequest req) => new DamageResult(in req, HitResult.Immune, 0f, 0f, 0f, false, false);
        public float Total => Dealt + Absorbed;
    }
}