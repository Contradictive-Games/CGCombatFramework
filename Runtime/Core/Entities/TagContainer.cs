using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class TagContainer
    {
        private readonly Dictionary<CombatTag, int> counts = new();

        public event Action<CombatTag> Added;
        public event Action<CombatTag> Removed;

        public void Add(CombatTag tag)
        {
            
        }

        public void Remove(CombatTag tag)
        {
            
        }

        public bool HasExact(CombatTag tag) => tag != null && counts.ContainsKey(tag);

        public bool Has(CombatTag tag)
        {
            return false;
        }

        public bool HasAny(IReadOnlyList<CombatTag> tags)
        {
            return false;
        }

        public IEnumerable<CombatTag> All => counts.Keys;
    }
}