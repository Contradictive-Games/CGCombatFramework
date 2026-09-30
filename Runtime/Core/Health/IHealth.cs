using System;

namespace CG.CombatFramework
{
    public interface IHealth
    {
        public IReadonlyResource Resource { get; }
        
        public bool IsDead { get; }

        public DamageResult ApplyDamage(in DamageRequest request);
        public HealResult ApplyHeal(float amount, in EffectContext ctx);

        public void Kill(in EffectContext ctx);
        public void Revive(float normalized = 1f);

        public event Action<DamageResult> OnDamaged;
        public event Action<HealResult> OnHealed;
        public event Action<DeathInfo> OnDied;
        public event Action OnRevived;
    }
}