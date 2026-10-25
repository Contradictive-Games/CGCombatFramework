using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("All Of")]
    [BlockInfo("Passes only when every condition inside it passes. An empty list passes.", Example = "Inside Not: Has Tag Stunned + Has Tag Silenced blocks casting only while both are present")]
    public sealed class AllOfCondition : ICondition
    {
        [SerializeReference, SelectType] private ICondition[] conditions = System.Array.Empty<ICondition>();

        public bool Evaluate(in EffectContext ctx) => conditions.All(in ctx);
    }
}