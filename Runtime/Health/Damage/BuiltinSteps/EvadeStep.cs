using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Evade")]
    public sealed class EvadeStep : IDamageStep
    {
        [SerializeField, Tooltip("Chance stat to check against, 0 - 1")] private StatType evadeChance;
        [SerializeField, Range(0f, 1f), Tooltip("Highest chance allowed no matter stat stacks")] private float cap = 0.75f;
        [SerializeField, Tooltip("If periodic effects can't be evaded")] private bool ignorePeriodic = true;

        public void Process(ref DamageCalculation calc)
        {
            if (evadeChance == null || calc.Request.Has(DamageFlags.CannotEvade)) return;
            if (ignorePeriodic && calc.Request.Has(DamageFlags.Periodic)) return;
            float chance = Mathf.Min(calc.TargetUnit.GetStat(evadeChance), cap);
            if (calc.Request.Context.Random.Roll(chance)) calc.Cancel(HitResult.Evaded);
        }
    }
}
