namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Deal Damage")]
    public sealed class DamagePayload : IPayload
    {
        [UnityEngine.Header("Damage Settings")]
        [UnityEngine.SerializeField] private ScalableValue amount = new(10f);
        [UnityEngine.SerializeField] private DamageType type;
        [UnityEngine.SerializeField] private DamageFlags flags;

        public void Apply(in EffectContext ctx)
        {
            var target = ctx.TargetUnit;
            if(target == null || !target.TryGet<IHealth>(out var health)) return;
            float value = amount.Evaluate(in ctx);
            if(value > 0f) health.ApplyDamage(new DamageRequest(value, type, in ctx, flags));
        }
    }
}