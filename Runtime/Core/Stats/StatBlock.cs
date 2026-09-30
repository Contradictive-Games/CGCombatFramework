using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class StatBlock : IStatProvider
    {
        private readonly Dictionary<StatType, IStat> statLookup;

        public StatBlock(UnitStatProfile profile)
        {
            statLookup = new();
            for(int i = 0; i < profile.Stats.Count; i++)
            {
                var entry = profile.Stats[i];
                if(statLookup.ContainsKey(entry.Type)) continue;
                statLookup.Add(entry.Type, new Stat(entry.Type, entry.BaseValue));
            }
        }

        public bool TryAddStat(StatType statType)
        {
            if(statLookup.ContainsKey(statType)) return false;
            statLookup.Add(statType, new Stat(statType, statType.DefaultValue));
            return true;
        }

        public float GetValue(StatType type, float fallback = 0)
        {
            return statLookup.TryGetValue(type, out var stat) ? stat.Value : fallback;
        }


        public bool TryGetStat(StatType type, out IStat stat)
        {
            if(!statLookup.TryGetValue(type, out stat)) return false;
            return true;
        }
    }
}
