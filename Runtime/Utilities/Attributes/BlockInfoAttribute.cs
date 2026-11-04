using System;

namespace CG.CombatFramework
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BlockInfoAttribute : Attribute
    {
        public readonly string Description;

        public string Example { get; set; }
        public string Color { get; set; }
        public string Icon { get; set; }

        public BlockInfoAttribute()
        {
        }

        public BlockInfoAttribute(string description) => Description = description;
    }
}
