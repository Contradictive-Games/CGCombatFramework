using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    public static class BlockTypes
    {
        private const BindingFlags k_Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const string k_ArrayData = ".Array.data[";

        private static readonly Dictionary<Type, Type[]> candidates = new();
        private static readonly Dictionary<Type, bool> holdsBlocks = new();

        public static Type[] Candidates(Type baseType)
        {
            if(baseType == null) return Array.Empty<Type>();
            if(candidates.TryGetValue(baseType, out var cached)) return cached;

            var found = TypeCache.GetTypesDerivedFrom(baseType)
                .Append(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericType
                         && !typeof(UnityEngine.Object).IsAssignableFrom(t)
                         && t.IsDefined(typeof(SerializableAttribute), false)
                         && t.GetConstructor(Type.EmptyTypes) != null)
                .Distinct()
                .OrderBy(MenuPath)
                .ToArray();

            candidates[baseType] = found;
            return found;
        }

        public static string MenuPath(Type type)
        {
            var menu = (TypeMenuAttribute)Attribute.GetCustomAttribute(type, typeof(TypeMenuAttribute));
            return menu != null ? menu.Path : ObjectNames.NicifyVariableName(type.Name);
        }

        public static string DisplayName(Type type)
        {
            if(type == null) return "None";
            string path = MenuPath(type);
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        public static BlockInfoAttribute InfoOf(Type type)
        {
            return type == null ? null : (BlockInfoAttribute)Attribute.GetCustomAttribute(type, typeof(BlockInfoAttribute));
        }

        public static string TooltipOf(Type type)
        {
            var info = InfoOf(type);
            if(info == null) return null;
            return string.IsNullOrEmpty(info.Example) ? info.Description : $"{info.Description}\n\nExample: {info.Example}";
        }

        public static Type ValueType(SerializedProperty property)
        {
            if(property == null || property.propertyType != SerializedPropertyType.ManagedReference) return null;
            return property.managedReferenceValue?.GetType();
        }

        public static Type SlotType(SerializedProperty property)
        {
            if(property == null) return null;
            if(property.propertyType == SerializedPropertyType.ManagedReference)
            {
                var parts = property.managedReferenceFieldTypename.Split(' ');
                return parts.Length == 2 ? Type.GetType($"{parts[1].Replace('/', '+')}, {parts[0]}") : null;
            }
            var field = FieldOf(property);
            return field == null ? null : Unwrap(field.FieldType);
        }

        public static bool IsBlockArray(SerializedProperty property)
        {
            if(property == null || !property.isArray || property.propertyType != SerializedPropertyType.Generic) return false;
            var field = FieldOf(property);
            return field != null && field.IsDefined(typeof(SerializeReference), true);
        }

        public static bool HoldsBlocks(SerializedProperty property)
        {
            if(property == null || property.propertyType != SerializedPropertyType.Generic) return false;
            var field = FieldOf(property);
            return field != null && HoldsBlocks(Unwrap(field.FieldType), new HashSet<Type>());
        }

        public static List<SerializedProperty> Children(SerializedProperty parent)
        {
            var children = new List<SerializedProperty>();
            if(parent == null) return children;
            var end = parent.GetEndProperty();
            var child = parent.Copy();
            if(!child.NextVisible(true)) return children;
            while(!SerializedProperty.EqualContents(child, end))
            {
                children.Add(child.Copy());
                if(!child.NextVisible(false)) break;
            }
            return children;
        }

        public static List<SerializedProperty> Children(SerializedObject serializedObject)
        {
            var children = new List<SerializedProperty>();
            var child = serializedObject.GetIterator();
            if(!child.NextVisible(true)) return children;
            do
            {
                if(child.propertyPath != "m_Script") children.Add(child.Copy());
            }
            while(child.NextVisible(false));
            return children;
        }

        public static FieldInfo FieldOf(SerializedProperty property)
        {
            object current = property.serializedObject.targetObject;
            FieldInfo field = null;
            var parts = property.propertyPath.Replace(k_ArrayData, ".[").Split('.');
            foreach(var part in parts)
            {
                if(part.StartsWith("[", StringComparison.Ordinal))
                {
                    int index = int.Parse(part.Substring(1, part.Length - 2));
                    current = current is IList list && index >= 0 && index < list.Count ? list[index] : null;
                    continue;
                }
                field = current == null ? null : FindField(current.GetType(), part);
                if(field == null) return null;
                current = field.GetValue(current);
            }
            return field;
        }

        public static void OpenScript(Type type)
        {
            if(type == null) return;
            string declaration = "class " + type.Name;
            foreach(var guid in AssetDatabase.FindAssets($"{type.Name} t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if(script == null) continue;
                string text = script.text;
                int at = text.IndexOf(declaration, StringComparison.Ordinal);
                if(at < 0) continue;
                int line = 1;
                for(int i = 0; i < at; i++)
                {
                    if(text[i] == '\n') line++;
                }
                AssetDatabase.OpenAsset(script, line);
                return;
            }
            Debug.LogWarning($"[CG Combat Framework] Couldn't find the script for '{type.Name}'");
        }

        private static Type Unwrap(Type type)
        {
            if(type.IsArray) return type.GetElementType();
            if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return type;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for(var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, k_Fields);
                if(field != null) return field;
            }
            return null;
        }

        private static bool HoldsBlocks(Type type, HashSet<Type> visiting)
        {
            if(type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            if(holdsBlocks.TryGetValue(type, out bool cached)) return cached;
            if(!visiting.Add(type)) return false;

            bool result = false;
            for(var current = type; current != null && current != typeof(object) && !result; current = current.BaseType)
            {
                foreach(var field in current.GetFields(k_Fields))
                {
                    if(field.IsDefined(typeof(SerializeReference), true))
                    {
                        result = true;
                        break;
                    }
                    if(field.IsNotSerialized || (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true))) continue;
                    var inner = Unwrap(field.FieldType);
                    if(inner.IsDefined(typeof(SerializableAttribute), false) && HoldsBlocks(inner, visiting))
                    {
                        result = true;
                        break;
                    }
                }
            }

            holdsBlocks[type] = result;
            return result;
        }
    }
}
