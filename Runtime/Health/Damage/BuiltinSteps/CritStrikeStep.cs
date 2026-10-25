using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Attacker/Critical Strike")]
    [BlockInfo("Rolls the attacker's crit chance stat. On a crit the damage is multiplied by the crit damage stat (or Default Crit Damage) and the hit is marked as a crit.", Example = "25% crit chance and 2.0 crit damage: a 40 hit becomes 80 about one time in four")]
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