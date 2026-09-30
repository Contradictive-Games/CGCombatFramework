namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("New Menu/Integer Item")]
    public sealed class SeparateSubMenuItem : IBaseInterface
    {
        public string Id => "OtherSubMenuItem";
        public int Integer;
    }
}