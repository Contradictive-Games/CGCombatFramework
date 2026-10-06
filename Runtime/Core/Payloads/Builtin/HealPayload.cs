namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Heal")]
    public sealed class HealPayload : IPayload
    {
        [UnityEngine.Header("Heal Settings")]
        [UnityEngine.SerializeField] private ScalableValue amount = new(10f);

        public void Apply(in EffectContext ctx)
        {
            var target = ctx.TargetUnit;
            if(target == null || !target.TryGet<IHealth>(out var health)) return;

            float value = amount.Evaluate(in ctx);
            if(value > 0f) health.ApplyHeal(value, in ctx);
        }
    }
}