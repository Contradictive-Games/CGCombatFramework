using UnityEngine;

namespace CG.CombatFramework
{
    /// <summary>
    /// Every hit is allowed to do this minimum amount of damage (or the minimum % of the original amount at this point)
    /// </summary>
    [System.Serializable, TypeMenu("Rules/Minimum Damage")]
    [BlockInfo("Raises the damage to at least Minimum, or Minimum Share of the original hit, whichever is higher. Never above the original hit.", Example = "After Armor, so heavily armored targets still take 1 damage")]
    public sealed class MinimumDamageStep : IDamageStep
    {
        [SerializeField, Tooltip("The minimum damage that will be done"), Min(0f)] 
        private float minimum = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Or this % of the original amount, whichever is higher")]
        private float minimumShare;

        public void Process(ref DamageCalculation calc)
        {
            float requested = calc.Request.Amount;
            if(requested <= 0f) return;
            float floor = Mathf.Min(Mathf.Max(minimum, requested * minimumShare), requested);
            if(calc.Amount < floor) calc.Amount = floor;
        }
    }
}