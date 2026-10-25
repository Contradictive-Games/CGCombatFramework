using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Not")]
    [BlockInfo("Flips the result of the condition inside it. An empty slot passes.", Example = "Not + Has Tag Immune skips immune targets")]
    public sealed class NotCondition : ICondition
    {
        [SerializeReference, SelectType] private ICondition condition;

        public bool Evaluate(in EffectContext ctx) => condition == null || !condition.Evaluate(in ctx);
    }
}