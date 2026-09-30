using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Tag", menuName = "CGCombatFramework/Combat/Tag")]
    public sealed class CombatTag : ScriptableObject, IGameDefinition
    {
        public string Id => "core:tag_" + Name.ToLowerInvariant().Trim().Replace(' ', '_');

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