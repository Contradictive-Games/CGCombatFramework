using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class StatBlock : IStatProvider
    {
        private readonly Dictionary<StatType, Stat> statLookup = new();
        private readonly Dictionary<StatType, float> baseValues = new();

        public StatBlock(UnitStatProfile profile)
        {
            if(profile == null) return;
            for(int i = 0; i < profile.Stats.Length; i++)
            {
                var stat = profile.Stats[i];
                if(stat.Type == null || statLookup.ContainsKey(stat.Type)) continue;
                statLookup.Add(stat.Type, new Stat(stat.Type, stat.BaseValue));
                baseValues.Add(stat.Type, stat.BaseValue);
            }
        }

        public float GetValue(StatType type, float fallback = 0)
        {
            return type != null && statLookup.TryGetValue(type, out var stat) ? stat.Value : fallback;
        }


        public bool TryGetStat(StatType type, out IStat stat)
        {
            if(type != null && statLookup.TryGetValue(type, out var found))
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
                stat = new Stat(type, type.DefaultValue);
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
