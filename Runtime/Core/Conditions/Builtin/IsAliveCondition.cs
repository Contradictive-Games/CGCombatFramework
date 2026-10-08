using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Is Alive")]
    public sealed class IsAliveCondition : ICondition
    {
        [SerializeField] private Subject subject = Subject.Target;
        public bool Evaluate(in EffectContext ctx) => ctx.GetUnit(subject)?.IsAlive ?? false;
    }
}