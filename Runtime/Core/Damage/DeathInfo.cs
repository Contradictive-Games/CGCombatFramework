namespace CG.CombatFramework
{
    public readonly struct DeathInfo
    {
        public readonly IUnit Victim;
        public readonly IUnit Killer;
        public readonly EffectContext Context;
        public readonly DamageResult? FinalBlow;

        public DeathInfo(IUnit victim, IUnit killer, in EffectContext ctx, DamageResult? finalBlow)
        {
            Victim = victim;
            Killer = killer;
            Context = ctx;
            FinalBlow = finalBlow;
        }
    }
}