using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Any Of")]
    public sealed class AnyOfCondition : ICondition
    {
        [SerializeReference, SelectType] private List<ICondition> conditions = new();

        public bool Evaluate(in EffectContext ctx) => conditions.Any(in ctx);
    }
}