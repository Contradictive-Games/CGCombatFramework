using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable, TypeMenu("Clamp")]
    [BlockInfo("Keeps the value between Minimum and Maximum.", Example = "Cap a stacking bonus at 100")]
    public sealed class ClampScaler : IValueScaler
    {
        [SerializeField, Tooltip("The absolute minimum damage this will do")] 
        private float minimum = 1f;
        [SerializeField, Tooltip("The absolute maximum this damage can do")] 
        private float maximum = 9999f;

        public float Apply(float value, in EffectContext ctx) => Mathf.Clamp(value, minimum, maximum);
    }
}