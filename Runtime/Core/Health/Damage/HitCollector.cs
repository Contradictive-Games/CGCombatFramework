using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace CG.CombatFramework
{
    public sealed class HitCollector
    {
        private static readonly Stack<HitCollector> pool = new();

        private readonly List<HitCandidate> hits = new();
        private readonly HashSet<IUnit> seen = new();

        public IReadOnlyList<HitCandidate> Hits => hits;

        public static HitCollector Rent()
        {
            return pool.Count > 0 ? pool.Pop() : new HitCollector();
        }

        public static void Return(HitCollector collector)
        {
            collector.hits.Clear();
            collector.seen.Clear();
            pool.Push(collector);
        }

        public bool Add(IUnit unit, in HitData hit, float distance)
        {
            if(unit == null || !seen.Add(unit)) return false;
            hits.Add(new HitCandidate(unit, in hit, distance));
            return true;
        }

        public void SortByDistance() => hits.Sort(DistanceComparer.Instance);

        private sealed class DistanceComparer : IComparer<HitCandidate>
        {
            public static readonly DistanceComparer Instance = new();
            public int Compare(HitCandidate a, HitCandidate b) => a.Distance.CompareTo(b.Distance);
        }
    }
}