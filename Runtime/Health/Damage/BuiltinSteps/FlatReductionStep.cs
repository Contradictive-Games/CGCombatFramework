using UnityEngine;

namespace CG.CombatFramework
{
    public sealed class FlatReductionStep : IDamageStep
    {
        [SerializeField, Tooltip("Target stat subtracted from every hit")]
        private StatType reduction;
        [SerializeField, Tooltip("If periodic effects ignore this step")]
        private bool ignorePeriodic = true;

        public void Process(ref DamageCalculation calc)
        {
            if(reduction == null || (ignorePeriodic && calc.Request.Has(DamageFlags.Periodic))) return;
            calc.Amount = Mathf.Max(0f, calc.Amount - calc.TargetUnit.GetStat(reduction));
        }
    }
}