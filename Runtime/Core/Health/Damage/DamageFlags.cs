namespace CG.CombatFramework
{
    [System.Flags]
    public enum DamageFlags
    {
        None = 0,
        CannotCrit = 1 << 0,
        Periodic = 1 << 1,
        IgnoreArmor = 1 << 2,
        IgnoreShield = 1 << 3,
        CannotEvade = 1 << 4
    }
    
}