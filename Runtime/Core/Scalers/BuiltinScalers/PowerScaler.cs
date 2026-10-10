using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Power")]
    public sealed class PowerScaler : IValueScaler
    {
        [SerializeField, Tooltip("0 = ignore power, 1 = linear")] private float weight = 1f;

        public float Apply(float value, in EffectContext ctx) => value * Mathf.LerpUnclamped(1f, ctx.Power, weight);
    }
}