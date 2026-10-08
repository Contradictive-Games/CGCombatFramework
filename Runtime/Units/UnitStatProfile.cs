using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable]
    public struct StatEntry
    {
        public StatType Type;
        public float BaseValue;
    }

    public enum ResourceStart { Full, Empty, Percent }

    [System.Serializable]
    public sealed class ResourceEntry
    {
        public ResourceType Type;
        public float BaseMax = 100f;
        public ResourceStart Start = ResourceStart.Full;
        [Range(0f, 1f)] public float StartPercent = 1f;
        [Tooltip("Keep Current = Keep current as same raw value \n Keep Percent = Keep current as same percent \n Gain The Increase = Current will go up same as max")] 
        public MaxChangePolicy OnMaxChanged = MaxChangePolicy.KeepPercent;
        [SerializeReference, SelectType] public List<IRegenRule> RegenRules = new();


        public Resource CreateRuntime() => new(Type, BaseMax, RegenRules, OnMaxChanged);

        public float StartValue(float max) => Start switch {
            ResourceStart.Empty => 0f,
            ResourceStart.Percent => max * StartPercent,
            _ => max
        };
    }

    [CreateAssetMenu(fileName = "Unit Stat Profile", menuName = "CG Combat Framework/Units/Unit Stat Profile")]
    public sealed class UnitStatProfile : GameDefinition
    {
        public List<StatEntry> Stats = new();
        public List<ResourceEntry> Resources = new();
    }
}