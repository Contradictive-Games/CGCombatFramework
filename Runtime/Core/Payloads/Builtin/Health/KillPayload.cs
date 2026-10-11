namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Kill")]
    public sealed class KillPayload : IPayload
    {
        [UnityEngine.SerializeField] private Subject who = Subject.Target;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(who);
            if(unit != null && unit.TryGet<IHealth>(out var health)) health.Kill(in ctx);
        }
    }
}