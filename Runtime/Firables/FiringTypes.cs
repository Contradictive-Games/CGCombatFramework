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
}
