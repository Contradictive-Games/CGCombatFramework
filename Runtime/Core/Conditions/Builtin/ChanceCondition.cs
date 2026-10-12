using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Chance")]
    public sealed class ChanceCondition : ICondition
    {
        [SerializeField, Range(0f, 1f), Tooltip("% chance for this to pass")]
        private float chance = 0.5f;

        public bool Evaluate(in EffectContext ctx) => ctx.Random.Roll(chance);
    }
}
