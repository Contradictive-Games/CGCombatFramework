using UnityEngine;

namespace CG.RGS.CombatFramework
{
    [CreateAssetMenu(fileName = "StatType", menuName = "CG/Combat Framework/Stat Type")]
    public sealed class StatType : ScriptableObject
    {
        public string Name;
        public string Description;
    }
}