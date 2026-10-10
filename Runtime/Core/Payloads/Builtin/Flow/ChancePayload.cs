namespace CG.CombatFramework
{
    /// <summary>
    /// A wrapper for other payloads that only executes on successful roll
    /// </summary>
    [System.Serializable, TypeMenu("Flow/Chance")]
    public sealed class ChancePayload : IPayload
    {
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)] private float chance = 0.25f;
        [UnityEngine.SerializeReference, SelectType] 
        private IPayload[] payloads = System.Array.Empty<IPayload>();

        public void Apply(in EffectContext ctx)
        {
            if(ctx.Random.Roll(chance)) Payloads.ApplyAll(payloads, in ctx);
        }
    }
}