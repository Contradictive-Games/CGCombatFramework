namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Triggered Amount")]
    [BlockInfo("Multiplies by the amount from the trigger that caused this, such as the damage dealt or taken.", Example = "Lifesteal: 0.2 x damage dealt")]
    public sealed class TriggeredAmountScaler : IValueScaler
    {
        public float Apply(float value, in EffectContext ctx) => value * ctx.Amount;
    }
}
