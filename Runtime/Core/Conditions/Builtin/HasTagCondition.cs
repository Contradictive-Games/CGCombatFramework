using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Has Tag")]
    [BlockInfo("Passes when the subject has the tag or one of its child tags.", Example = "Not + Has Tag State.Stunned on a channel stops it when the caster is stunned")]
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
