using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public readonly struct BlockAction
    {
        public readonly string Label;
        public readonly bool Enabled;
        public readonly Action Run;

        public BlockAction(string label, bool enabled, Action run)
        {
            Label = label;
            Enabled = enabled;
            Run = run;
        }

        public bool IsSeparator => Label == null;
        public static BlockAction Separator => default;
    }

    public static class BlockMenu
    {
        public static List<BlockAction> For(SerializedObject serializedObject, string path, Action<string> changed)
        {
            var actions = new List<BlockAction>();
            var property = serializedObject.FindProperty(path);
            if(property == null) return actions;

            var value = property.managedReferenceValue;
            var slotType = BlockTypes.SlotType(property);
            bool element = BlockEdits.TrySplit(path, out var arrayPath, out int index);
            int size = element ? serializedObject.FindProperty(arrayPath).arraySize : 0;

            actions.Add(new BlockAction("Copy", value != null, () => BlockClipboard.Copy(value)));
            actions.Add(new BlockAction("Paste", BlockClipboard.CanPasteInto(slotType), Edit(() => BlockEdits.Paste(serializedObject, path), changed)));
            if(element) actions.Add(new BlockAction("Duplicate", value != null, Edit(() => BlockEdits.Duplicate(serializedObject, path), changed)));
            actions.Add(BlockAction.Separator);
            if(element)
            {
                actions.Add(new BlockAction("Move Up", index > 0, Edit(() => BlockEdits.Move(serializedObject, path, -1), changed)));
                actions.Add(new BlockAction("Move Down", index < size - 1, Edit(() => BlockEdits.Move(serializedObject, path, 1), changed)));
            }
            actions.Add(new BlockAction("Reset", value != null, Edit(() => BlockEdits.Reset(serializedObject, path), changed)));
            actions.Add(new BlockAction(element ? "Remove" : "Clear", element || value != null, Edit(() => BlockEdits.Remove(serializedObject, path), changed)));
            actions.Add(BlockAction.Separator);
            actions.Add(new BlockAction("Edit Script", value != null, () => BlockTypes.OpenScript(value?.GetType())));
            return actions;
        }

        public static void Show(Rect rect, List<BlockAction> actions)
        {
            var menu = new GenericMenu();
            foreach(var action in actions)
            {
                if(action.IsSeparator)
                {
                    menu.AddSeparator(string.Empty);
                    continue;
                }
                var run = action.Run;
                if(action.Enabled) menu.AddItem(new GUIContent(action.Label), false, () => run());
                else menu.AddDisabledItem(new GUIContent(action.Label));
            }
            menu.DropDown(rect);
        }

        public static void Append(DropdownMenu menu, List<BlockAction> actions)
        {
            foreach(var action in actions)
            {
                if(action.IsSeparator)
                {
                    menu.AppendSeparator();
                    continue;
                }
                var run = action.Run;
                menu.AppendAction(action.Label, _ => run(), action.Enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        private static Action Edit(Func<string> edit, Action<string> changed)
        {
            return () =>
            {
                string path = edit();
                changed?.Invoke(path);
            };
        }
    }
}
