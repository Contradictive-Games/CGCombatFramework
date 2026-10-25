using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal static class InlineFields
    {
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
            field.tooltip = string.IsNullOrEmpty(property.tooltip) ? property.displayName : $"{property.displayName}: {property.tooltip}";
            field.AddToClassList("cg-inline__field");
            field.AddToClassList("cg-inline__" + name.ToLowerInvariant());
            return field;
        }
    }

    [CustomPropertyDrawer(typeof(StatValue))]
    internal sealed class StatValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => InlineFields.Row(property, "Stat", "Value");
    }

    [CustomPropertyDrawer(typeof(ResourceValue))]
    internal sealed class ResourceValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => InlineFields.Row(property, "Resource", "Current");
    }
}
