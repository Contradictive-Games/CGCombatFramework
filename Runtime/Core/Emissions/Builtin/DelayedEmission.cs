using UnityEngine;

namespace CG.CombatFramework
{
    public sealed class DelayedEmission : IEmission
    {
        [SerializeField, Min(0f), Tooltip("Seconds of combat time, origin & aim are fixed when it was aimed - not afte delay")] 
        private float delay = 1f;
        
        [SerializeReference, SelectType] private IEmission emission;

        public void Emit(in DeliveryContext ctx) => CombatScheduler.Schedule(delay, emission, in ctx);
    }
}
