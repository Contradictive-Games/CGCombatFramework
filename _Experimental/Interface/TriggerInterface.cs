namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Conditions & Triggers/Trigger")]
    public sealed class TriggerInterface : IBaseInterface
    {
        public string Id => TriggerName;
        public string TriggerName = "Trigger";
    }
}