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

    public readonly struct ReloadStartedEvent
    {
        public readonly IUnit Owner;
        public readonly FirableDefinition Firable;
        public readonly float Duration;
        public ReloadStartedEvent(IUnit owner, FirableDefinition firable, float duration)
        {
            Owner = owner;
            Firable = firable;
            Duration = duration;
        }
    }

    public readonly struct ReloadCompletedEvent
    {
        public readonly IUnit Owner;
        public readonly FirableDefinition Firable;
        public ReloadCompletedEvent(IUnit owner, FirableDefinition firable)
        {
            Owner = owner;
            Firable = firable;
        }
    }
}
