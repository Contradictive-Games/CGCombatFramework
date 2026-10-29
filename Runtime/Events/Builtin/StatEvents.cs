namespace CG.CombatFramework
{
    public readonly struct StatChangedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IReadonlyStat Stat;
        public readonly float Previous;
        public readonly float Current;
        public StatChangedEvent(IUnit unit, IReadonlyStat stat, float previous, float current)
        {
            Unit = unit;
            Stat = stat;
            Previous = previous;
            Current = current;
        }
    }
}
