namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Kill")]
    public sealed class KillPayload : IPayload
    {
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Who will receive this")]
        private Subject subject = Subject.Target;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit != null && unit.TryGet<IHealth>(out var health)) health.Kill(in ctx);
        }
    }
}
