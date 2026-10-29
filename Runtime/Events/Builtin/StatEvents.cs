namespace CG.CombatFramework
{
    public readonly struct StatChangedEvent
    {
        public readonly IUnit Owner;
        public readonly StatType Type;
        public readonly float Previous;
        public readonly float Current;

        public StatChangedEvent(IUnit owner, StatType type, float previous, float current)
        {
            Owner = owner;
            Type = type;
            Previous = previous;
            Current = current;
        }
    }
}
