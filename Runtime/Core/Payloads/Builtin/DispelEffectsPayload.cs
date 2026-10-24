using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Status Effects/Dispel Effect")]
    public sealed class DispelEffectsPayload : IPayload
    {
        [SerializeField, Tooltip("Removes all effects of this category or child of category")]
        private CombatTag category;
        [SerializeField, Min(1)] private int max = 1;
        [SerializeField, Tooltip("Who will receive this")] private Subject subject = Subject.Target;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit != null && unit.TryGet<IStatusEffects>(out var effects)) effects.Dispel(category, max);
        }
    }
}
