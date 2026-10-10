using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Status Effects/Apply Effect")]
    public sealed class ApplyStatusEffectPayload : IPayload
    {
        [SerializeField] private StatusEffectDefinition effect;
        [SerializeField, Min(1)] private int stacks = 1;
        [SerializeField] private Subject applyTo = Subject.Target;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(applyTo);
            if(unit != null && unit.TryGet<IStatusEffects>(out var effects)) effects.Apply(effect, ctx.SourceUnit, stacks);
        }
    }
}