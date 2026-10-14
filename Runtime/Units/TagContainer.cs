using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class TagContainer
    {
        private readonly Dictionary<CombatTag, int> counts = new();

        public event Action<CombatTag> Added;
        public event Action<CombatTag> Removed;

        public IEnumerable<CombatTag> All => counts.Keys;

        public void Add(CombatTag tag)
        {
            if(tag == null) return;
            counts.TryGetValue(tag, out int count);
            counts[tag] = count + 1;
            if(count == 0) Added?.Invoke(tag);
        }

        public void Remove(CombatTag tag)
        {
            if(tag == null || !counts.TryGetValue(tag,out int count)) return;
            if(count > 1)
            {
                counts[tag] = count - 1;
                return;
            }
            counts.Remove(tag);
            Removed?.Invoke(tag);
        }

        public bool HasExact(CombatTag tag) => tag != null && counts.ContainsKey(tag);

        public bool Has(CombatTag tag)
        {
            if(tag == null) return false;
            if(counts.ContainsKey(tag)) return true;
            foreach(var held in counts.Keys)
            {
                if(held.IsOrDescendedFrom(tag)) return true;
            }
            return false;
        }

        public bool HasAny(CombatTag[] tags)
        {
            if(tags == null) return false;
            for(int i =0; i < tags.Length; i++)
            {
                if(Has(tags[i])) return true;
            }
            return false;
        }


        internal void Clear() => counts.Clear();
    }
}