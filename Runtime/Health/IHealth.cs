namespace CG.CombatFramework
{
    public interface IHealth
    {
        public IReadonlyResource Resource { get; }

        public bool IsDead { get; }
        public DeathInfo? LastDeath => null;

        public DamageResult ApplyDamage(in DamageRequest request);
        public HealResult ApplyHeal(float amount, in EffectContext ctx);

        public void Kill(in EffectContext ctx);
        public void Revive(float normalized = 1f, in EffectContext ctx = default);

        public void AddListener(IHealthListener listener);
        public void RemoveListener(IHealthListener listener);
    }
}