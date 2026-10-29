using Vector3 = UnityEngine.Vector3;

namespace CG.CombatFramework
{
    public enum ScheduleCancelReason { Cleared }

    public readonly struct ScheduledEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly int Key;
        public readonly float Delay;
        public readonly IGameDefinition Cause;
        public readonly Vector3 Origin;
        public readonly Vector3 AimPoint;
        public ScheduledEvent(IUnit unit, int key, float delay, IGameDefinition cause, Vector3 origin, Vector3 aimPoint)
        {
            Unit = unit;
            Key = key;
            Delay = delay;
            Cause = cause;
            Origin = origin;
            AimPoint = aimPoint;
        }
    }

    public readonly struct ScheduleResolvedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly int Key;
        public ScheduleResolvedEvent(IUnit unit, int key)
        {
            Unit = unit;
            Key = key;
        }
    }

    public readonly struct ScheduleCancelledEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly int Key;
        public readonly ScheduleCancelReason Reason;
        public ScheduleCancelledEvent(IUnit unit, int key, ScheduleCancelReason reason)
        {
            Unit = unit;
            Key = key;
            Reason = reason;
        }
    }

    public readonly struct PauseChangedEvent : ICombatEvent
    {
        public readonly bool IsPaused;
        public PauseChangedEvent(bool isPaused) => IsPaused = isPaused;
    }

    public readonly struct SessionEndedEvent : ICombatEvent
    {
        public readonly DespawnReason Reason;
        public SessionEndedEvent(DespawnReason reason) => Reason = reason;
    }
}
