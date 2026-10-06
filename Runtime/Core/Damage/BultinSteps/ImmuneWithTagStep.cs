using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Immune With Tag")]
    public sealed class ImmuneWithTagStep : IDamageStep
    {
        [SerializeField, Tooltip("What tag must be present to cancel damage")] private CombatTag tag;

        public void Process(ref DamageCalculation calc)
        {
            if(tag != null && calc.TargetUnit.Tags.Has(tag)) calc.Cancel(HitResult.Immune);
        }
    }
}