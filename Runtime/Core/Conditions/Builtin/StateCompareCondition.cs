using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Stat Compare")]
    public sealed class StateCompareCondition : ICondition
    {
        [SerializeField] private Subject subject = Subject.Source;
        [SerializeField] private StatType stat;
        [SerializeField] private ThresholdComparison comparison = ThresholdComparison.AtOrAbove;
        [SerializeField] private float value = 1f;

        public bool Evaluate(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit == null || stat == null) return false;
            float current = unit.GetStat(stat);
            return comparison == ThresholdComparison.Below ? current < value : current >= value;
        }
    }
}
