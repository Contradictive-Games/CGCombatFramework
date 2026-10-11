namespace CG.CombatFramework
{
    internal static class ArrayExtensions
    {
        public static T[] OrEmpty<T>(this T[] array) => array ?? System.Array.Empty<T>();
    }
}