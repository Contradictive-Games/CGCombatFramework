using UnityEngine;

namespace CG.CombatFramework
{
    public enum ThresholdComparison
    {
        Below,
        AtOrAbove
    }

    [System.Serializable, TypeMenu("Resource Threshold")]
    public sealed class ResourceThresholdCondition : ICondition
    {
        [SerializeField, Tooltip("Who we are checking the resource of")] private Subject subject = Subject.Target;
        [SerializeField] private ResourceType resource;
        [SerializeField] private ThresholdComparison comparison = ThresholdComparison.Below;
        [SerializeField, Range(0f, 1f), Tooltip("The % threshold we are checking against")] private float fraction = 0.3f;

        public bool Evaluate(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit == null || !unit.TryGet<IResourceProvider>(out var provider) || !provider.TryGet(resource, out var r)) return false;
            return comparison == ThresholdComparison.Below ? r.Normalized < fraction : r.Normalized >= fraction;
        }
    }
}