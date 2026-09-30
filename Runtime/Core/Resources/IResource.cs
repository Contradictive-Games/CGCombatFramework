namespace CG.CombatFramework
{
    public interface IResource : IReadonlyResource
    {
        public float Add(float amount, ChangeReason reason, in EffectContext ctx = default);
        public float Remove(float amount, ChangeReason reason, in EffectContext ctx = default);
        public bool TrySpend(float amount, in EffectContext ctx = default);
        public void Set(float value, ChangeReason reason = ChangeReason.Set, in EffectContext ctx = default);
        public bool RegenPaused { get; set; }
    }
}