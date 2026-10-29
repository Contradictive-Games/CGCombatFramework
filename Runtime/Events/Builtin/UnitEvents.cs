using Vector3 = UnityEngine.Vector3;

namespace CG.CombatFramework
{
    public readonly struct UnitSpawnedEvent
    {
        public readonly IUnit Unit;
        public readonly SpawnData Data;
        public UnitSpawnedEvent(IUnit unit, in SpawnData data)
        {
            Unit = unit;
            Data = data;
        }
    }

    public readonly struct UnitDespawnedEvent
    {
        public readonly IUnit Unit;
        public readonly DespawnReason Reason;
        public UnitDespawnedEvent(IUnit unit, DespawnReason reason)
        {
            Unit = unit;
            Reason = reason;
        }
    }

    public readonly struct DamageDealtEvent
    {
        public readonly IUnit SourceUnit;
        public readonly IUnit TargetUnit;
        public readonly DamageResult Result;
        public DamageDealtEvent(IUnit sourceUnit, IUnit targetUnit, in DamageResult result)
        {
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            Result = result;
        }
    }
    public readonly struct DamageReceivedEvent
    {
        public readonly IUnit SourceUnit;
        public readonly IUnit TargetUnit;
        public readonly DamageResult Result;
        public DamageReceivedEvent(IUnit sourceUnit, IUnit targetUnit, in DamageResult result)
        {
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            Result = result;
        }
    }

    public readonly struct HealDealtEvent
    {
        public readonly IUnit SourceUnit;
        public readonly IUnit TargetUnit;
        public readonly HealResult Result;
        public HealDealtEvent(IUnit sourceUnit, IUnit targetUnit, in HealResult result)
        {
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            Result = result;
        }
    }

    public readonly struct HealReceivedEvent
    {
        public readonly IUnit SourceUnit;
        public readonly IUnit TargetUnit;
        public readonly HealResult Result;
        public HealReceivedEvent(IUnit sourceUnit, IUnit targetUnit, in HealResult result)
        {
            SourceUnit = sourceUnit;
            TargetUnit = targetUnit;
            Result = result;
        }
    }

    public readonly struct DiedEvent
    {
        public readonly DeathInfo Death;
        public DiedEvent(in DeathInfo death)
        {
            Death = death;
        }
    }

    public readonly struct KilledUnitEvent
    {
        public readonly DeathInfo Death;
        public KilledUnitEvent(in DeathInfo death)
        {
            Death = death;
        }
    }

    public readonly struct KnockbackEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Source;
        public readonly float Impulse;
        public KnockbackEvent(IUnit unit, IUnit source, float impulse)
        {
            Unit = unit;
            Source = source;
            Impulse = impulse;
        }
    }

    public readonly struct TeleportedEvent
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

    public readonly struct UnitArrivingEvent
    {
        public readonly IUnit Unit;
        public readonly CombatTag Arrival;
        public readonly float Duration;
        public UnitArrivingEvent(IUnit unit, CombatTag arrival, float duration)
        {
            Unit = unit;
            Arrival = arrival;
            Duration = duration;
        }
    }

    public readonly struct UnitArrivedEvent
    {
        public readonly IUnit Unit;
        public UnitArrivedEvent(IUnit unit) => Unit = unit;
    }
}
