namespace CG.CombatFramework
{
    public readonly struct HealResult
    {
        public readonly float Healed;
        public readonly float Overheal;
        public readonly bool IsCrit;
        public readonly EffectContext Context;

        public HealResult(float healed, float overheal, bool isCrit, in EffectContext ctx)
        {
            Healed = healed;
            Overheal = overheal;
            IsCrit = isCrit;
            Context = ctx;
        }

        public static HealResult Ignored() => new HealResult(0f, 0f, false, default);
    }
}