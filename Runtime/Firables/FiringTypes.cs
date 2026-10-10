using UnityEngine;

namespace CG.CombatFramework
{
    public enum FiringPhase
    {
        Idle,
        Firing,
        Bursting,
        Charging,
        Channeling
    }

    public enum FireBlockReason
    {
        None,
        Paused,
        Dead,
        Cooldown,
        NoCharges,
        NoAmmo,
        Reloading,
        NotEnoughResource,
        Condition
    }

    public readonly struct FiredEvent
    {
        public readonly IUnit Owner;
        public readonly Firable Firable;
        public readonly float Power;

        public FiredEvent(IUnit owner, Firable firable, float power)
        {
            Owner = owner;
            Firable = firable;
            Power = power;
        }
    }

    public readonly struct FireBlockedEvent
    {
        public readonly IUnit Owner;
        public readonly Firable Firable;
        public readonly FireBlockReason Reason;

        public FireBlockedEvent(IUnit owner, Firable firable, FireBlockReason reason)
        {
            Owner = owner;
            Firable = firable;
            Reason = reason;
        }
    }
}
