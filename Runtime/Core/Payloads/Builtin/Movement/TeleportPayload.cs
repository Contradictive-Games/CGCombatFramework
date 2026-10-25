using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    public interface ITeleportReceiver
    {
        public void Teleport(Vector3 position, in EffectContext ctx);
    }

    public enum TeleportTo { HitPoint, BegindTarget, SwapWithTarget }

    [System.Serializable, TypeMenu("Movement/Teleport")]
    public sealed class TeleportPayload : IPayload
    {
        [FormerlySerializedAs("who")]
        [SerializeField, Tooltip("Who will receive this")] private Subject subject = Subject.Source;
        [SerializeField, Tooltip("Hit Point: where hit landed (use Emit from AimPoint -> payloads to blink to cursor)"
            + "\nBehind Target: a step begind the target, facing it"
            + "\nSwap: trade places with the atrget")]
        private TeleportTo to = TeleportTo.HitPoint;
        [SerializeField, Min(0f), Tooltip("Behind target only")] private float distanceBehind = 1.5f;

        public void Apply(in EffectContext ctx)
        {
            var unit =ctx.GetUnit(subject);
            if(unit == null) return;
            var target =ctx.TargetUnit;
            switch (to)
            {
                case TeleportTo.HitPoint:
                    Move(unit, ctx.Hit.Point, in ctx);
                    break;
                case TeleportTo.BegindTarget:
                    if(target == null || ReferenceEquals(target, unit)) return;
                    var behind = target.Transform.position - target.Transform.forward * distanceBehind;
                    Move(unit, behind, in ctx);
                    break;
                case TeleportTo.SwapWithTarget:
                    var other = ReferenceEquals(unit, target) ? ctx.SourceUnit : target;
                    if(other == null || ReferenceEquals(other, unit)) return;
                    Vector3 a = unit.Transform.position;
                    Move(unit, other.Transform.position, in ctx);
                    Move(other, a, in ctx);
                    break;
            }
        }

        private static void Move(IUnit unit, Vector3 position, in EffectContext ctx)
        {
            if(unit.TryGet<ITeleportReceiver>(out var receiver)) receiver.Teleport(position, in ctx);
            else unit.Transform.position = position;
        }
    }
}
