using System.Collections.Generic;

namespace CG.RGS.CombatFramework
{
    public class Defense
    {
        public int ArmorValue { get; private set; }
        public readonly Dictionary<DamageType, Stat> Resistances = new();
    }
}