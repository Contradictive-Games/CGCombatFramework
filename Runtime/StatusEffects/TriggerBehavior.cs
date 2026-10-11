using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum TriggerCredit { Host, Applier }
    public enum TriggerTarget { Other, Host }

    public readonly struct TriggerRead
    {
        public readonly IUnit Other;
        public readonly HitData Hit;
        public readonly float Amount;
        public readonly int ProcDepth;

        public TriggerRead(IUnit other, in HitData hit, float amount, int procDepth)
        {
            Other = other;
            Hit = hit;
            Amount = amount;
            ProcDepth = procDepth;
        }
    }


    [System.Serializable]
    public abstract class TriggerBehavior<TEvent> : IStatusEffectBehavior where TEvent : struct
    {
        [Header("When")]
        [SerializeField, Range(0f, 1f)] private float chance = 1f;
        [SerializeField, Min(1), Tooltip("Only every Nth qualifying event. 3 = every 3rd hit")] 
        private int everyNth = 1;
        [SerializeField, Min(0f), Tooltip("Seconds before it can fire again, 0 = no limit \nNOTE: (this timer pauses with combat)")] 
        private float cooldown;
        [SerializeField, Range(1, 32), Tooltip("Reacts only to events fewer than this many trigger levels deep \n1 = direct hits only. Raise it for chain reactions")] 
        private int maxProcDepth = 1;
        [SerializeField, Tooltip("Host: the unit with this effect \nApplier: whoever applied effect")] 
        private TriggerCredit credit = TriggerCredit.Host;
        [SerializeField, Tooltip("Other: the other unit in the event (the victim) \nHost: unit wearing the effect")] 
        private TriggerTarget target = TriggerTarget.Other;
        [SerializeReference, SelectType, Tooltip("Source = credited unit, Target = chosen above")]
        private ICondition[] conditions = System.Array.Empty<ICondition>();

        [Header("Then")]
        [SerializeReference, SelectType]
        private IPayload[] payloads = System.Array.Empty<IPayload>();

        private Dictionary<StatusEffectInstance, Listener> listeners;
        private Dictionary<StatusEffectInstance, Listener> Listeners => listeners ??= new();


        public void OnApplied(StatusEffectInstance effect)
        {
            var listener = new Listener(this, effect);
            Listeners[effect] = listener;
            effect.Host.Events.Subscribe(listener.Handler);
        }

        public void OnRemoved(StatusEffectInstance effect, EffectRemoveReason reason)
        {
            if(Listeners.Remove(effect, out var listener)) effect.Host.Events.Unsubscribe(listener.Handler);
        }

        protected abstract bool Read(in TEvent evt, StatusEffectInstance effect, out TriggerRead read);

        private void Fire(Listener listener, in TEvent evt)
        {
            var effect = listener.Effect;
            if(!effect.IsActive || effect.Version != listener.Version) return;
            if(!Read(in evt, effect, out var read) || read.ProcDepth >= maxProcDepth) return;
            if(effect.Elapsed < listener.ReadyAt) return;

            var source = credit == TriggerCredit.Host ? effect.Host : effect.SourceUnit;
            if(source == null) return;
            var aimed = target == TriggerTarget.Host ? effect.Host : read.Other;
            var ctx = new EffectContext(source, effect.Definition, aimed, in read.Hit,
                effect.Power, effect.Stacks, read.ProcDepth + 1, null, read.Amount);
            if(!conditions.All(in ctx)) return;
            if(++listener.Count < everyNth) return;
            listener.Count = 0;
            if(chance < 1f && !ctx.Random.Roll(chance)) return;

            listener.ReadyAt = effect.Elapsed + cooldown;
            Payloads.ApplyAll(payloads, in ctx);
        }
        
        private sealed class Listener
        {
            public readonly StatusEffectInstance Effect;
            public readonly int Version;
            public readonly GameEventHandler<TEvent> Handler;
            public float ReadyAt;
            public int Count;

            public Listener(TriggerBehavior<TEvent> owner, StatusEffectInstance effect)
            {
                Effect = effect;
                Version = effect.Version;
                Handler = (in TEvent evt) => owner.Fire(this, in evt);
            }
        }
    }
}
