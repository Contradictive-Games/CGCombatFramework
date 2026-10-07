using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Conditions/All Of")]
    public sealed class AllOfCondition : ICondition
    {
        [SerializeReference, SelectType] private List<ICondition> conditions = new();

        public bool Evaluate(in EffectContext ctx) => conditions.All(in ctx);
    }
}