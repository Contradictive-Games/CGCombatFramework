using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Attacker/Critical Strike")]
    public sealed class CritStrikeStep : IDamageStep
    {
        [SerializeField, Tooltip("Source stat for crit chance. 0 -> 1")] private StatType critChanceStat;
        [SerializeField, Tooltip("Source stat for crit damage. 1.5 = +50%")] private StatType critDamageStat;
        [SerializeField, Tooltip("Fallback crit damage if not critDamageStat is present")] private float defaultCritDamage = 1.5f;
        [SerializeField, Tooltip("What tag can prevent this crit")] private CombatTag noCritTag;

        public void Process(ref DamageCalculation calc)
        {
            if(calc.Request.Has(DamageFlags.CannotCrit)) return;
            if(noCritTag != null && calc.TargetUnit.Tags.Has(noCritTag)) return;

            var source = calc.Request.Context.SourceUnit;
            if(!calc.Request.Context.Random.Roll(source.GetStat(critChanceStat))) return;
            calc.IsCrit = true;
            calc.Amount *= source.GetStat(critDamageStat, defaultCritDamage);
        }
    }
}