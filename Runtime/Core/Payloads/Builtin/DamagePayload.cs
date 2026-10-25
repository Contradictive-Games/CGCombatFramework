namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Deal Damage")]
    [BlockInfo("Sends damage to the subject, which runs through their damage pipeline.", Example = "12 Physical damage to the target")]
    public sealed class DamagePayload : IPayload
    {
        [UnityEngine.Header("Damage Settings")]
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Who will receive this")]
        private Subject subject = Subject.Target;
        [UnityEngine.SerializeField] private ScalableValue amount = new(10f);
        [UnityEngine.SerializeField] private DamageType type;
        [UnityEngine.SerializeField] private DamageFlags flags;

        public void Apply(in EffectContext ctx)
        {
            var target = ctx.GetUnit(subject);
            if(target == null || !target.TryGet<IHealth>(out var health)) return;
            float value = amount.Evaluate(in ctx);
            if(value > 0f) health.ApplyDamage(new DamageRequest(value, type, in ctx, flags));
        }
    }
}
