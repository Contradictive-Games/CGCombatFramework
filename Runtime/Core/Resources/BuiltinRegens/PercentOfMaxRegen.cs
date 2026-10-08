using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Percent of Max Per Second")]
    public sealed class PercentOfMaxRegen : IRegenRule
    {
        [SerializeField, Range(0f, 100f)] private float percentPerSecond = 2f;
        public float Evaluate(in RegenState state, float deltaTime)
        {
            return state.Current < state.Max ? state.Max * percentPerSecond * 0.01f * deltaTime : 0f;
        }
    }
}