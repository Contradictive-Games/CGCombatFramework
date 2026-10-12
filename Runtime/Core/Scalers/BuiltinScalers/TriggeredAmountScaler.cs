namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Triggered Amount")]
    public sealed class TriggeredAmountScaler : IValueScaler
    {
        public float Apply(float value, in EffectContext ctx) => value * ctx.Amount;
    }
}
