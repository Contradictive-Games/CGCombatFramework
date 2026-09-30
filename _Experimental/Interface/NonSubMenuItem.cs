namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("BoolItem")]
    public sealed class NonSubMenuItem : IBaseInterface
    {
        public string Id => "NonSubMenuItem";
        public bool IsTrue;
    }
}