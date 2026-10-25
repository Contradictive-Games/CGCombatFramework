using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Stacks")]
    [BlockInfo("Multiplies by 1 + (stacks - 1) x Per Extra Stack, using the context's stacks (a status effect's stacks, or the stacks consumed).", Example = "A poison tick that grows with every stack")]
    public sealed class StacksScaler : IValueScaler
    {
        [SerializeField, Tooltip("Bonus per stack after the first. 1 = 3 stacks deal 3x, 0.5 = 3 stacks deal 2x")]
        private float perExtraStack = 1f;

        public float Apply(float value, in EffectContext ctx) => value * (1f + Mathf.Max(0, ctx.Stacks - 1) * perExtraStack);
    }
}