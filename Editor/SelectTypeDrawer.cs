using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    [CustomPropertyDrawer(typeof(SelectTypeAttribute))]
    public sealed class SelectTypeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if(property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return new HelpBox($"{property.displayName}: [SelectType] needs [SerializeReference]", HelpBoxMessageType.Warning);
            }
            return new BlockElement(property, preferredLabel ?? property.displayName);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if(property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            float x = position.x + EditorGUIUtility.labelWidth + 2f;
            var button = new Rect(x, position.y, position.xMax - x, EditorGUIUtility.singleLineHeight);
            var serializedObject = property.serializedObject;
            string path = property.propertyPath;
            var slotType = BlockTypes.SlotType(property);
            var current = BlockTypes.ValueType(property);
            if(EditorGUI.DropdownButton(button, new GUIContent(BlockTypes.DisplayName(current)), FocusType.Keyboard))
            {
                BlockTypePicker.Show(button, slotType, true, type =>
                {
                    if(type != current) BlockEdits.SetType(serializedObject, path, type);
                });
            }

            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }
    }
}
