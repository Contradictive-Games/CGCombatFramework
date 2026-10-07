using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Conditions/Not")]
    public sealed class NotCondition : ICondition
    {
        [SerializeReference, SelectType] private ICondition condition;

        public bool Evaluate(in EffectContext ctx) => condition == null || !condition.Evaluate(in ctx);
    }
}