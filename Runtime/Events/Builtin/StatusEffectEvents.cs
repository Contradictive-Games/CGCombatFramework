namespace CG.CombatFramework
{
    public enum EffectRemoveReason { Expired, Dispelled, Died, Despawned }

    public readonly struct StatusEffectAppliedEvent
    {
        public readonly IUnit Host;
        public readonly StatusEffectInstance Effect;
        public readonly int PreviousStacks;

        public StatusEffectAppliedEvent(IUnit host, StatusEffectInstance effect, int previousStacks)
        {
            Host = host;
            Effect = effect;
            PreviousStacks = previousStacks;
        }
    }

    public readonly struct StatusEffectRemovedEvent
    {
        public readonly IUnit Host;
        public readonly StatusEffectInstance Effect;
        public readonly EffectRemoveReason Reason;

        public StatusEffectRemovedEvent(IUnit host, StatusEffectInstance effect, EffectRemoveReason reason)
        {
            Host = host;
            Effect = effect;
            Reason = reason;
        }
    }

    public readonly struct StatusEffectStacksChangedEvent
    {
        public readonly IUnit Owner; //host
        public readonly StatusEffectInstance Effect;
        public readonly int Previous;
        public StatusEffectStacksChangedEvent(IUnit owner, StatusEffectInstance effect, int previous)
        {
            Owner = owner;
            Effect = effect;
            Previous = previous;
        }
    }

    public readonly struct StatusEffectTickedEvent
    {
        public readonly IUnit Owner;
        public readonly StatusEffectInstance Effect;
        public StatusEffectTickedEvent(IUnit owner, StatusEffectInstance effect)
        {
            Owner = owner;
            Effect = effect;
        }
    }

}
