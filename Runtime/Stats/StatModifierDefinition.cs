using UnityEngine;

namespace CG.CombatFramework
{
    [CreateAssetMenu(fileName = "Stat Modifier", menuName = "CG Combat Framework/Stat Modifier")]
    public sealed class StatModifierDefinition : GameDefinition
    {
        [Header("Stat Modifier Settings")]
        public StatType TargetStat;
        public float Value;
        public StatModifierType Type;

        public StatModifier Create(object source, float scale = 1f) => new StatModifier(Value * scale, Type, source);
    }
}
