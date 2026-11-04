using System;

namespace CG.CombatFramework
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class DefinitionInfoAttribute : Attribute
    {
        public readonly string Title;

        public string Group { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public string Icon { get; set; }

        public DefinitionInfoAttribute()
        {
        }

        public DefinitionInfoAttribute(string title) => Title = title;
    }
}
