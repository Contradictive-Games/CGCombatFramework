using UnityEngine;

namespace CG.RGS.CombatFramework
{
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Contradictive Games/Combat Framework/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public ITargetingStrategy TargetingStrategy;
        public string Name;
        public Sprite Icon;
        public float BaseCooldown;
    }
}