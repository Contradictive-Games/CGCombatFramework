using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatType", menuName = "CG Combat Framework/Types/Stat Type")]
    public sealed class StatType : ScriptableObject
    {
        public string Name;
        public string Description;
    }
}