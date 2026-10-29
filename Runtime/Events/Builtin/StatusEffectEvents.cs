namespace CG.CombatFramework
{
    public enum EffectRemoveReason { Expired, Dispelled, Died, Despawned }

    public readonly struct StatusEffectAppliedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly StatusEffectInstance Effect;
        public readonly int PreviousStacks;
        public StatusEffectAppliedEvent(IUnit unit, StatusEffectInstance effect, int previousStacks)
        {
            Unit = unit;
            Effect = effect;
            PreviousStacks = previousStacks;
        }
    }

    public readonly struct StatusEffectRemovedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly StatusEffectInstance Effect;
        public readonly EffectRemoveReason Reason;
        public StatusEffectRemovedEvent(IUnit unit, StatusEffectInstance effect, EffectRemoveReason reason)
        {
            Unit = unit;
            Effect = effect;
            Reason = reason;
        }
    }

    public readonly struct StatusEffectStacksChangedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly StatusEffectInstance Effect;
        public readonly int Previous;
        public StatusEffectStacksChangedEvent(IUnit unit, StatusEffectInstance effect, int previous)
        {
            Unit = unit;
            Effect = effect;
            Previous = previous;
        }
    }

    public readonly struct StatusEffectTickedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly StatusEffectInstance Effect;
        public StatusEffectTickedEvent(IUnit unit, StatusEffectInstance effect)
        {
            Unit = unit;
            Effect = effect;
        }
    }

    public readonly struct StatusEffectTriggeredEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly StatusEffectInstance Effect;
        public readonly IUnit Target;
        public StatusEffectTriggeredEvent(IUnit unit, StatusEffectInstance effect, IUnit target)
        {
            Unit = unit;
            Effect = effect;
            Target = target;
        }
    }
}
