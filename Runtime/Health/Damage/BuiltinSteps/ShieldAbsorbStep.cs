using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Shield Absorb")]
    [BlockInfo("Takes Absorb Share of the hit out of the defender's shield resource first. Whatever the shield can't take carries on. Hits while the shield is empty restart its Delay After Loss. Skipped by Ignore Shield.", Example = "Absorb Share 1 with 50 shield: a 30 hit leaves 20 shield and deals 0 to health")]
    public sealed class ShieldAbsorbStep : IDamageStep
    {
        [SerializeField]
        private ResourceType shield;
        [SerializeField, Range(0f, 1f), Tooltip("Share of each hit the shield will take. 1 = shield takes all, 0.5 = half goes to shield and half goes to health")]
        private float absorbShare = 1f;
        [SerializeField, Tooltip("Off = hits while the shield is empty restart its Delay After Loss, so it only recharges once the unit stops taking damage\nOn = the delay only counts from when the shield broke")]
        private bool delayFromBreakOnly;

        public void Process(ref DamageCalculation calc)
        {
            if(shield == null || calc.Amount <= 0f || calc.Request.Has(DamageFlags.IgnoreShield)) return;
            if(!calc.TargetUnit.TryGet<IResourceProvider>(out var provider) || !provider.TryGet(shield, out var r)) return;
            if(r.IsDepleted)
            {
                if(!delayFromBreakOnly) r.RestartLossTimer();
                return;
            }

            float absorbed = r.Remove(calc.Amount * absorbShare, ChangeReason.Damage, calc.Request.Context);
            calc.Absorbed += absorbed;
            calc.Amount -= absorbed;
        }
    }
}