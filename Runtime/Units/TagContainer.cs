using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class TagContainer
    {
        private readonly Dictionary<CombatTag, int> counts = new();


        public IEnumerable<CombatTag> All => counts.Keys;

        private readonly IUnit owner;
        public TagContainer(IUnit owner)
        {
            this.owner = owner;
        }

        public void Add(CombatTag tag)
        {
            if(tag == null) return;
            counts.TryGetValue(tag, out int count);
            counts[tag] = count + 1;
            if(count == 0) owner?.Events?.Raise(new TagAddedEvent(owner, tag));
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
            owner?.Events?.Raise(new TagRemovedEvent(owner, tag));
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


        internal void Clear() {
            foreach(var (tag, _) in counts)
            {
                owner?.Events?.Raise(new TagRemovedEvent(owner, tag));
            }
            counts.Clear();
        }
    }
}
