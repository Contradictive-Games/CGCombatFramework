using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Is Alive")]
    [BlockInfo("Passes when the subject is spawned and not dead.", Example = "Only heal allies that are still alive")]
    public sealed class IsAliveCondition : ICondition
    {
        [SerializeField] private Subject subject = Subject.Target;
        public bool Evaluate(in EffectContext ctx) => ctx.GetUnit(subject)?.IsAlive ?? false;
    }
}