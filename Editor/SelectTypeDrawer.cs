using System;
using System.Collections.Generic;
using System.Linq;
using CG.CombatFramework;
using UnityEditor;
using UnityEngine;

namespace ContradictiveGames.Combat.Editor
{
    [CustomPropertyDrawer(typeof(SelectTypeAttribute))]
    public sealed class SelectTypeDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> candidates = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // Dropdown on the header row, right of the label. Drawn first so it receives the click.
            float x = position.x + EditorGUIUtility.labelWidth + 2f;
            var button = new Rect(x, position.y, position.xMax - x, EditorGUIUtility.singleLineHeight);
            var value = property.managedReferenceValue;
            string current = value == null ? "(None)" : ShortName(value.GetType());
            if (EditorGUI.DropdownButton(button, new GUIContent(current), FocusType.Keyboard))
                ShowMenu(property);

            // Unity does not re-enter this drawer for the same property, so this draws the children.
            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }

        private static void ShowMenu(SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            string path = property.propertyPath; // capture: the property is stale by the time the menu calls back
            var currentType = property.managedReferenceValue?.GetType();

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(None)"), currentType == null, () => Assign(serializedObject, path, null));
            foreach (var type in Candidates(FieldType(property)))
            {
                var t = type;
                menu.AddItem(new GUIContent(MenuPath(t)), t == currentType, () => Assign(serializedObject, path, t));
            }
            menu.ShowAsContext();
        }

        private static void Assign(SerializedObject serializedObject, string path, Type type)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(path);
            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.isExpanded = type != null;
            serializedObject.ApplyModifiedProperties();
        }

        // managedReferenceFieldTypename is "AssemblyName Namespace.Type" (nested types use '/').
        private static Type FieldType(SerializedProperty property)
        {
            var parts = property.managedReferenceFieldTypename.Split(' ');
            return parts.Length == 2 ? Type.GetType($"{parts[1].Replace('/', '+')}, {parts[0]}") : null;
        }

        private static Type[] Candidates(Type baseType)
        {
            if (baseType == null) return Array.Empty<Type>();
            if (candidates.TryGetValue(baseType, out var cached)) return cached;

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

        private static string MenuPath(Type type)
        {
            var menu = (TypeMenuAttribute)Attribute.GetCustomAttribute(type, typeof(TypeMenuAttribute));
            return menu != null ? menu.Path : ObjectNames.NicifyVariableName(type.Name);
        }

        private static string ShortName(Type type)
        {
            string path = MenuPath(type);
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }
    }
}