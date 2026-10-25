using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flow/Delayed Emission")]
    [BlockInfo("Waits Delay seconds of combat time, then runs its emissions. Origin and aim are locked in when it starts.", Example = "Telegraphed strike: Delayed 1s, then Delivery (Sphere 3m) with Deal Damage")]
    public sealed class DelayedEmission : IEmission
    {
        [SerializeField, Min(0f), Tooltip("Seconds of combat time, origin & aim are fixed when it was aimed - not afte delay")]
        private float delay = 1f;

        [SerializeReference, SelectType] private IEmission[] emissions;

        public void Emit(in DeliveryContext ctx) => CombatScheduler.Schedule(delay, emissions, in ctx);
    }
}
