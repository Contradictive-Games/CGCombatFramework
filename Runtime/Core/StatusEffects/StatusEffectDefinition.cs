using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatusEffectDefinition", menuName = "CG Combat Framework/Status Effect Definition")]
    public sealed class StatusEffectDefinition : GameDefinition
    {
        [Header("Status Effect Settings")]
        public string Name;
        public float Duration;
        public Sprite Icon;
    }
}