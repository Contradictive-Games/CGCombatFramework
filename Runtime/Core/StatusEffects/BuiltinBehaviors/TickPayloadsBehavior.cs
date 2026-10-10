namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Payloads/On Tick")]
    public sealed class TickPayloadsBehavior : IStatusEffectBehavior
    {
        [UnityEngine.SerializeReference, SelectType]
        private IPayload[] payloads = System.Array.Empty<IPayload>();

        public void OnTick(StatusEffectInstance effect) => Payloads.ApplyAll(payloads, effect.CreateContext());
    }
}