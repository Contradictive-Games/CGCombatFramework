namespace CG.CombatFramework
{
    public enum ChangeReason { Damage, Heal, Regen, Spend, Restore, Set, MaxChanged }

    public readonly struct ResourceChange
    {
        public readonly IReadonlyResource Resource;
        public readonly float Old;
        public readonly float New;
        public readonly float Max;
        public readonly ChangeReason Reason;
        public readonly EffectContext Context;

        public ResourceChange(IReadonlyResource resource, float old, float newValue, float max, ChangeReason reason, in EffectContext ctx)
        {
            Resource = resource;
            Old = old;
            New = newValue;
            Max = max;
            Reason = reason;
            Context = ctx;
        }
    }
}