using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Tag", menuName = Globals.k_PACKAGE_NAME + "/Types/Tag")]
    public sealed class CombatTag : GameDefinition
    {
        [Header("Tag Settings")]
        public string Name;
        public CombatTag Parent;


        public bool IsOrDescendedFrom(CombatTag other)
        {
            for(var tag = this; tag != null; tag = tag.Parent)
            {
                if(tag == other) return true;
            }
            return false;
        }
    }
}