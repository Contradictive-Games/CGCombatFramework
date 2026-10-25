using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Firables/Add Ammo")]
    public sealed class AddAmmoPayload : IPayload
    {
        [FormerlySerializedAs("who")]
        [SerializeField, Tooltip("Who will receive this")] private Subject subject = Subject.Source;
        [SerializeField, Tooltip("Empty = every equipped firable with ammo")] private FirableSlot slot;
        [SerializeField, Tooltip("Rounds added. 0 = fill"), Min(0)] private int amount = 1;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit == null || !unit.TryGet<FirableLoadout>(out var loadout)) return;
            if(slot != null)
            {
                if(loadout.TryGet(slot, out var firable) && firable.TryGetGate<IReloadable>(out var magazine)) magazine.AddAmmo(amount);
                return;
            }

            foreach(var firable in loadout.Equipped.Values)
            {
                if(firable.TryGetGate<IReloadable>(out var magainze)) magainze.AddAmmo(amount);
            }
        }
    }
}
