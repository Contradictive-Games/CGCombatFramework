using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Resource", menuName = Globals.k_PACKAGE_NAME + "/Types/Resource Type")]
    public sealed class ResourceType : GameDefinition
    {
        [Header("Resource Settings")]
        public Color Color = Color.red;
        public Sprite Icon;
        [Tooltip("If set, the max value will come from this and will take modifiers. (this should generally be set)")]
        public StatType MaxStat;
    }
}
