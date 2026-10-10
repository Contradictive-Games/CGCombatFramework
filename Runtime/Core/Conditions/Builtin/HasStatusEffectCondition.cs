using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Has Status Effect")]
    public sealed class HasStatusEffectCondition : ICondition
    {
        [SerializeField] private Subject subject = Subject.Target;
        [SerializeField] private StatusEffectDefinition effect;
        [SerializeField, Min(1), Tooltip("Minimum amount of stacks required for pass")] private int minStacks = 1;

        public bool Evaluate(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            return unit != null && unit.TryGet<IStatusEffects>(out var effects) && effects.GetStacks(effect) >= minStacks;
        }
    }
}