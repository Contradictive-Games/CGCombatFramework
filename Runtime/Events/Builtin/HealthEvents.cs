namespace CG.CombatFramework
{
    public readonly struct DamageReceivedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Source;
        public readonly DamageResult Result;
        public DamageReceivedEvent(IUnit unit, IUnit source, in DamageResult result)
        {
            Unit = unit;
            Source = source;
            Result = result;
        }
    }

    public readonly struct DamageDealtEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Target;
        public readonly DamageResult Result;
        public DamageDealtEvent(IUnit unit, IUnit target, in DamageResult result)
        {
            Unit = unit;
            Target = target;
            Result = result;
        }
    }

    public readonly struct HealReceivedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Source;
        public readonly HealResult Result;
        public HealReceivedEvent(IUnit unit, IUnit source, in HealResult result)
        {
            Unit = unit;
            Source = source;
            Result = result;
        }
    }

    public readonly struct HealDealtEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Target;
        public readonly HealResult Result;
        public HealDealtEvent(IUnit unit, IUnit target, in HealResult result)
        {
            Unit = unit;
            Target = target;
            Result = result;
        }
    }

    public readonly struct DiedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Killer;
        public readonly DeathInfo Death;
        public DiedEvent(in DeathInfo death)
        {
            Unit = death.Victim;
            Killer = death.Killer;
            Death = death;
        }
    }

    public readonly struct KilledUnitEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Victim;
        public readonly DeathInfo Death;
        public KilledUnitEvent(in DeathInfo death)
        {
            Unit = death.Killer;
            Victim = death.Victim;
            Death = death;
        }
    }

    public readonly struct RevivedEvent : ICombatEvent
    {
        public readonly IUnit Unit;
        public readonly IUnit Source;
        public readonly float Health;
        public RevivedEvent(IUnit unit, IUnit source, float health)
        {
            Unit = unit;
            Source = source;
            Health = health;
        }
    }
}
