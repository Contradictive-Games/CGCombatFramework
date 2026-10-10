using UnityEngine;

namespace CG.CombatFramework
{
    public enum StatScaleMode { Multiply, AddPerPoint }

    [System.Serializable, TypeMenu("Stat")]
    public sealed class StatScaler : IValueScaler
    {
        [SerializeField] private Subject subject = Subject.Source;
        [SerializeField] private StatType stat;
        [SerializeField, Tooltip("Multipley: value x stat (i.e: damage mult. state, 1= normal)\nAdd Per Point: value + stat * Per Point")] 
        private StatScaleMode mode = StatScaleMode.Multiply;
        [SerializeField] private float perPoint = 1f;

        public float Apply(float value, in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit == null || stat == null) return value;
            return mode == StatScaleMode.Multiply
            ? value * unit.GetStat(stat, 1f)
            : value * unit.GetStat(stat) * perPoint;
        }
    }
}