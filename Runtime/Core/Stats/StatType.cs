using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatType", menuName = "CG Combat Framework/Types/Stat Type")]
    public sealed class StatType : GameDefinition
    {
        public override string DefinitionPrefix => "core:stat_";
        
        [Header("Identity")]
        public string Name;
        public string Description;
        
        [Header("Settings")]
        public float DefaultValue;
        public float MinValue;
        public float MaxValue;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if(DefaultValue < MinValue || DefaultValue > MaxValue)
            {
                Debug.LogWarning("StatType DefaultValue is invalid");
            }
        }
#endif
    }
}