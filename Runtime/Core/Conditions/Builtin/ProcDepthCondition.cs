using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Proc Depth Below")]
    public sealed class ProcDepthCondition : ICondition
    {
        [SerializeField, Min(1), Tooltip("1 = only direct hits")]
        private int below = 1;

        public bool Evaluate(in EffectContext ctx) => ctx.ProcDepth < below;
    }
}
