namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Conditions & Triggers/Condition")]
    public sealed class ConditionInterface : IBaseInterface
    {
        public string Id => ConditionName;
        public string ConditionName = "Condition";
    }
}