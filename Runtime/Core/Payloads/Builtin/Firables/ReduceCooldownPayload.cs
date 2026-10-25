using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Firables/Reduce Cooldown")]
    [BlockInfo("Shortens (or finishes) cooldowns on the subject's firables and can refund charges. No slot = every equipped firable.", Example = "Crits reduce all cooldowns by 1s")]
    public sealed class ReduceCooldownPayload : IPayload
    {
        [FormerlySerializedAs("who")]
        [SerializeField, Tooltip("Who will receive this")] private Subject subject = Subject.Source;
        [SerializeField, Tooltip("Empty = every equipped firable")]
        private FirableSlot slot;
        [SerializeField] private ScalableValue seconds = new(1f);
        [SerializeField, Tooltip("Finishes cooldowns outright")] private bool finish;
        [SerializeField, Min(0), Tooltip("Charges given back")] private int refundCharges;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit == null || !unit.TryGet<FirableLoadout>(out var loadout)) return;
            float amount = finish ? 0f : seconds.Evaluate(in ctx);
            if(slot != null)
            {
                if(loadout.TryGet(slot, out var firable)) Affect(firable, amount);
                return;
            }
            foreach(var firable in loadout.Equipped.Values)
            {
                Affect(firable, amount);
            }
        }

        private void Affect(Firable firable, float amount)
        {
            var gates = firable.Gates;
            for(int i = 0; i < gates.Count; i++)
            {
                if(gates[i] is ICooldown cooldown) cooldown.Reduce(finish ? cooldown.Remaining : amount);
                if(refundCharges > 0 && gates[i] is IChargeCounter charges) charges.AddCharges(refundCharges);
            }
        }
    }
}
