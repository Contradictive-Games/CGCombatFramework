namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Resources/Restore or Drain")]
    public sealed class ModifyResourcePayload : IPayload
    {
        [UnityEngine.SerializeField] private ResourceType resource;
        [UnityEngine.SerializeField] private ScalableValue amount = new(10f);
        [UnityEngine.SerializeField, UnityEngine.Tooltip("If we want to remove the amount instead of restore")]
        private bool drain;

        public void Apply(in EffectContext ctx)
        {
            var target = ctx.TargetUnit;
            if(target == null || !target.TryGet<IResourceProvider>(out var provider) || !provider.TryGet(resource, out var res)) return;

            float value = amount.Evaluate(in ctx);
            if(value <= 0f) return;
            if(drain) res.Remove(value, ChangeReason.Damage, in ctx);
            else res.Add(value, ChangeReason.Restore, in ctx);
        }
    }
}