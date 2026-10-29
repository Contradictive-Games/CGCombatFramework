namespace CG.CombatFramework
{
    public readonly struct TagAddedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly CombatTag Tag;
        public TagAddedEvent(IUnit unit, CombatTag tag)
        {
            Unit = unit;
            Tag = tag;
        }
    }

    public readonly struct TagRemovedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly CombatTag Tag;
        public TagRemovedEvent(IUnit unit, CombatTag tag)
        {
            Unit = unit;
            Tag = tag;
        }
    }
}
