namespace CG.CombatFramework
{
    public enum FiringEndReason
    {
        None,
        Fired,
        Released,
        Complated,
        Interrupted,
        Cancelled
    }
    public readonly struct FiringPhaseChangedEvent
    {
        public readonly IUnit Owner;
        public readonly Firable Firable;
        public readonly FiringPhase Previous;
        public readonly FiringPhase Current;
        public readonly FiringEndReason EndReason;
        public readonly float Elapsed;
        public FiringPhaseChangedEvent(IUnit owner, Firable firable, FiringPhase previous,
            FiringPhase current, FiringEndReason endReason, float elapsed)
        {
            Owner = owner;
            Firable = firable;
            Previous = previous;
            Current = current;
            EndReason = endReason;
            Elapsed = elapsed;
        }
    }

    public readonly struct FirableChargedEvent
    {
        public readonly IUnit Owner;
        public readonly Firable Firable;
        public FirableChargedEvent(IUnit owner, Firable firable)
        {
            Owner = owner;
            Firable = firable;
        }
    }

    public readonly struct FirableEquippedEvent
    {
        public readonly IUnit Owner;
        public readonly FirableSlot Slot;
        public readonly Firable Firable;
        public FirableEquippedEvent(IUnit owner, FirableSlot slot, Firable firable)
        {
            Owner = owner;
            Slot = slot;
            Firable = firable;
        }
    }

    public readonly struct FirableUnequippedEvent
    {
        public readonly IUnit Owner;
        public readonly FirableSlot Slot;
        public readonly Firable Firable;
        public FirableUnequippedEvent(IUnit owner, FirableSlot slot, Firable firable)
        {
            Owner = owner;
            Slot = slot;
            Firable = firable;
        }
    }

    public readonly struct CooldownStartedEvent
    {
        public readonly IUnit Owner;
        public readonly Firable Firable;
        public readonly float Duration;
        public CooldownStartedEvent(IUnit owner, Firable firable, float duration)
        {
            Owner = owner;
            Firable = firable;
            Duration = duration;
        }
    }

    public readonly struct CooldownEndedEvent
    {
        public readonly IUnit Owner;
        public readonly FirableDefinition Firable;
        public CooldownEndedEvent(IUnit owner, FirableDefinition firable)
        {
            Owner = owner;
            Firable = firable;
        }
    }

    public readonly struct ChargesChangedEvent
    {
        public readonly IUnit Owner;
        public readonly FirableDefinition Firable;
        public readonly int Charges;
        public readonly int MaxCharges;
        public ChargesChangedEvent(IUnit owner, FirableDefinition firable, int charges, int maxCharges)
        {
            Owner = owner;
            Firable = firable;
            Charges = charges;
            MaxCharges = maxCharges;
        }
    }

    public readonly struct AmmoChangedEvent
    {
        public readonly IUnit Owner;
        public readonly FirableDefinition Firable;
        public readonly int Max;
        public readonly int Current;
        public AmmoChangedEvent(IUnit owner, FirableDefinition firable, int max, int current)
        {
            Owner = owner;
            Firable = firable;
            Max = max;
            Current = current;
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
