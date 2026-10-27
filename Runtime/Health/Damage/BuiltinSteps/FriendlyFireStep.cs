using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Rules/No Friendly Fire")]
    [BlockInfo("Cancels damage between friendly factions. Damage to yourself (or from anything you own) follows Allow Self Damage.", Example = "Allies' grenades don't hurt each other, but your own rocket jump still can")]
    public sealed class FriendlyFireStep : IDamageStep
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
