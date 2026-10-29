using System.Collections.Generic;

namespace CG.CombatFramework
{
    public readonly struct UnitSpawnedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly SpawnData Data;
        public UnitSpawnedEvent(IUnit unit, in SpawnData data)
        {
            Unit = unit;
            Data = data;
        }
    }

    public readonly struct UnitDespawnedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly DespawnReason Reason;
        public UnitDespawnedEvent(IUnit unit, DespawnReason reason)
        {
            Unit = unit;
            Reason = reason;
        }
    }

    public readonly struct UnitArrivingEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IReadOnlyList<CombatTag> Tags;
        public readonly float Duration;
        public UnitArrivingEvent(IUnit unit, IReadOnlyList<CombatTag> tags, float duration)
        {
            Unit = unit;
            Tags = tags;
            Duration = duration;
        }
    }

    public readonly struct UnitArrivedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public UnitArrivedEvent(IUnit unit) => Unit = unit;
    }

    public readonly struct DespawnScheduledEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly float Delay;
        public readonly DespawnReason Reason;
        public DespawnScheduledEvent(IUnit unit, float delay, DespawnReason reason)
        {
            Unit = unit;
            Delay = delay;
            Reason = reason;
        }
    }

    public readonly struct UnitDepartingEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly DespawnReason Reason;
        public readonly float Duration;
        public UnitDepartingEvent(IUnit unit, DespawnReason reason, float duration)
        {
            Unit = unit;
            Reason = reason;
            Duration = duration;
        }
    }

    public readonly struct UnitReleasedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public UnitReleasedEvent(IUnit unit) => Unit = unit;
    }
}
