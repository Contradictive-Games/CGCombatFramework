using UnityEngine;

namespace CG.CombatFramework
{
    [System.Flags]
    public enum PayloadEvent
    {
        None = 0,
        Applied = 1 << 0,
        StacksIncreased = 1 << 1,
        StacksDecreased = 1 << 2,
        Tick = 1 << 3,
        Expired = 1 << 4,
        Dispelled = 1 << 5,
        Died = 1 << 6
    }

    [System.Serializable, TypeMenu("Payloads")]
    public sealed class PayloadsBehavior : IStatusEffectBehavior
    {
        [SerializeField, Tooltip("When these payloads will run, despawning never runs them")]
        private PayloadEvent when = PayloadEvent.Tick;
        [SerializeReference, SelectType]
        private IPayload[] payloads = System.Array.Empty<IPayload>();


        public void OnApplied(StatusEffectInstance effect) => Run(PayloadEvent.Applied, effect);
        public void OnTick(StatusEffectInstance effect) => Run(PayloadEvent.Tick, effect);

        public void OnStacksChanged(StatusEffectInstance effect, int previousStacks)
        {
            if(effect.Stacks > previousStacks) Run(PayloadEvent.StacksIncreased, effect);
            else if(effect.Stacks < previousStacks) Run(PayloadEvent.StacksDecreased, effect);
        }

        public void OnRemoved(StatusEffectInstance effect, EffectRemoveReason reason)
        {
            switch (reason)
            {
                case EffectRemoveReason.Expired:
                    Run(PayloadEvent.Expired, effect);
                    break;
                case EffectRemoveReason.Dispelled:
                    Run(PayloadEvent.Dispelled, effect);
                    break;
                case EffectRemoveReason.Died:
                    Run(PayloadEvent.Died, effect);
                    break;
            }
        }

        private void Run(PayloadEvent evt, StatusEffectInstance effect)
        {
            if((when & evt) != 0) Payloads.ApplyAll(payloads, effect.CreateContext());
        }
    }
}
