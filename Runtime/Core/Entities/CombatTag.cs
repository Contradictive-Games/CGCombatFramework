using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Tag", menuName = "CG Combat Framework/Types/Tag")]
    public sealed class CombatTag : GameDefinition
    {
        public override string DefinitionPrefix => "core:tag_";
        
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