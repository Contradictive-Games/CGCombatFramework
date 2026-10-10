using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("All Of")]
    public sealed class AllOfCondition : ICondition
    {
        [SerializeReference, SelectType] private ICondition[] conditions = System.Array.Empty<ICondition>();

        public bool Evaluate(in EffectContext ctx) => conditions.All(in ctx);
    }
}