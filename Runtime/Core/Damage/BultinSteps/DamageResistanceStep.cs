using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Damage Resistance Step")]
    public sealed class DamageResistanceStep : IDamageStep
    {
        [SerializeField, Range(0f, 1f), Tooltip("The highest the resistance % we will go")]
        private float cap = 0.9f;

        public void Process(ref DamageCalculation calc)
        {
            var type = calc.Request.Type;
            if(type == null) return;

            if(type.ImmunityTag != null && calc.TargetUnit.Tags.Has(type.ImmunityTag))
            {
                calc.Cancel(HitResult.Immune);
                return;
            }
            if(type.ResistanceStat == null) return;
            float resist = Mathf.Min(calc.TargetUnit.GetStat(type.ResistanceStat), cap);
            calc.Amount *= 1f - resist;
        }
    }
}