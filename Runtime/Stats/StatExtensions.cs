using UnityEngine;

namespace CG.CombatFramework
{
    public static class StatExtensions
    {
        public static float GetStat(this IUnit unit, StatType type, float fallback = 0f)
        {
            if(unit == null || type == null) return fallback;
            return unit.TryGet<IStatProvider>(out var stats) ? stats.GetValue(type, fallback) : fallback;
        }

        public static float GetRate(this IUnit unit, StatType stat) => stat != null ? Mathf.Max(0.01f, unit.GetStat(stat, 1f)) : 1f;
    }
}
