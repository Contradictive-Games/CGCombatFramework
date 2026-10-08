using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Armor")]
    public sealed class ArmorStep : IDamageStep
    {
        [SerializeField] private StatType armorStat;
        [SerializeField, Min(1f), Tooltip("Armor equal to this will half the damage")] private float halfAt = 100f;

        public void Process(ref DamageCalculation calc)
        {
            if(calc.Request.Has(DamageFlags.IgnoreArmor)) return;
            float value = Mathf.Max(0f, calc.TargetUnit.GetStat(armorStat));
            calc.Amount *= halfAt / (halfAt + value);
        }
    }
}