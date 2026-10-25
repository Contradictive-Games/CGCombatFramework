namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Triggers/On Died")]
    [BlockInfo("Runs payloads when the host dies. Other is the killer.", Example = "Exploding barrel: Emit At Target with Delivery (Sphere 4m) and Deal Damage")]
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