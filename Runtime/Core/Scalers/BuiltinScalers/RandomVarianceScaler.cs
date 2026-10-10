using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Random Variance")]
    public sealed class RandomVarianceScaler : IValueScaler
    {
        [SerializeField, Range(0f, 1f), Tooltip("% difference +/- the damage will become")] 
        private float variance = 0.1f;

        public float Apply(float value, in EffectContext ctx) => value * ctx.Random.Range(1f - variance, 1f + variance);
    }
}