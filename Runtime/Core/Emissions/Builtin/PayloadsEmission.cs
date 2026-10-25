using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Payloads At Point")]
    [BlockInfo("Runs payloads once at the emission origin, with no target. Use it for payloads that work on the hit point.", Example = "Blink: Move Origin to Aim Point, then Payloads At Point with Teleport (Source, Hit Point)")]
    public sealed class PayloadsEmission : IEmission
    {
        [SerializeReference, SelectType] private IPayload[] payloads = System.Array.Empty<IPayload>();

        public void Emit(in DeliveryContext ctx)
        {
            var e = ctx.Effect;
            var normal = e.Hit.Normal != Vector3.zero ? e.Hit.Normal : Vector3.up;
            var here = e.Retarget(null, new HitData(ctx.Origin, normal, ctx.Direction));
            Payloads.ApplyAll(payloads, in here);
        }
    }
}
