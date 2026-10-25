using System;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum EmitDirection { Keep, Down, TowardAimPoint, TowardTarget }

    [Serializable, TypeMenu("Flow/Move Origin")]
    [BlockInfo("Moves the origin to the aim point, source, target or current origin (plus Offset), optionally re-aims, then runs its emissions from there.", Example = "Meteor: Move Origin to Aim Point with offset (0, 15, 0), direction Down, then a Projectile")]
    public sealed class EmitFromEmission : IEmission
    {
        [SerializeField] private QueryCenter from = QueryCenter.AimPoint;
        [SerializeField] private Vector3 offset;
        [SerializeField] private EmitDirection direction = EmitDirection.Keep;
        [SerializeReference, SelectType] private IEmission[] emissions = Array.Empty<IEmission>();

        public void Emit(in DeliveryContext ctx)
        {
            Vector3 point = ctx.PointAt(from) + offset;
            Vector3 dir = Direction(in ctx, point, direction);
            var moved = new DeliveryContext(in ctx.Effect, point, dir, ctx.AimPoint);
            emissions.EmitAll(in moved);
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
