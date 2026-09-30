using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "StatusEffectDefinition", menuName = "CG Combat Framework/Status Effect Definition")]
    public sealed class StatusEffectDefinition : GameDefinition
    {
        public override string DefinitionPrefix => "core:statuseffect_";
        public string Name;
        public float Duration;
        public Sprite Icon;
    }
}