using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class BlockElement : VisualElement
    {
        private readonly SerializedObject serializedObject;
        private readonly string path;
        private readonly VisualElement header;
        private readonly Foldout foldout;
        private readonly BlockIcon icon;
        private readonly Button picker;
        private readonly Label pickerLabel;
        private readonly Label summary;
        private readonly Button menu;
        private readonly VisualElement body;
        private long boundId = long.MinValue;
        private bool hasFields;

        public BlockElement(SerializedProperty property, string label)
        {
            serializedObject = property.serializedObject;
            path = property.propertyPath;

            CombatEditorStyles.Apply(this);
            AddToClassList("cg-block");
            EnableInClassList("cg-block--element", BlockEdits.TrySplit(path, out _, out _));

            header = new VisualElement();
            header.AddToClassList("cg-block__header");
            Add(header);

            foldout = new Foldout { text = BlockEdits.TrySplit(path, out _, out _) ? string.Empty : label, value = property.isExpanded };
            foldout.AddToClassList("cg-block__foldout");
            foldout.RegisterValueChangedCallback(OnExpanded);
            header.Add(foldout);

            icon = new BlockIcon();
            header.Add(icon);

            picker = new Button(OpenPicker);
            picker.AddToClassList("cg-picker");
            pickerLabel = CombatEditorStyles.Text(null, "cg-picker__label");
            picker.Add(pickerLabel);
            picker.Add(CombatEditorStyles.Icon("icon dropdown", "cg-picker__arrow"));
            header.Add(picker);

            summary = CombatEditorStyles.Text(null, "cg-summary");
            header.Add(summary);

            menu = new Button(OpenMenu);
            menu.AddToClassList("cg-menu-button");
            menu.Add(CombatEditorStyles.Icon("_Menu", "cg-menu-button__icon"));
            header.Add(menu);

            header.AddManipulator(new ContextualMenuManipulator(evt => BlockMenu.Append(evt.menu, BlockMenu.For(serializedObject, path, null))));

            body = new VisualElement();
            body.AddToClassList("cg-block__body");
            Add(body);

            Rebuild(property, false);
            this.TrackPropertyValue(property, OnChanged);
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            AssetChanges.Changed += OnAssetsChanged;
            for(var each = parent; each != null; each = each.parent)
            {
                if(each is BaseVerticalCollectionView list && list.parent is PropertyField) list.style.maxHeight = StyleKeyword.None;
            }
        }

        private void OnDetach(DetachFromPanelEvent evt) => AssetChanges.Changed -= OnAssetsChanged;

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            var property = Property();
            if(property != null) summary.text = BlockSummary.Of(property, true);
        }

        private SerializedProperty Property()
        {
            try
            {
                return serializedObject.targetObject != null ? serializedObject.FindProperty(path) : null;
            }
            catch(System.Exception)
            {
                return null;
            }
        }

        private void Rebuild(SerializedProperty property, bool bind)
        {
            boundId = property.managedReferenceId;
            var type = BlockTypes.ValueType(property);
            var category = BlockCategories.Of(type ?? BlockTypes.SlotType(property));
            BlockCategories.Apply(this, category);
            icon.SetCategory(category);
            header.tooltip = BlockTypes.TooltipOf(type);
            EnableInClassList("cg-block--none", type == null);
            pickerLabel.text = BlockTypes.DisplayName(type);

            body.Clear();
            hasFields = false;
            if(type != null)
            {
                foreach(var child in BlockTypes.Children(property))
                {
                    body.Add(new PropertyField(child));
                    hasFields = true;
                }
            }
            EnableInClassList("cg-block--leaf", !hasFields);
            if(bind && hasFields) body.Bind(serializedObject);
            Refresh(property);
        }

        private void Refresh(SerializedProperty property)
        {
            summary.text = BlockSummary.Of(property, true);
            bool expanded = hasFields && property.isExpanded;
            foldout.SetValueWithoutNotify(expanded);
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            summary.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnChanged(SerializedProperty _)
        {
            var property = Property();
            if(property == null) return;
            if(property.managedReferenceId != boundId) Rebuild(property, true);
            else Refresh(property);
        }

        private void OnExpanded(ChangeEvent<bool> evt)
        {
            if(evt.target != foldout) return;
            evt.StopPropagation();
            var property = Property();
            if(property == null) return;
            property.isExpanded = evt.newValue;
            Refresh(property);
        }

        private void OpenPicker()
        {
            var property = Property();
            if(property == null) return;
            var current = BlockTypes.ValueType(property);
            BlockTypePicker.Show(picker.worldBound, BlockTypes.SlotType(property), true, type =>
            {
                if(type != current) BlockEdits.SetType(serializedObject, path, type);
            });
        }

        private void OpenMenu()
        {
            if(Property() == null) return;
            BlockMenu.Show(menu.worldBound, BlockMenu.For(serializedObject, path, null));
        }
    }
}
