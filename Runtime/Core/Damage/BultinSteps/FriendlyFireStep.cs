using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Rules/No Friendly Fire")]
    public class FriendlyFireStep : IDamageStep
    {
        [SerializeField] private bool allowSelfDamage = true;

        public void Process(ref DamageCalculation calc)
        {
            var source = calc.Request.Context.SourceUnit;
            if(source == null) return;
            if(ReferenceEquals(source.Root(), calc.TargetUnit.Root()))
            {
                if(!allowSelfDamage) calc.Cancel(HitResult.Immune);
                return;
            }
            if(source.RelationTo(calc.TargetUnit) == FactionRelation.Friendly) calc.Cancel(HitResult.Immune);
        }
    }
}
