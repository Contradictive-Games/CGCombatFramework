using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Triggers/On Damage Dealt")]
    public sealed class OnDamageDealtTrigger : TriggerBehavior<DamageDealtEvent>
    {
        [SerializeField] private bool critsOnly = false;
        [SerializeField] private bool ignorePeriodic = true;
        [SerializeField, Min(0f)] private float minimumDamage;

        protected override bool Read(in DamageDealtEvent evt, StatusEffectInstance effect, out TriggerRead read)
        {
            read = default;
            var result = evt.Result;
            if(result.Result != HitResult.Hit || result.Total <= 0f || result.Total < minimumDamage) return false;
            if(critsOnly && !result.IsCrit) return false;
            if(ignorePeriodic && result.Request.Has(DamageFlags.Periodic)) return false;
            if(ReferenceEquals(evt.TargetUnit, effect.Host)) return false;
            var ctx = result.Request.Context;
            read = new TriggerRead(evt.TargetUnit, in ctx.Hit, result.Total, ctx.ProcDepth);
            return true;
        }
    }
}
