using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Chance")]
    [BlockInfo("Passes randomly with the given chance, rolled every time it is checked. 0.25 = 25%.", Example = "Inside If ... Else for a 20% chance to stun on hit")]
    public sealed class ChanceCondition : ICondition
    {
        [SerializeField, Range(0f, 1f), Tooltip("% chance for this to pass")]
        private float chance = 0.5f;

        public bool Evaluate(in EffectContext ctx) => ctx.Random.Roll(chance);
    }
}
