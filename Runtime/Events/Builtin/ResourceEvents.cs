namespace CG.CombatFramework
{
    public readonly struct ResourceChangedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly ResourceChange Change;
        public ResourceChangedEvent(IUnit unit, in ResourceChange change)
        {
            Unit = unit;
            Change = change;
        }
    }

    public readonly struct ResourceDepletedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly ResourceChange Change;
        public readonly EffectContext Source;
        public ResourceDepletedEvent(IUnit unit, in ResourceChange change, in EffectContext source)
        {
            Unit = unit;
            Change = change;
            Source = source;
        }
    }

    public readonly struct ResourceFilledEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly ResourceChange Change;
        public ResourceFilledEvent(IUnit unit, in ResourceChange change)
        {
            Unit = unit;
            Change = change;
        }
    }

    public readonly struct ResourceRegenStartedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IReadonlyResource Resource;
        public ResourceRegenStartedEvent(IUnit unit, IReadonlyResource resource)
        {
            Unit = unit;
            Resource = resource;
        }
    }

    public readonly struct ResourceRegenStoppedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IReadonlyResource Resource;
        public ResourceRegenStoppedEvent(IUnit unit, IReadonlyResource resource)
        {
            Unit = unit;
            Resource = resource;
        }
    }
}
