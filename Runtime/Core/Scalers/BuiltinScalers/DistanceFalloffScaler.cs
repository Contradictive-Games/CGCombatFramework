using UnityEngine;

namespace CG.CombatFramework
{
    public enum FallOffFrom { Attacker, QueryOrigin }

    [System.Serializable, TypeMenu("Distance Falloff")]
    [BlockInfo("Full value up to Start, falling to Minimum (a share of the value) at End. Measures from the attacker, or from the query origin such as a blast center.", Example = "Grenade: full damage within 1m, 25% at 4m")]
    public sealed class DistanceFalloffScaler : IValueScaler
    {
        [SerializeField, Tooltip("Attacker: from source unit pos (guns) \nQuery Origin: from where query started (blast's center, projectile's launch, chain's first target)")]
        private FallOffFrom measureFrom = FallOffFrom.Attacker;
        [SerializeField, Min(0f), Tooltip("Full value up to this distance")] 
        private float start = 2f;
        [SerializeField, Min(0f), Tooltip("Minimum value from this distance on")] 
        private float end = 10f;
        [SerializeField, Range(0f, 1f), Tooltip("Minimum % damage multiplier. 0.25f = does 25% of the damage")] 
        private float minimum = 0.25f;

        public float Apply(float value, in EffectContext ctx)
        {
            float distance;
            if(measureFrom == FallOffFrom.QueryOrigin) distance = ctx.Hit.Distance;
            else
            {
                var source = ctx.SourceUnit;
                var target = ctx.TargetUnit;
                if(source == null || target == null) return value;
                Vector3 point = ctx.Hit.Point != Vector3.zero ? ctx.Hit.Point : target.Transform.position;
                distance = Vector3.Distance(source.Transform.position, point);
            }
            float t = Mathf.InverseLerp(start, Mathf.Max(start + 0.01f, end), distance);
            return value * Mathf.Lerp(1f, minimum, t);
        }
    }
}