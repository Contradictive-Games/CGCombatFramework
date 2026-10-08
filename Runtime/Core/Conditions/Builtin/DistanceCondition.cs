using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Within Distance Range")]
    public sealed class DistanceCondition : ICondition
    {
        [SerializeField, Min(0f)] private float minDistance = 0f;
        [SerializeField, Min(0f)] private float maxDistance = 10f;

        public bool Evaluate(in EffectContext ctx)
        {
            var src = ctx.SourceUnit;
            var target = ctx.TargetUnit;
            if(src == null || target == null) return false;
            float distance = Vector3.Distance(src.Transform.position, target.Transform.position);
            return distance >= minDistance && distance <= maxDistance;
        }
    }
}