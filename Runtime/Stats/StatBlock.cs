using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class StatBlock : IStatProvider
    {
        private readonly Dictionary<StatType, Stat> statLookup = new(ReferenceComparer<StatType>.Instance);
        private readonly Dictionary<StatType, float> baseValues = new(ReferenceComparer<StatType>.Instance);
        private readonly IUnit owner;

        public StatBlock(UnitStatProfile profile, IUnit owner = null)
        {
            this.owner = owner;
            if(profile == null) return;
            for(int i = 0; i < profile.Stats.Length; i++)
            {
                var stat = profile.Stats[i];
                if(stat.Type == null || statLookup.ContainsKey(stat.Type)) continue;
                statLookup.Add(stat.Type, new Stat(stat.Type, stat.BaseValue, owner));
                baseValues.Add(stat.Type, stat.BaseValue);
            }
        }

        public float GetValue(StatType type, float fallback = 0)
        {
            return !(type is null) && statLookup.TryGetValue(type, out var stat) ? stat.Value : fallback;
        }


        public bool TryGetStat(StatType type, out IStat stat)
        {
            if(!(type is null) && statLookup.TryGetValue(type, out var found))
            {
                stat = found;
                return true;
            }
            stat = null;
            return false;
        }

        public IStat GetOrCreate(StatType type)
        {
            if(type == null) throw new ArgumentNullException(nameof(type));
            if(!statLookup.TryGetValue(type, out var stat))
            {
                stat = new Stat(type, type.DefaultValue, owner);
                statLookup.Add(type, stat);
            }
            return stat;
        }

        public int RemoveAllModifiersFromSource(object source)
        {
            int removed = 0;
            foreach(var stat in statLookup.Values)
            {
                removed += stat.RemoveAllModifiersFromSource(source);
            }
            return removed;
        }

        public void ResetToProfile()
        {
            foreach(var stat in statLookup.Values)
            {
                stat.Reset(baseValues.TryGetValue(stat.Type, out var value) ? value : stat.Type.DefaultValue);
            }
        }
    }
}
