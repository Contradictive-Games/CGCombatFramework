using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Immune With Tag")]
    public sealed class ImmuneWithTagStep : IDamageStep
    {
        [SerializeField, Tooltip("What tag must be present to cancel damage")] private CombatTag tag;

        public void Process(ref DamageCalculation calc)
        {
            var type = calc.Request.Type;
            if (type == null) return;

            if (type.ImmunityTag != null && calc.TargetUnit.Tags.Has(type.ImmunityTag))
            {
                calc.Cancel(HitResult.Immune);
                return;
            }
            if (tag != null && calc.TargetUnit.Tags.Has(tag)) calc.Cancel(HitResult.Immune);
        }
    }
}
