using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Delay After Loss")]
    [BlockInfo("Does nothing until Delay seconds after the last loss, then runs the rule inside it.", Example = "Shields that start recharging 3s after taking damage")]
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