using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Ramp Up")]
    [BlockInfo("Regen that speeds up from Start Per Second to Full Per Second over Ramp Seconds since the last loss.", Example = "Out-of-combat healing that builds up")]
    public sealed class RampUpRegen : IRegenRule
    {
        [SerializeField, Min(0.01f), Tooltip("Seconds from start per second to full per second")] 
        private float rampSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("How much it will regen at beginning")] 
        private float startPerSecond = 1f;
        [SerializeField, Min(0f), Tooltip("How much it will regen at max ramp")] 
        private float fullPerSecond = 10f;


        public float Evaluate(in RegenState state, float deltaTime)
        {
            if(state.Current >= state.Max) return 0f;
            float t = Mathf.Clamp01(state.TimeSinceLoss / rampSeconds);
            return Mathf.Lerp(startPerSecond, fullPerSecond, t) * deltaTime;
        }
    }
}