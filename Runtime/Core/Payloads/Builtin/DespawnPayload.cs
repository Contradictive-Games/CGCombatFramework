using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Units/Despawn")]
    public sealed class DespawnPayload : IPayload
    {
        [FormerlySerializedAs("who")]
        [SerializeField, Tooltip("Who will receive this")] private Subject subject = Subject.Target;
        [SerializeField] private DespawnReason reason = DespawnReason.Recycled;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit != null && unit.IsSpawned) CombatServices.Spawner.Despawn(unit, reason);
        }
    }
}
