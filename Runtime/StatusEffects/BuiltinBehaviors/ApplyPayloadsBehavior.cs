using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Payloads/On Apply")]
    public sealed class ApplyPayloadsBehavior : IStatusEffectBehavior
    {
        [SerializeField, Tooltip("Also run when effect restacks")] private bool onRestack;
        [SerializeReference, SelectType] private IPayload[] payloads = System.Array.Empty<IPayload>();
        
        public void OnApplied(StatusEffectInstance effect) => Payloads.ApplyAll(payloads, effect.CreateContext());

        public void OnStacksChanged(StatusEffectInstance effect, int previousStacks)
        {
            if(onRestack && effect.Stacks > previousStacks) Payloads.ApplyAll(payloads, effect.CreateContext());
        }
    }
}