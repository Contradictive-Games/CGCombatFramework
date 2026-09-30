using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [System.Serializable]
    public struct StatEntry
    {
        public StatType Type;
        public float BaseValue;
        public float MinValue;
        public float MaxValue;
    }

    [CreateAssetMenu(fileName = "Unit Stat Profile", menuName = "CG Combat Framework/Units/Unit Stat Profile")]
    public sealed class UnitStatProfile : GameDefinition
    {
        public override string DefinitionPrefix => "core:statprofile_";
        public List<StatEntry> Stats = new();
    }
}