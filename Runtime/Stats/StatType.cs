using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatType", menuName = Globals.k_PACKAGE_NAME + "/Types/Stat Type")]
    public sealed class StatType : GameDefinition
    {
        [Header("Stat Identity")]
        public string Name;
        public Sprite Icon;
        public string Description;
        
        [Header("Settings")]
        public float DefaultValue;
        [Tooltip("Whether or not the final value should clamp between Min/Max (after modifiers)")]
        public bool ClampValue;
        public float MinValue;
        public float MaxValue = 10f;


        public float Clamp(float value) => ClampValue ? Mathf.Clamp(value, MinValue, MaxValue) : value;
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if(MaxValue < MinValue) MaxValue = MinValue;
            if(ClampValue) DefaultValue = Mathf.Clamp(DefaultValue, MinValue, MaxValue);
        }
#endif
    }
}