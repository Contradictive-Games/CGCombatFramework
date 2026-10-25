using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Flat Reduction")]
    [BlockInfo("Subtracts the defender's stat from every hit, never going below 0. Periodic damage can be left out.", Example = "3 Toughness turns a 10 hit into 7 and a 2 hit into 0")]
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
