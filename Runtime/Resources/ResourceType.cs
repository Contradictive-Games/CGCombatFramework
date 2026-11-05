using UnityEngine;

namespace CG.CombatFramework
{
    [DefinitionInfo("Resource Types", Group = "Combat", Description = "Pools like health, shields or mana, with a max that can follow a stat.", Color = "#E5534B", Icon = "resource", Order = -60)]
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
