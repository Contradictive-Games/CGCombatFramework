namespace CG.CombatFramework
{
    public static class UnitExtensions
    {
        public static IUnit Root(this IUnit unit)
        {
            var current = unit;
            while(current?.Owner != null) current = current.Owner;
            return current;
        }
    }
}