using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class TagContainer
    {
        private readonly Dictionary<CombatTag, int> counts = new();
        private readonly List<CombatTag> clearing = new();
        private readonly IUnit owner;

        public IEnumerable<CombatTag> All => counts.Keys;

        public TagContainer(IUnit owner)
        {
            this.owner = owner;
        }

        public void Add(CombatTag tag)
        {
            if(tag == null) return;
            counts.TryGetValue(tag, out int count);
            counts[tag] = count + 1;
            if(count == 0 && owner != null) GameEvents.Raise(owner, new TagAddedEvent(owner, tag));
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
            if(owner != null) GameEvents.Raise(owner, new TagRemovedEvent(owner, tag));
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


        internal void Clear()
        {
            if(counts.Count == 0) return;
            clearing.Clear();
            foreach(var tag in counts.Keys)
            {
                clearing.Add(tag);
            }
            counts.Clear();
            if(owner != null)
            {
                for(int i = 0; i < clearing.Count; i++)
                {
                    GameEvents.Raise(owner, new TagRemovedEvent(owner, clearing[i]));
                }
            }
            clearing.Clear();
        }
    }
}
