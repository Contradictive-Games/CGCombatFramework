namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/Apply To Source")]
    public sealed class ApplyToSourcePayload : IPayload
    {
        [UnityEngine.SerializeReference, SelectType] private IPayload[] payloads = System.Array.Empty<IPayload>();

        public void Apply(in EffectContext ctx)
        {
            var source = ctx.SourceUnit;
            if (source == null) return;
            var self = ctx.Retarget(source, ctx.Hit);
            Payloads.ApplyAll(payloads, in self);
        }
    }
}