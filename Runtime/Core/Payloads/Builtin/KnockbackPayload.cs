using UnityEngine;

namespace CG.CombatFramework
{
    public interface IKnockbackReceiver
    {
        public void ApplyKnockback(Vector3 impulse, in EffectContext ctx);
    }

    public enum KnockbackDirection { AwayFromSource, AlongHit, SourceForward }

    [System.Serializable, TypeMenu("Movement/Knockback")]
    public sealed class KnockbackPayload : IPayload
    {
        [SerializeField] private ScalableValue force = new(8f);
        [SerializeField] private KnockbackDirection direction = KnockbackDirection.AwayFromSource;
        [SerializeField, Min(0f), Tooltip("Upward force added to push")] private float lift;
        [SerializeField, Tooltip("Optional target stat, 0 -> 1. 0.5 = halves the push")] private StatType resistance;


        public void Apply(in EffectContext ctx)
        {
            var target = ctx.TargetUnit;
            if(target == null || !target.TryGet<IKnockbackReceiver>(out var receiver)) return;

            float amount = force.Evaluate(in ctx) * (1f - Mathf.Clamp01(target.GetStat(resistance)));
            if(amount <= 0f) return;

            receiver.ApplyKnockback(Direction(in ctx, target, direction, lift) * amount, in ctx);
        }

        private static Vector3 Direction(in EffectContext ctx, IUnit target, KnockbackDirection dir, float lift = 0f)
        {
            var source = ctx.SourceUnit;
            Vector3 d = dir switch
            {
                KnockbackDirection.AlongHit => ctx.Hit.Direction,
                KnockbackDirection.SourceForward when source != null => source.Transform.forward,
                _ when source != null => target.Transform.position - source.Transform.position,
                _ => ctx.Hit.Direction
            };

            d.y = 0f;
            if(d.sqrMagnitude < 0.0001f) d = -target.Transform.forward;
            return (d.normalized + Vector3.up * lift).normalized;
        }
    }
}