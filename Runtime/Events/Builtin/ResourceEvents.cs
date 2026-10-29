namespace CG.CombatFramework
{
    public readonly struct ResourceChangedEvent
    {
        public readonly IUnit Owner;
        public readonly ResourceChange Change;
        public ResourceChangedEvent(IUnit owner, in ResourceChange change)
        {
            Owner = owner;
            Change = change;
        }
    }

    public readonly struct ResourceDepletedEvent
    {
        public readonly IUnit Owner;
        public readonly ResourceChange Change;
        public ResourceDepletedEvent(IUnit owner, in ResourceChange change)
        {
            Owner = owner;
            Change = change;
        }
    }

    public readonly struct ResourceFilledEvent
    {
        public readonly IUnit Owner;
        public readonly Resource Resource;
        public ResourceFilledEvent(IUnit owner, Resource resource)
        {
            Owner = owner;
            Resource = resource;
        }
    }

    public readonly struct ResourceRegenStartedEvent
    {
        public readonly IUnit Owner;
        public readonly Resource Resource;
        public ResourceRegenStartedEvent(IUnit owner, Resource resource)
        {
            Owner = owner;
            Resource = resource;
        }
    }

    public readonly struct ResourceRegenStoppedEvent
    {
        public readonly IUnit Owner;
        public readonly Resource Resource;
        public ResourceRegenStoppedEvent(IUnit owner, Resource resource)
        {
            Owner = owner;
            Resource = resource;
        }
    }
}
