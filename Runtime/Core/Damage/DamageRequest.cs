namespace CG.CombatFramework
{
    public readonly struct DamageRequest
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly DamageFlags Flags;
        public readonly EffectContext Context;

        public DamageRequest(float amount, DamageType type, in EffectContext ctx, DamageFlags flags)
        {
            Amount = amount;
            Type = type;
            Flags = flags;
            Context = ctx;
        }

        public bool Has(DamageFlags flag) => (Flags & flag) != 0;
    }
}