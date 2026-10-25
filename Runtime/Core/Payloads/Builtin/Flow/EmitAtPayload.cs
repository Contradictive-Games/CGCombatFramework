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
            if(!TryGetOrigin(in ctx, out var origin, out var direction, out var aimPoint)) return;
            var effect = ctx.Nested(null);
            emissions.EmitAll(new DeliveryContext(in effect, origin, direction, aimPoint));
        }

        private bool TryGetOrigin(in EffectContext ctx, out Vector3 origin, out Vector3 direction, out Vector3 aimPoint)
        {
            switch (emitFrom)
            {
                case EmitFrom.Target:
                    var target = ctx.TargetUnit;
                    if(target == null) break;
                    origin = aimPoint = target.Transform.forward;
                    direction = target.Transform.forward;
                    return true;
                case EmitFrom.SourceAim:
                    var source = ctx.SourceUnit;
                    if(source == null) break;
                    var aim = DeliveryContext.FromUnit(source, ctx.Cause);
                    origin = aim.Origin;
                    direction = aim.Direction;
                    aimPoint = aim.AimPoint;
                    return true;
                case EmitFrom.HitPoint:
                    origin = aimPoint = ctx.Hit.Point;
                    direction = ctx.Hit.Direction.sqrMagnitude > 0.0001f ? ctx.Hit.Direction : Vector3.forward;
                    return true;
            }
            origin = direction = aimPoint = default;
            return false;
        }
    }
}
