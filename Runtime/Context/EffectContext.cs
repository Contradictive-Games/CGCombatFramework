namespace CG.CombatFramework
{
    public enum Subject
    {
        Target, //who received (was hit)
        Source, //who sent (created)
        Owner //source unit owner (e.g: player on summoned turret)
    }

    public readonly struct EffectContext
    {
        public readonly UnitHandle Source;
        public readonly UnitHandle Target;
        public readonly IGameDefinition Cause;
        public readonly HitData Hit;
        public readonly float Power;
        public readonly int Stacks;
        public readonly int ProcDepth;
        public readonly float Amount;
        private readonly IGameRandom random;

        public EffectContext(IUnit sourceUnit, IGameDefinition cause, IUnit targetUnit = null, in HitData data = default,
            float power = 1f, int stacks = 1, int procDepth = 0, IGameRandom random = null, float amount = 0f)
        {
            Source = new UnitHandle(sourceUnit);
            Target = new UnitHandle(targetUnit);
            Cause = cause;
            Hit = data;
            Power = power;
            Stacks = stacks;
            ProcDepth = procDepth;
            Amount = amount;
            this.random = random;
        }

        private EffectContext(in EffectContext from, UnitHandle target, in HitData hit, int procDepth, int stacks)
        {
            Source = from.Source;
            Target = target;
            Cause = from.Cause;
            Hit = hit;
            Power = from.Power;
            Stacks = stacks;
            ProcDepth = procDepth;
            Amount = from.Amount;
            random = from.random;
        }


        public IUnit SourceUnit => Source.Get();
        public IUnit TargetUnit => Target.Get();
        public IGameRandom Random => random ?? CombatServices.Random;

        public IUnit GetUnit(Subject subject) => subject switch
        {
            Subject.Target => TargetUnit,
            Subject.Owner => SourceUnit?.Root(),
            _ => SourceUnit
        };

        /// <summary>
        /// Create a new EffectContext with same parameters but aimed at another target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="hit"></param>
        /// <returns></returns>
        public EffectContext Retarget(IUnit target, in HitData hit) => new(in this, new UnitHandle(target), in hit, ProcDepth, Stacks);
        public EffectContext Nested(IUnit target) => new(in this, new UnitHandle(target), in Hit, ProcDepth + 1, Stacks);
        public EffectContext ForStacks(int stacks) => new(in this, Target, in Hit, ProcDepth, stacks);
    }
}
