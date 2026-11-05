using System;

namespace CG.CombatFramework
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UnitPartAttribute : Attribute
    {
        public readonly string Label;

        public string Description { get; set; }
        public string Group { get; set; }
        public string Color { get; set; }
        public string Icon { get; set; }
        public int Order { get; set; }
        public bool Hidden { get; set; }

        public UnitPartAttribute()
        {
        }

        public UnitPartAttribute(string label) => Label = label;
    }
}
