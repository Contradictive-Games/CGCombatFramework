using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Shield Absorb")]
    public sealed class ShieldAbsorbStep : IDamageStep
    {
        [SerializeField]
        private ResourceType shield;
        [SerializeField, Range(0f, 1f), Tooltip("Share of each hit the shield will take. 1 = shield takes all, 0.5 = half goes to shield and half goes to health")]
        private float absorbShare = 1f;

        public void Process(ref DamageCalculation calc)
        {
            if(shield == null || calc.Amount <= 0f || calc.Request.Has(DamageFlags.IgnoreShield)) return;
            if(!calc.TargetUnit.TryGet<IResourceProvider>(out var provider) || !provider.TryGet(shield, out var r) || r.IsDepleted) return;

            float absorbed = r.Remove(calc.Amount * absorbShare, ChangeReason.Damage, calc.Request.Context);
            calc.Absorbed += absorbed;
            calc.Amount -= absorbed;
        }
    }
}