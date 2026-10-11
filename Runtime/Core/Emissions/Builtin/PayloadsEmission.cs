using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Payloads (emission origin)")]
    public sealed class PayloadsEmission : IEmission
    {
        [SerializeReference, SelectType] private IPayload[] payloads = System.Array.Empty<IPayload>();

        public void Emit(in DeliveryContext ctx)
        {
            var e = ctx.Effect;
            var normal = e.Hit.Normal != Vector3.zero ? e.Hit.Normal : Vector3.up;
            var here = new EffectContext(e.SourceUnit, e.Cause, null, new HitData(ctx.Origin, normal, ctx.Direction),
                e.Power, e.Stacks, e.ProcDepth, e.Random);
            Payloads.ApplyAll(payloads, in here);
        }
    }
}