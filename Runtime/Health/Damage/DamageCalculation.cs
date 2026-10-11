namespace CG.CombatFramework
{
    public struct DamageCalculation
    {
        public readonly DamageRequest Request;
        public readonly IUnit TargetUnit;
        public float Amount;
        public float Absorbed;
        public bool IsCrit;
        public HitResult Result;

        public DamageCalculation(in DamageRequest request, IUnit targetUnit)
        {
            Request = request;
            TargetUnit = targetUnit;
            Amount = request.Amount;
            Absorbed = 0f;
            IsCrit = false;
            Result = HitResult.Hit;
        }

        public bool IsCancelled => Result != HitResult.Hit;

        public void Cancel(HitResult result)
        {
            Result = result;
            Amount = 0f;
        }
    }
}