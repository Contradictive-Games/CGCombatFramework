namespace CG.CombatFramework
{
    public enum Subject { Target, Source }

    public readonly struct EffectContext
    {
        public readonly IUnit SourceUnit;
        public readonly IGameDefinition Source;
        public readonly IUnit TargetUnit;
        public readonly HitData Hit;
        public readonly float Power;
        public readonly int Stacks;

        private readonly ICombatRandom random;

        public EffectContext(IUnit sourceUnit, IGameDefinition source, IUnit targetUnit, HitData hit = default,
            float power = 1f, int stacks = 1, ICombatRandom random = null)
        {
            SourceUnit = sourceUnit;
            Source = source;
            TargetUnit = targetUnit;
            Hit = hit;
            Power = power;
            Stacks = stacks;
            this.random = random;
        }

        public IUnit GetUnit(Subject subject) => subject == Subject.Target ? TargetUnit : SourceUnit;

        public EffectContext WithTarget(IUnit targetUnit, in HitData hit) => new(SourceUnit, Source, targetUnit, hit, Power, Stacks, random);
        public EffectContext WithSource(IGameDefinition source) => new(SourceUnit, source, TargetUnit, Hit, Power, Stacks, random);
        public EffectContext WithPower(float power) => new(SourceUnit, Source, TargetUnit, Hit, power, Stacks, random);
        public EffectContext WithStacks(int stacks) => new(SourceUnit, Source, TargetUnit, Hit, Power, stacks, random);
    }
}
