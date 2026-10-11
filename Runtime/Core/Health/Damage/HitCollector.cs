using System.Collections.Generic;

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

        public void SortByDistance()
        {
            for(int i = 1; i < hits.Count; i++)
            {
                var current = hits[i];
                int j = i - 1;
                while(j >= 0 && hits[j].Distance > current.Distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = current;
            }
        }
    }
}