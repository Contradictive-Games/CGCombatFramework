namespace CG.CombatFramework
{
    public readonly struct TagAddedEvent
    {
        public readonly IUnit Owner;
        public readonly CombatTag Tag;
        public TagAddedEvent(IUnit owner, CombatTag tag)
        {
            Owner = owner;
            Tag = tag;
        }
    }
    public readonly struct TagRemovedEvent
    {
        public readonly IUnit Owner;
        public readonly CombatTag Tag;
        public TagRemovedEvent(IUnit owner, CombatTag tag)
        {
            Owner = owner;
            Tag = tag;
        }
    }
}
