using System.Threading;

namespace CG.CombatFramework
{
    internal static class CapabilitySlots
    {
        private static int count;

        internal static int Next() => Interlocked.Increment(ref count) - 1;
    }

    internal static class CapabilitySlot<T> where T : class
    {
        internal static readonly int Index = CapabilitySlots.Next();
    }
}
