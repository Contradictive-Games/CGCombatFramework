using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Stacks")]
    public sealed class StacksScaler : IValueScaler
    {
        [SerializeField, Tooltip("Bonus per stack after the first. 1 = 3 stacks deal 3x, 0.5 = 3 stacks deal 2x")]
        private float perExtraStack = 1f;

        public float Apply(float value, in EffectContext ctx) => value * (1f + Mathf.Max(0, ctx.Stacks - 1) * perExtraStack);
    }
}