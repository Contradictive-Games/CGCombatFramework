using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Within Distance Range")]
    [BlockInfo("Passes when the distance between the source and the target is between Min and Max.", Example = "Only knock back targets within 3m of the caster")]
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