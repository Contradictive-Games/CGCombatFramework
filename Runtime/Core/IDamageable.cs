namespace CG.RGS.CombatFramework
{
    public interface IDamageable
    {
        public Resource Health { get; }
        public void Damage(DamageContext ctx);
    }
}