using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Any Of")]
    public sealed class AnyOfCondition : ICondition
    {
        [SerializeReference, SelectType] private ICondition[] conditions = System.Array.Empty<ICondition>();

        public bool Evaluate(in EffectContext ctx) => conditions.Any(in ctx);
    }
}