namespace CG.CombatFramework
{
    public enum Subject { Target, Source }

    public readonly struct EffectContext
    {
        public readonly UnitHandle Source;
        public readonly UnitHandle Target;
        public readonly IGameDefinition Cause;
        public readonly HitData Hit;
        public readonly float Power;
        public readonly int Stacks;
        public readonly int ProcDepth;
        private readonly IGameRandom random;

        public EffectContext(IUnit sourceUnit, IGameDefinition cause, IUnit targetUnit = null, in HitData data = default,
            float power = 1f, int stacks = 1, int procDepth = 0, IGameRandom random = null)
        {
            Source = new UnitHandle(sourceUnit);
            Target = new UnitHandle(targetUnit);
            Cause = cause;
            Hit = data;
            Power = power;
            Stacks = stacks;
            ProcDepth = procDepth;
            this.random = random;
        }


        public IUnit SourceUnit => Source.Get();
        public IUnit TargetUnit => Target.Get();
        public IGameRandom Random => random ?? CombatServices.Random;

        public IUnit GetUnit(Subject subject) => subject == Subject.Target ? TargetUnit : SourceUnit;
    }
}
