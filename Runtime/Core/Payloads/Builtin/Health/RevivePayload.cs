using UnityEngine;
using UnityEngine.Serialization;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Revive")]
    [BlockInfo("Brings a dead subject back with Health as a share of their max health.", Example = "Phoenix passive: On Died, Revive at 50%")]
    public sealed class RevivePayload : IPayload
    {
        [FormerlySerializedAs("who")]
        [SerializeField, Tooltip("Who will receieve this")] private Subject subject = Subject.Target;
        [SerializeField, Range(0.01f, 1f), Tooltip("% max health unit will come back with")]
        private float health = 0.5f;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(subject);
            if(unit != null && unit.TryGet<IHealth>(out var life) && life.IsDead) life.Revive(health);
        }
    }
}
