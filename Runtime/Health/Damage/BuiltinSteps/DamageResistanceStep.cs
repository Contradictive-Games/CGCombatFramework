using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Defender/Damage Resistance Step")]
    [BlockInfo("Reduces damage by the defender's resistance stat for the hit's damage type, up to Cap.", Example = "0.5 Fire Resistance turns a 20 fire hit into 10")]
    public sealed class DamageResistanceStep : IDamageStep
    {
        [SerializeField, Range(0f, 1f), Tooltip("The highest the resistance % we will go")]
        private float cap = 0.9f;

        public void Process(ref DamageCalculation calc)
        {
            var type = calc.Request.Type;
            if(type == null || type.ResistanceStat == null) return;
            float resist = Mathf.Min(calc.TargetUnit.GetStat(type.ResistanceStat), cap);
            calc.Amount *= 1f - resist;
        }
    }
}
