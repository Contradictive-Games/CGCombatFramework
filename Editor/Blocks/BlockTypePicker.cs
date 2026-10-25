using System;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    public sealed class BlockTypePicker : AdvancedDropdown
    {
        private readonly Type baseType;
        private readonly bool allowNone;
        private readonly Action<Type> picked;
        private int nextId;

        private BlockTypePicker(Type baseType, bool allowNone, Action<Type> picked) : base(new AdvancedDropdownState())
        {
            this.baseType = baseType;
            this.allowNone = allowNone;
            this.picked = picked;
            minimumSize = new Vector2(260f, 320f);
        }

        public static void Show(Rect rect, Type baseType, bool allowNone, Action<Type> picked)
        {
            if(baseType == null) return;
            new BlockTypePicker(baseType, allowNone, picked).Show(rect);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(BlockCategories.Label(BlockCategories.Of(baseType)));
            if(allowNone) root.AddChild(new TypeItem("None", null, ++nextId));

            foreach(var type in BlockTypes.Candidates(baseType))
            {
                var parts = BlockTypes.MenuPath(type).Split('/');
                var parent = root;
                for(int i = 0; i < parts.Length - 1; i++)
                {
                    parent = Folder(parent, parts[i]);
                }
                parent.AddChild(new TypeItem(parts[parts.Length - 1], type, ++nextId));
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if(item is TypeItem typed) picked?.Invoke(typed.Type);
        }

        private AdvancedDropdownItem Folder(AdvancedDropdownItem parent, string name)
        {
            foreach(var child in parent.children)
            {
                if(!(child is TypeItem) && child.name == name) return child;
            }
            var folder = new AdvancedDropdownItem(name) { id = ++nextId };
            parent.AddChild(folder);
            return folder;
        }

        private sealed class TypeItem : AdvancedDropdownItem
        {
            public readonly Type Type;

            public TypeItem(string name, Type type, int id) : base(name)
            {
                Type = type;
                this.id = id;
            }
        }
    }
}
