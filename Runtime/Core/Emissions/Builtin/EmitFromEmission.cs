using System;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum EmitDirection { Keep, Down, TowardAimPoint, TowardTarget }

    [Serializable, TypeMenu("Flow/Move Origin")]
    public sealed class EmitFromEmission : IEmission
    {
        [SerializeField] private QueryCenter from = QueryCenter.AimPoint;
        [SerializeField] private Vector3 offset;
        [SerializeField] private EmitDirection direction = EmitDirection.Keep;
        [SerializeReference, SelectType] private IEmission[] emissions = Array.Empty<IEmission>();

        public void Emit(in DeliveryContext ctx)
        {
            Vector3 point = Point(in ctx, from) + offset;
            Vector3 dir = Direction(in ctx, point, direction);
            var moved = new DeliveryContext(in ctx.Effect, point, dir, ctx.AimPoint);
            for(int i = 0; i < emissions.Length; i++)
            {
                emissions[i]?.Emit(in moved);
            }
            Emissions.EmitAll(emissions, in moved);
        }

        private static Vector3 Point(in DeliveryContext ctx, QueryCenter queryCenter)
        {
            switch (queryCenter)
            {
                case QueryCenter.AimPoint: return ctx.AimPoint;
                case QueryCenter.Source:
                    var source = ctx.Effect.SourceUnit;
                    return source != null ? source.Transform.position : ctx.Origin;
                case QueryCenter.Target:
                    var target = ctx.Effect.TargetUnit;
                    return target != null ? target.Transform.position : ctx.Origin;
                default: return ctx.Origin;
            }
        }

        private static Vector3 Direction(in DeliveryContext ctx, Vector3 point, EmitDirection emitDirection)
        {
            Vector3 toward;
            switch (emitDirection)
            {
                case EmitDirection.Down: return Vector3.down;
                case EmitDirection.TowardAimPoint:
                    toward = ctx.AimPoint - point;
                    break;
                case EmitDirection.TowardTarget:
                    var target = ctx.Effect.TargetUnit;
                    toward = target != null ? target.Transform.position - point : ctx.Direction;
                    break;
                default: return ctx.Direction;
            }
            return toward.sqrMagnitude > 0.0001f ? toward.normalized : ctx.Direction;
        }
    }
}
