using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Distance Falloff")]
    public sealed class DistanceFallofScaler : IValueScaler
    {
        [SerializeField, Min(0f), Tooltip("Full value up to this distance")] 
        private float start = 2f;
        [SerializeField, Min(0f), Tooltip("Minimum value from this distance on")] 
        private float end = 10f;
        [SerializeField, Range(0f, 1f), Tooltip("Minimum % damage multiplier. 0.25f = does 25% of the damage")] 
        private float minimum = 0.25f;

        public float Apply(float value, in EffectContext ctx)
        {
            var source = ctx.SourceUnit;
            var target = ctx.TargetUnit;
            if(source == null || target == null) return value;
            Vector3 point = ctx.Hit.Point != Vector3.zero ? ctx.Hit.Point : target.Transform.position;
            float t = Mathf.InverseLerp(start, Mathf.Max(start + 0.01f, end), Vector3.Distance(source.Transform.position, point));
            return value * Mathf.Lerp(1f, minimum, t);
        }
    }
}