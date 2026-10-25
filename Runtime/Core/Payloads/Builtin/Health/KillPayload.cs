using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Kill")]
    [BlockInfo("Kills the subject outright.", Example = "Execute targets below 10% health")]
    public sealed class KillPayload : IPayload
    {
        [FormerlySerializedAs("who")]
        [UnityEngine.SerializeField, UnityEngine.Tooltip("Who will receive this")]
        private Subject subject = Subject.Target;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit != null && unit.TryGet<IHealth>(out var health)) health.Kill(in ctx);
        }
    }
}
