using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Delay After Loss")]
    public sealed class DelayAfterLoss : IRegenRule
    {
        [SerializeField, Min(0f)] private float delay = 3f;
        [SerializeReference, SelectType] private IRegenRule then;

        public float Evaluate(in RegenState state, float deltaTime)
        {
            if(then == null || state.TimeSinceLoss < delay) return 0f;
            var shifted = new RegenState(state.Current, state.Max, state.TimeSinceLoss - delay, state.TimeSinceGain);
            return then.Evaluate(in shifted, deltaTime);
        }
    }
}