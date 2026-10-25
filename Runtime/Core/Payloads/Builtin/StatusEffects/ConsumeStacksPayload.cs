using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Status Effects/Consume Stacks")]
    [BlockInfo("Removes every stack of the effect from the subject, then runs its payloads with the number of stacks removed.", Example = "Detonate: consume Poison and deal 10 per stack with a Stacks scaler")]
    public sealed class ConsumeStacksPayload : IPayload
    {
        [SerializeField] private StatusEffectDefinition effect;
        [FormerlySerializedAs("from")]
        [SerializeField, Tooltip("Who will receive this")]
        private Subject subject = Subject.Target;
        [SerializeField, Min(1), Tooltip("Do nothing below this many stacks")]
        private int minimumStacks = 1;
        [SerializeReference, SelectType]
        private IPayload[] payloads = Array.Empty<IPayload>();

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if (unit == null || !unit.TryGet<IStatusEffects>(out var effects)) return;
            int stacks = effects.GetStacks(effect);
            if (stacks < minimumStacks) return;
            effects.RemoveAll(effect);
            var consumed = ctx.ForStacks(stacks);
            Payloads.ApplyAll(payloads, in consumed);
        }
    }
}
