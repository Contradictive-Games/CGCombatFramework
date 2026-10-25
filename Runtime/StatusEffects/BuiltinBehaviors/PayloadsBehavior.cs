namespace CG.CombatFramework
{
    [System.Flags]
    public enum PayloadEvent
    {
        Applied,
        Restacked,
        Tick,
        Expired,
        Removed
    }
    
    [System.Serializable, TypeMenu("Payload")]
    public sealed class PayloadsBehavior : IStatusEffectBehavior
    {

    }
}
