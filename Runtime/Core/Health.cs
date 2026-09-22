using UnityEngine;

namespace CG.RGS.CombatFramework
{
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        public Resource Health { get; private set; }

        public int MaxHealth => Health.MaxValue;
        public int CurrentHealth => Health.CurrentValue;

        public void Damage(DamageContext ctx)
        {
            int finalValue = Mathf.RoundToInt(DamageResolver.CalculateFinalValue(ctx, new TargetContext()));
            Health.Decrement(finalValue);
        }
    }
}
