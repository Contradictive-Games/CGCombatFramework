using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal static class InlineFields
    {
        private const float k_NumberWidth = 56f;
        private const float k_Gap = 4f;

        public static VisualElement Row(SerializedProperty property, params string[] names)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-inline");
            foreach(var name in names)
            {
                var child = property.FindPropertyRelative(name);
                if(child != null) row.Add(Field(child, name));
            }
            return row;
        }

        public static void Row(Rect position, SerializedProperty property, params string[] names)
        {
            var children = new SerializedProperty[names.Length];
            int numbers = 0;
            int others = 0;
            for(int i = 0; i < names.Length; i++)
            {
                children[i] = property.FindPropertyRelative(names[i]);
                if(children[i] == null) continue;
                if(IsNumber(children[i])) numbers++;
                else others++;
            }
            if(numbers + others == 0) return;

            float free = position.width - k_Gap * (numbers + others - 1) - k_NumberWidth * numbers;
            float flexible = others > 0 ? Mathf.Max(0f, free / others) : 0f;
            float x = position.x;
            foreach(var child in children)
            {
                if(child == null) continue;
                var rect = new Rect(x, position.y, IsNumber(child) ? k_NumberWidth : flexible, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(rect, child, GUIContent.none);
                GUI.Label(rect, new GUIContent(string.Empty, Tooltip(child)));
                x = rect.xMax + k_Gap;
            }
        }

        public static VisualElement Field(SerializedProperty property, string name, string placeholder = null)
        {
            VisualElement field;
            switch(property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    var type = BlockTypes.FieldOf(property)?.FieldType ?? typeof(Object);
                    var objectField = new ObjectField { objectType = type, allowSceneObjects = false };
                    objectField.BindProperty(property);
                    field = objectField;
                    break;
                case SerializedPropertyType.Float:
                    var floatField = new FloatField();
                    floatField.BindProperty(property);
                    field = floatField;
                    break;
                case SerializedPropertyType.Integer:
                    var integerField = new IntegerField();
                    integerField.BindProperty(property);
                    field = integerField;
                    break;
                case SerializedPropertyType.Boolean:
                    var toggle = new Toggle();
                    toggle.BindProperty(property);
                    field = toggle;
                    break;
                case SerializedPropertyType.String:
                    var textField = new TextField();
                    if(!string.IsNullOrEmpty(placeholder)) textField.textEdition.placeholder = placeholder;
                    textField.BindProperty(property);
                    field = textField;
                    break;
                case SerializedPropertyType.Enum:
                    var enumType = BlockTypes.FieldOf(property)?.FieldType;
                    if(enumType != null && enumType.IsEnum)
                    {
                        var enumField = new EnumField((Enum)Enum.GetValues(enumType).GetValue(0));
                        enumField.BindProperty(property);
                        field = enumField;
                    }
                    else field = new PropertyField(property, string.Empty);
                    break;
                default:
                    field = new PropertyField(property, string.Empty);
                    break;
            }
            field.tooltip = Tooltip(property);
            field.AddToClassList("cg-inline__field");
            field.AddToClassList("cg-inline__" + name.ToLowerInvariant());
            return field;
        }

        private static bool IsNumber(SerializedProperty property) => property.propertyType == SerializedPropertyType.Float || property.propertyType == SerializedPropertyType.Integer;

        private static string Tooltip(SerializedProperty property) => string.IsNullOrEmpty(property.tooltip) ? property.displayName : $"{property.displayName}: {property.tooltip}";
    }

    [CustomPropertyDrawer(typeof(StatValue))]
    internal sealed class StatValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => InlineFields.Row(property, "Stat", "Value");
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => InlineFields.Row(position, property, "Stat", "Value");
    }

    [CustomPropertyDrawer(typeof(ResourceValue))]
    internal sealed class ResourceValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => InlineFields.Row(property, "Resource", "Current");
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => InlineFields.Row(position, property, "Resource", "Current");
    }
}
