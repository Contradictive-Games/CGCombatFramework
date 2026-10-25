using UnityEngine;

namespace CG.CombatFramework
{
    public enum ResourcePart { Max, Current, Missing }

    [System.Serializable, TypeMenu("Percent of Resource")]
    [BlockInfo("Adds Percent of the subject's resource (max, current or missing).", Example = "Extra damage equal to 5% of the target's max Health")]
    public sealed class PercentOfResourceScaler : IValueScaler
    {
        [SerializeField] private Subject subject = Subject.Target;
        [SerializeField] private ResourceType resource;
        [SerializeField] private ResourcePart part = ResourcePart.Max;
        [SerializeField, Tooltip("0.1 = adds 10% of it")] private float percent = 0.1f;

        public float Apply(float value, in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit == null || !unit.TryGet<IResourceProvider>(out var provider) || !provider.TryGet(resource, out var r)) return value;

            float amount = part switch
            {
                ResourcePart.Current => r.Current,
                ResourcePart.Missing => r.Max - r.Current,
                _ => r.Max
            };
            return value + amount * percent;
        }
    }
}