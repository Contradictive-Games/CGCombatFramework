using JetBrains.Annotations;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Health/Revive")]
    public sealed class RevivePayload : IPayload
    {
        [SerializeField] private Subject who = Subject.Target;
        [SerializeField, Range(0.01f, 1f), Tooltip("% max health unit will come back with")] 
        private float health = 0.5f;

        public void Apply(in EffectContext ctx)
        {
            var unit = ctx.GetUnit(who);
            if(unit != null && unit.TryGet<IHealth>(out var life) && life.IsDead) life.Revive(health);
        }
    }
}