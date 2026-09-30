using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Resource", menuName = "CG Combat Framework/Types/Resource Type")]
    public sealed class ResourceType : GameDefinition
    {
        [Header("Resource Settings")]
        public string DisplayName;
        public Color Color = Color.red;
        public Sprite Icon;
        [Tooltip("If set, the max value will come from this and will take modifiers. (this should generally be set)")]
        public StatType MaxStat;
    }
}
