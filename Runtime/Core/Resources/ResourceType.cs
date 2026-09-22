using UnityEngine;

namespace CG.RGS.CombatFramework
{
    [CreateAssetMenu(fileName = "ResourceType", menuName = "CG/Combat Framework/Resource Type")]
    public sealed class ResourceType : ScriptableObject
    {
        public string Name;
        public Color UIColor;
    }
}