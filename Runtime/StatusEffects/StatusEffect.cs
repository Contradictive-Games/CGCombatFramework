namespace CG.RGS.CombatFramework
{
    public abstract class StatusEffect
    {
        public virtual void OnApply(StatusEffectContext ctx)
        {

        }

        public virtual void OnRemove(StatusEffectContext ctx)
        {

        }

        public virtual void Tick(StatusEffectContext ctx, float deltaTime)
        {

        }
    }
}
