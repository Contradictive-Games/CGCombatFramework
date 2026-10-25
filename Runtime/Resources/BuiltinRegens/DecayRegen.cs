using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Decay Toward Floor")]
    [BlockInfo("Drains Per Second down toward Floor Percent of max, starting Grace After Gain seconds after the last gain.", Example = "Rage that fades 3s after your last hit")]
    public sealed class DecayRegen : IRegenRule
    {
        [SerializeField] private float perSecond = 5f;
        [SerializeField, Range(0f, 1f)] private float floorPercent = 0f;
        [SerializeField, Min(0f), Tooltip("How long since the last gain until decay starts")] 
        private float graceAfterGain = 3f;

        public float Evaluate(in RegenState state, float deltaTime)
        {
            float floor = state.Max * floorPercent;
            if(state.TimeSinceGain < graceAfterGain || state.Current <= floor) return 0f;
            return -Mathf.Min(perSecond * deltaTime, state.Current - floor);
        }
    }
}