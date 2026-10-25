using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Status Effects/Apply Effect")]
    public sealed class ApplyStatusEffectPayload : IPayload
    {
        [SerializeField] private StatusEffectDefinition effect;
        [SerializeField, Min(1)] private int stacks = 1;
        [FormerlySerializedAs("applyTo")]
        [SerializeField, Tooltip("Who will receive this")] private Subject subject = Subject.Target;
        [SerializeField, Tooltip("Evaluated once with this hit's context and remembered by effect. \nExample: Stat scaler on caster's DamageMultiplier snapshots their damage bonus into the burn")]
        private ScalableValue power = new(1f);
        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit != null && unit.TryGet<IStatusEffects>(out var effects)) effects.Apply(
                effect,
                ctx.SourceUnit,
                stacks,
                power.Evaluate(in ctx));
        }
    }
}
