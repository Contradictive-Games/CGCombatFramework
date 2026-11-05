using UnityEngine;

namespace CG.CombatFramework
{
    [DefinitionInfo("Damage Types", Group = "Combat", Description = "Kinds of damage, each with an optional resistance stat.", Color = "#F0883E", Icon = "damage", Order = -50)]
    [CreateAssetMenu(fileName = "Damage Type", menuName = Globals.k_PACKAGE_NAME + "/Types/Damage Type")]
    public sealed class DamageType : GameDefinition
    {
        [Header("Damage Type Settings")]
        public Color Color = Color.red;
        [Tooltip("What stat will be used to calculate resistances")]
        public StatType ResistanceStat;
        [Tooltip("What tags will make units immune to this damage type")]
        public CombatTag ImmunityTag;
    }
}
