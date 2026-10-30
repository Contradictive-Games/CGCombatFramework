using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class TagContainer
    {
        private struct Held
        {
            public int Count;
            public CombatTag[] Lineage;
        }

        private readonly Dictionary<CombatTag, Held> held = new(ReferenceComparer<CombatTag>.Instance);
        private readonly Dictionary<CombatTag, int> covered = new(ReferenceComparer<CombatTag>.Instance);
        private readonly List<CombatTag> clearing = new();
        private readonly IUnit owner;

        public IEnumerable<CombatTag> All => held.Keys;

        public TagContainer(IUnit owner)
        {
            this.owner = owner;
        }

        public void Add(CombatTag tag)
        {
            if(tag is null) return;
            if(held.TryGetValue(tag, out var entry))
            {
                entry.Count++;
                held[tag] = entry;
                return;
            }
            var lineage = tag.Lineage;
            held.Add(tag, new Held { Count = 1, Lineage = lineage });
            Cover(lineage, 1);
            if(owner != null) GameEvents.Raise(owner, new TagAddedEvent(owner, tag));
        }

        public void Remove(CombatTag tag)
        {
            if(tag is null || !held.TryGetValue(tag, out var entry)) return;
            if(entry.Count > 1)
            {
                entry.Count--;
                held[tag] = entry;
                return;
            }
            held.Remove(tag);
            Cover(entry.Lineage, -1);
            if(owner != null) GameEvents.Raise(owner, new TagRemovedEvent(owner, tag));
        }

        public bool HasExact(CombatTag tag) => !(tag is null) && held.ContainsKey(tag);

        public bool Has(CombatTag tag) => !(tag is null) && covered.ContainsKey(tag);

        public bool HasAny(CombatTag[] tags)
        {
            if(tags == null) return false;
            for(int i =0; i < tags.Length; i++)
            {
                if(Has(tags[i])) return true;
            }
            return false;
        }


        internal void Clear()
        {
            if(held.Count == 0) return;
            clearing.Clear();
            foreach(var tag in held.Keys)
            {
                clearing.Add(tag);
            }
            held.Clear();
            covered.Clear();
            if(owner != null)
            {
                for(int i = 0; i < clearing.Count; i++)
                {
                    GameEvents.Raise(owner, new TagRemovedEvent(owner, clearing[i]));
                }
            }
            clearing.Clear();
        }

        private void Cover(CombatTag[] lineage, int delta)
        {
            for(int i = 0; i < lineage.Length; i++)
            {
                var tag = lineage[i];
                covered.TryGetValue(tag, out int count);
                count += delta;
                if(count > 0) covered[tag] = count;
                else covered.Remove(tag);
            }
        }
    }
}
