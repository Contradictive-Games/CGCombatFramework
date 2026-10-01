using UnityEngine;

namespace CG.CombatFramework
{
    public interface ICondition
    {
        public bool Evaluate(in EffectContext ctx);
    }
}
