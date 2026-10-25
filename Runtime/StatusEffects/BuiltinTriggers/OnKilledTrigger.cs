using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Triggers/On Kill")]
    [BlockInfo("Runs payloads when the host kills a unit, including kills by things it owns unless Direct Only is on. Other is the victim.", Example = "Refund ammo on kill")]
    public sealed class OnKillTrigger : TriggerBehavior<KilledUnitEvent>
    {
        [SerializeField, Tooltip("Only kills host landed itself (false = summon's and turret's kills count as well)")]
        private bool directOnly;

        protected override bool Read(in KilledUnitEvent evt, StatusEffectInstance effect, out TriggerRead read)
        {
            read = default;
            var death = evt.Death;
            if(directOnly && !ReferenceEquals(death.Context.SourceUnit, effect.Host)) return false;
            float amount = death.FinalBlow.HasValue ? death.FinalBlow.Value.Total : 0f;
            read = new TriggerRead(death.Victim, in death.Context.Hit, amount, death.Context.ProcDepth);
            return true;
        }
    }
}