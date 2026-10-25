using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Proc Depth Below")]
    [BlockInfo("Passes when this effect is fewer than Below trigger levels deep. 1 = direct hits only, not hits caused by triggers.", Example = "Stops an on-hit explosion from setting off more on-hit explosions")]
    public sealed class ProcDepthCondition : ICondition
    {
        [SerializeField, Min(1), Tooltip("1 = only direct hits")]
        private int below = 1;

        public bool Evaluate(in EffectContext ctx) => ctx.ProcDepth < below;
    }
}
