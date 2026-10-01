namespace CG.CombatFramework
{
    public static class UnitExtensions
    {
        /// <summary>
        /// Get Owner to the top-level unit
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public static IUnit Root(this IUnit unit)
        {
            var current = unit;
            while(current?.Owner != null) current = current.Owner;
            return current;
        }

        public static UnitHandle ToHandle(this IUnit unit) => new(unit);
    }
}