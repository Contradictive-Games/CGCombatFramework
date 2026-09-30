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

    [CreateAssetMenu(fileName = "Unit Stat Profile", menuName = "CG Combat Framework/Units/Unit Stat Profile")]
    public sealed class UnitStatProfile : GameDefinition
    {
        public List<StatEntry> Stats = new();
    }
}