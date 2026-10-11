namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Status Effects/Remove Effect")]
    public sealed class RemoveEffectPayload : IPayload
    {
        [UnityEngine.SerializeField] private StatusEffectDefinition effect;
        [UnityEngine.SerializeField] private Subject from = Subject.Target;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(from);
            if(unit != null && unit.TryGet<IStatusEffects>(out var effects)) effects.RemoveAll(effect);
        }
    }
}