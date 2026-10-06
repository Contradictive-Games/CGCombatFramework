using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatusEffectDefinition", menuName = Globals.k_PACKAGE_NAME + "/Status Effect Definition")]
    public sealed class StatusEffectDefinition : GameDefinition
    {
        [Header("Status Effect Settings")]
        public string Name;
        public float Duration;
        public Sprite Icon;
    }
}