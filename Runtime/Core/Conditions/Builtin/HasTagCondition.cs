using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Has Tag")]
    public sealed class HasTagCondition : ICondition
    {
        [SerializeField] private Subject subject = Subject.Target;
        [SerializeField] private CombatTag tag;

        public bool Evaluate(in EffectContext ctx)
        {
            var who = ctx.GetUnit(subject);
            bool has = who != null && who.Tags.Has(tag);
            return has;
        }
    }
}
