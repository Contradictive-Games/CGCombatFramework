using UnityEngine;

namespace CG.RGS.CombatFramework
{
    [CreateAssetMenu(fileName = "StatusEffectDefinition", menuName = "CG/Combat Framework/Status Effect Definition")]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        public string Name;
        public float Duration;
        public Sprite Icon;
    }
}