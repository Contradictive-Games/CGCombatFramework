using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Regen/Delay Afer Loss")]
    public sealed class DelayAfterLoss : IRegenRule
    {
        [SerializeField, Min(0f)] private float delay = 3f;
        [SerializeReference, SelectType] private IRegenRule then;

        public float Evaluate(in RegenState state, float deltaTime)
        {
            return state.TimeSinceLoss >= delay && then != null ? then.Evaluate(in state, deltaTime) : 0f;
        }
    }
}