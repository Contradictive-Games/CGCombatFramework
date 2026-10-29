using Vector3 = UnityEngine.Vector3;

namespace CG.CombatFramework
{
    public readonly struct KnockbackEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Source;
        public readonly Vector3 Impulse;
        public KnockbackEvent(IUnit unit, IUnit source, Vector3 impulse)
        {
            Unit = unit;
            Source = source;
            Impulse = impulse;
        }
    }

    public readonly struct TeleportedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Source;
        public readonly Vector3 From;
        public readonly Vector3 To;
        public TeleportedEvent(IUnit unit, IUnit source, Vector3 from, Vector3 to)
        {
            Unit = unit;
            Source = source;
            From = from;
            To = to;
        }
    }
}
