using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Flow/Delay")]
    public sealed class DelayPayload : IPayload
    {
        [SerializeField, Min(0f), Tooltip("Delay in seconds")] 
        private float delay = 1f;
        
        [SerializeReference, SelectType] 
        private IPayload[] payloads = Array.Empty<IPayload>();

        public void Apply(in EffectContext ctx) => CombatScheduler.Schedule(delay, payloads, in ctx);
    }
}
