using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Power")]
    [BlockInfo("Scales by the context's Power: charge level, channel curve, or the power stored on a status effect. Weight 0 ignores it, 1 is linear.", Example = "Charged shot where a full charge does full damage")]
    public sealed class PowerScaler : IValueScaler
    {
        [SerializeField, Tooltip("0 = ignore power, 1 = linear")] private float weight = 1f;

        public float Apply(float value, in EffectContext ctx) => value * Mathf.LerpUnclamped(1f, ctx.Power, weight);
    }
}