using System.Collections.Generic;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/Apply To Source")]
    public sealed class ApplyToSourcePayload : IPayload
    {
        [UnityEngine.SerializeReference, SelectType] private List<IPayload> payloads = new();

        public void Apply(in EffectContext ctx)
        {
            var source = ctx.SourceUnit;
            if(source == null) return;
            var self = new EffectContext(source, ctx.Cause, source, ctx.Hit, ctx.Power, ctx.Stacks, ctx.ProcDepth, ctx.Random);
            Payloads.ApplyAll(payloads, in self);
        }
    }
}