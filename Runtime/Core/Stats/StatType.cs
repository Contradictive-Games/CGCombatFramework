using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatType", menuName = "CG Combat Framework/Types/Stat Type")]
    public sealed class StatType : GameDefinition
    {
        [Header("Stat Identity")]
        public string Name;
        public Sprite Icon;
        public string Description;
        
        [Header("Settings")]
        public float DefaultValue;
        public float MinValue;
        public float MaxValue;

        private bool clamped = false;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            clamped = DefaultValue < MinValue || DefaultValue > MaxValue;
            if (!clamped)
            {
                Mathf.Clamp(DefaultValue, MinValue, MaxValue);
            }
        }
#endif
    }
}