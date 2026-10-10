using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Payloads/On Remove")]
    public sealed class RemovePayloadsBehavior : IStatusEffectBehavior
    {
        [SerializeField, Tooltip("If this will only happen when timer runs out and not on dispel/death/despawn")] 
        private bool onlyWhenExpired = true;
        [SerializeReference, SelectType]
        private IPayload[] payloads = System.Array.Empty<IPayload>();

        public void OnRemoved(StatusEffectInstance effect, EffectRemoveReason reason)
        {
            if(reason == EffectRemoveReason.Despawned) return;
            if(onlyWhenExpired && reason != EffectRemoveReason.Expired) return;
            Payloads.ApplyAll(payloads, effect.CreateContext());
        }
    }
}