using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Stacks of Effect")]
    [BlockInfo("Multiplies by 1 + stacks x Per Stack, counting the stacks of an effect on the subject.", Example = "+10% damage per stack of Vulnerable on the target")]
    public sealed class EffectStacksScaler : IValueScaler
    {
        [SerializeField] private Subject subject = Subject.Target;
        [SerializeField] private StatusEffectDefinition effect;
        [SerializeField, Tooltip("0.1 = +10% per stack")] private float perStack = 0.1f;

        public float Apply(float value, in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit == null || effect == null || !unit.TryGet<IStatusEffects>(out var effects)) return value;
            return value * (1f + effects.GetStacks(effect) * perStack);
        }
    }
}
