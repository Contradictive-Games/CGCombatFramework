using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    [CustomPropertyDrawer(typeof(ScalableValue))]
    public sealed class ScalableValueDrawer : PropertyDrawer
    {
        private const string k_Base = nameof(ScalableValue.Base);
        private const string k_Scalers = nameof(ScalableValue.Scalers);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            string scalersPath = property.FindPropertyRelative(k_Scalers).propertyPath;

            var root = new VisualElement();
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-scalable");

            var row = new VisualElement();
            row.AddToClassList("cg-scalable__row");
            root.Add(row);

            var baseField = new PropertyField(property.FindPropertyRelative(k_Base), preferredLabel ?? property.displayName);
            baseField.AddToClassList("cg-scalable__base");
            row.Add(baseField);

            var toggle = new Button();
            toggle.AddToClassList("cg-scalable__toggle");
            row.Add(toggle);

            var list = new PropertyField(property.FindPropertyRelative(k_Scalers));
            list.AddToClassList("cg-scalable__list");
            root.Add(list);

            void Refresh()
            {
                var scalers = serializedObject.FindProperty(scalersPath);
                if(scalers == null) return;
                int count = scalers.arraySize;
                toggle.text = count == 0 ? "+ Scaler" : count == 1 ? "1 scaler" : $"{count} scalers";
                toggle.EnableInClassList("cg-scalable__toggle--active", count > 0);
                list.style.display = scalers.isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            toggle.clicked += () =>
            {
                var scalers = serializedObject.FindProperty(scalersPath);
                if(scalers == null) return;
                scalers.isExpanded = !scalers.isExpanded;
                Refresh();
            };

            root.TrackPropertyValue(property.FindPropertyRelative(k_Scalers), _ => Refresh());
            Refresh();
            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var scalers = property.FindPropertyRelative(k_Scalers);
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(scalers, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, property.FindPropertyRelative(k_Base), label);

            var scalers = property.FindPropertyRelative(k_Scalers);
            float top = line.yMax + EditorGUIUtility.standardVerticalSpacing;
            var rest = new Rect(position.x, top, position.width, EditorGUI.GetPropertyHeight(scalers, true));
            EditorGUI.indentLevel++;
            EditorGUI.PropertyField(rest, scalers, true);
            EditorGUI.indentLevel--;
        }
    }
}
