using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatType", menuName = "Contradictive Games/Combat Framework/Stat Type")]
    public sealed class StatType : ScriptableObject
    {
        public string Name;
        public string Description;
        public float DefaultValue;
        public float Minimum;
        public float Maximum;


        public float Clamp(float value)
        {
            return Mathf.Clamp(value, Minimum, Maximum);
        }
    }
}