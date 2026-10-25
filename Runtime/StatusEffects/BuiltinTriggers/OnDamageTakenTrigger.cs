using UnityEngine;

namespace CG.CombatFramework
{
    public enum TakenWhen { Landed, Avoided, Either }

    [System.Serializable, TypeMenu("Triggers/On Damage Taken")]
    [BlockInfo("Runs payloads when the host takes damage that lands, is avoided, or either. Other is the attacker.", Example = "Thorns: deal 5 damage back to the attacker")]
    public sealed class OnDamageTakenTrigger : TriggerBehavior<DamageReceivedEvent>
    {
        [SerializeField] private TakenWhen when = TakenWhen.Landed;
        [SerializeField] private bool ignorePeriodic = true;
        [SerializeField, Min(0f)] private float minimumDamage;


        protected override bool Read(in DamageReceivedEvent evt, StatusEffectInstance effect, out TriggerRead read)
        {
            read = default;
            var result = evt.Result;
            bool landed = result.Result == HitResult.Hit && result.Total > 0f;
            if (when == TakenWhen.Landed && (!landed || result.Total < minimumDamage)) return false;
            if (when == TakenWhen.Avoided && result.Result == HitResult.Hit) return false;
            if (ignorePeriodic && result.Request.Has(DamageFlags.Periodic)) return false;
            if (ReferenceEquals(evt.SourceUnit, effect.Host)) return false;
            var ctx = result.Request.Context;
            read = new TriggerRead(evt.SourceUnit, in ctx.Hit, result.Total, ctx.ProcDepth);
            return true;
        }
    }
}