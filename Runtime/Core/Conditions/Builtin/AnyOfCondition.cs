using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Any Of")]
    [BlockInfo("Passes when at least one condition inside it passes. An empty list passes.", Example = "Has Tag Burning or Has Tag Poisoned for a bonus against any damage-over-time target")]
    public sealed class AnyOfCondition : ICondition
    {
        [SerializeReference, SelectType] private ICondition[] conditions = System.Array.Empty<ICondition>();

        public bool Evaluate(in EffectContext ctx) => conditions.Any(in ctx);
    }
}