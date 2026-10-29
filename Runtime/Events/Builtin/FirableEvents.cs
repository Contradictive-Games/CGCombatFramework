namespace CG.CombatFramework
{
    public enum FiringEndReason
    {
        None,
        Fired,
        Released,
        Completed,
        Interrupted,
        Cancelled
    }

    public readonly struct FiredEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public readonly float Power;
        public FiredEvent(IUnit unit, Firable firable, float power)
        {
            Unit = unit;
            Firable = firable;
            Power = power;
        }
    }

    public readonly struct FireBlockedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public readonly FireBlockReason Reason;
        public FireBlockedEvent(IUnit unit, Firable firable, FireBlockReason reason)
        {
            Unit = unit;
            Firable = firable;
            Reason = reason;
        }
    }

    public readonly struct FiringPhaseChangedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public readonly FiringPhase Previous;
        public readonly FiringPhase Current;
        public readonly FiringEndReason EndReason;
        public readonly float Elapsed;
        public FiringPhaseChangedEvent(IUnit unit, Firable firable, FiringPhase previous,
            FiringPhase current, FiringEndReason endReason, float elapsed)
        {
            Unit = unit;
            Firable = firable;
            Previous = previous;
            Current = current;
            EndReason = endReason;
            Elapsed = elapsed;
        }
    }

    public readonly struct FirableChargedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public FirableChargedEvent(IUnit unit, Firable firable)
        {
            Unit = unit;
            Firable = firable;
        }
    }

    public readonly struct FirableEquippedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly FirableSlot Slot;
        public readonly Firable Firable;
        public FirableEquippedEvent(IUnit unit, FirableSlot slot, Firable firable)
        {
            Unit = unit;
            Slot = slot;
            Firable = firable;
        }
    }

    public readonly struct FirableUnequippedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly FirableSlot Slot;
        public readonly Firable Firable;
        public FirableUnequippedEvent(IUnit unit, FirableSlot slot, Firable firable)
        {
            Unit = unit;
            Slot = slot;
            Firable = firable;
        }
    }

    public readonly struct CooldownStartedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public readonly float Duration;
        public CooldownStartedEvent(IUnit unit, Firable firable, float duration)
        {
            Unit = unit;
            Firable = firable;
            Duration = duration;
        }
    }

    public readonly struct CooldownEndedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public CooldownEndedEvent(IUnit unit, Firable firable)
        {
            Unit = unit;
            Firable = firable;
        }
    }

    public readonly struct ChargesChangedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public readonly int Charges;
        public readonly int MaxCharges;
        public ChargesChangedEvent(IUnit unit, Firable firable, int charges, int maxCharges)
        {
            Unit = unit;
            Firable = firable;
            Charges = charges;
            MaxCharges = maxCharges;
        }
    }

    public readonly struct AmmoChangedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public readonly int Current;
        public readonly int Capacity;
        public AmmoChangedEvent(IUnit unit, Firable firable, int current, int capacity)
        {
            Unit = unit;
            Firable = firable;
            Current = current;
            Capacity = capacity;
        }
    }

    public readonly struct ReloadStartedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public readonly float Duration;
        public ReloadStartedEvent(IUnit unit, Firable firable, float duration)
        {
            Unit = unit;
            Firable = firable;
            Duration = duration;
        }
    }

    public readonly struct ReloadCompletedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly Firable Firable;
        public ReloadCompletedEvent(IUnit unit, Firable firable)
        {
            Unit = unit;
            Firable = firable;
        }
    }
}
