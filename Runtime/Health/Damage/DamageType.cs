using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Damage Type", menuName = Globals.k_PACKAGE_NAME + "/Types/Damage Type")]
    public class DamageType : GameDefinition
    {
        [Header("Damage Type Settings")]
        public Color Color = Color.red;
        [Tooltip("What stat will be used to calculate resistances")]
        public StatType ResistanceStat;
        [Tooltip("What tags will make units immune to this damage type")]
        public CombatTag ImmunityTag;
    }
}
