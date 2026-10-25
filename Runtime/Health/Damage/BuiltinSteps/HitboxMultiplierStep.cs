using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Hitbox Multiplier")]
    [BlockInfo("Multiplies damage by the hit Hitbox's multiplier. When it is above 1, the attacker's Weak Point Bonus stat is added to it.", Example = "A 2.0 head hitbox with 0.5 weak point bonus turns a 10 hit into 25")]
    public sealed class HitboxMultiplierStep : IDamageStep
    {
        [SerializeField, Tooltip("Optional source stat that is flat added to multilpiers above 1. 0.5 turns a 200% damage increase -> 250%")]
        private StatType weakPointBonus;

        public void Process(ref DamageCalculation calc)
        {
            float multiplier = calc.Request.Context.Hit.Multiplier;
            if(multiplier > 1f && weakPointBonus != null)
            {
                multiplier += calc.Request.Context.SourceUnit.GetStat(weakPointBonus);
            }
            calc.Amount *= multiplier;
        }
    }
}