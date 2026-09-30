using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PolymorphicTypeAttribute : PropertyAttribute { }
    
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TypeMenuAttribute : Attribute
    {
        public readonly string Path;
        public TypeMenuAttribute(string path) => Path = path;
    }
}