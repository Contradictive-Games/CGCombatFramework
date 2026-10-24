using UnityEngine;

namespace CG.CombatFramework
{
    public enum EmitFrom
    {
        Target, SourceAim, HitPoint
    }

    [System.Serializable, TypeMenu("Flow/Emit At")]
    public sealed class EmitAtPayload : IPayload
    {
        [SerializeField] private EmitFrom emitFrom = EmitFrom.SourceAim;
        [SerializeReference, SelectType]
        private IEmission[] emissions = System.Array.Empty<IEmission>();

        public void Apply(in EffectContext ctx)
        {
            switch (emitFrom)
            {
                case EmitFrom.Target:
                    EmitAtTarget(ctx);
                    break;
                case EmitFrom.SourceAim:
                    EmitAtSourceAim(ctx);
                    break;
                case EmitFrom.HitPoint:
                    EmitAtHitPoint(ctx);
                    break;
            }
        }

        private void EmitAtTarget(in EffectContext ctx)
        {
            var target = ctx.TargetUnit;
            if (target == null) return;
            var tf = target.Transform;
            var effect = ctx.Nested(null);
            var delivery = new DeliveryContext(in effect, tf.position, tf.forward, tf.position);
            Emissions.EmitAll(emissions, in delivery);

        }

        private void EmitAtHitPoint(in EffectContext ctx)
        {
            var target = ctx.TargetUnit;
            if (target == null) return;
            var tf = target.Transform;
            var effect = ctx.Nested(null);
            var delivery = new DeliveryContext(in effect, tf.position, tf.forward, tf.position);
            Emissions.EmitAll(emissions, in delivery);

        }

        private void EmitAtSourceAim(in EffectContext ctx)
        {
            var source = ctx.SourceUnit;
            if (source == null) return;
            var aim = DeliveryContext.FromUnit(source, ctx.Cause, ctx.Power, ctx.Random);
            var effect = ctx.Nested(null);
            var delivery = new DeliveryContext(in effect, aim.Origin, aim.Direction, aim.AimPoint);
            Emissions.EmitAll(emissions, in delivery);
        }
    }
}
