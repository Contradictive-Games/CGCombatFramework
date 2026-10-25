using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Immune With Tag")]
    [BlockInfo("Cancels the hit as Immune when the defender has the tag, or the immunity tag of the hit's damage type.", Example = "Tag State.Invulnerable during a dodge roll")]
    public sealed class ImmuneWithTagStep : IDamageStep
    {
        [SerializeField, Tooltip("What tag must be present to cancel damage")] private CombatTag tag;

        public void Process(ref DamageCalculation calc)
        {
            var tags = calc.TargetUnit.Tags;
            var type = calc.Request.Type;
            if (tags.Has(tag) || (type != null && tags.Has(type.ImmunityTag))) calc.Cancel(HitResult.Immune);
        }
    }
}
