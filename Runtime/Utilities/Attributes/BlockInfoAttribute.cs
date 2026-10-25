using System;

namespace CG.CombatFramework
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BlockInfoAttribute : Attribute
    {
        public readonly string Description;

        public string Example { get; set; }

        public BlockInfoAttribute(string description) => Description = description;
    }
}
