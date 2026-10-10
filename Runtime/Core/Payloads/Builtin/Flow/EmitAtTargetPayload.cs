using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/Emit At Target")]
    public sealed class EmitAtTargetPayload : IPayload
    {
        [SerializeReference, SelectType] private IEmission[] emissions = System.Array.Empty<IEmission>();

        public void Apply(in EffectContext ctx)
        {
            var target = ctx.TargetUnit;
            if(target == null) return;
            var tf = target.Transform;
            var effect = new EffectContext(ctx.SourceUnit, ctx.Cause, null, default, ctx.Power, ctx.Stacks, ctx.ProcDepth + 1, ctx.Random);
            var delivery = new DeliveryContext(in effect, tf.position, tf.forward, tf.position);
            for(int i =0; i < emissions.Length; i++)
            {
                emissions[i]?.Emit(in delivery);
            }
        }
    }
}