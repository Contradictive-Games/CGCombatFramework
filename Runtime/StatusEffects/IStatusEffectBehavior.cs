namespace CG.CombatFramework
{
    public interface IStatusEffectBehavior
    {
        public void OnApplied(StatusEffectInstance effect) { }
        public void OnStacksChanged(StatusEffectInstance effect, int previousStacks) { }
        public void OnTick(StatusEffectInstance effect) { }
        public void OnRemoved(StatusEffectInstance effect, EffectRemoveReason reason) { }
    }
}