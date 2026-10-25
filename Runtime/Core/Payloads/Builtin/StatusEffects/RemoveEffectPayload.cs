using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Status Effects/Remove Effect")]
    public sealed class RemoveEffectPayload : IPayload
    {
        [SerializeField] private StatusEffectDefinition effect;
        [FormerlySerializedAs("from")]
        [SerializeField, Tooltip("Who will receive this")]
        private Subject subject = Subject.Target;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if (unit != null && unit.TryGet<IStatusEffects>(out var effects)) effects.RemoveAll(effect);
        }
    }
}
