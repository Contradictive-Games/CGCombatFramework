using System.Collections.Generic;
using UnityEngine;

namespace CG.RGS.CombatFramework
{
    public sealed class StatBlock
    {
        public Dictionary<StatType, Stat> Stats { get; private set; }


        public Stat GetStat(StatType type)
        {
            if(Stats.TryGetValue(type, out Stat stat))
            {
                return stat;
            }
            return null;
        }


        public bool TryAddModifierToStat(StatType type, StatModifier modifier)
        {
            Stat stat = GetStat(type);
            if(stat == null) return false;
            stat.AddModifier(modifier);
            return true;
        }

        public bool TryRemoveModifierFromStat(StatType type, StatModifier modifier)
        {
            Stat stat = GetStat(type);
            if(stat == null) return false;
            stat.RemoveModifier(modifier);
            return true;
        }

        public bool TryRemoveModifiersFromSourceFromStat(StatType type, StatModifier modifier, object source)
        {
            Stat stat = GetStat(type);
            if(stat == null) return false;
            stat.RemoveAllModifiersFromSource(source);
            return true;
        }


        public bool TryAddStat(StatType type, Stat stat)
        {
            if(Stats.ContainsKey(type))
            {
                Debug.LogError($"Tried to add duplicate stat type '{type.Name}'");
                return false;
            }
            Stats.Add(type, stat);
            return true;
        }

        public bool TryRemoveStat(StatType type)
        {
            if(!Stats.ContainsKey(type))
            {
                return false;
            }
            Stats.Remove(type);
            return true;
        }
    }
}
