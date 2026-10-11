namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Triggers/On Died")]
    public sealed class OnDiedTrigger : TriggerBehavior<DiedEvent>
    {
        protected override bool Read(in DiedEvent evt, StatusEffectInstance effect, out TriggerRead read)
        {
            var death = evt.Death;
            float amount = death.FinalBlow.HasValue ? death.FinalBlow.Value.Total : 0f;
            read = new TriggerRead(death.Killer, in death.Context.Hit, amount, 0);
            return true;
        }
    }
}