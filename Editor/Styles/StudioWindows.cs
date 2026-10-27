using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public static class StudioWindows
    {
        public const string k_Menu = "Tools/CG Combat Framework/";

        private sealed class AssetAction
        {
            public readonly string Label;
            public readonly Func<Object, bool> Applies;
            public readonly Action<Object> Run;

            public AssetAction(string label, Func<Object, bool> applies, Action<Object> run)
            {
                Label = label;
                Applies = applies;
                Run = run;
            }
        }

        private static readonly List<(string path, Action open, Func<bool> isChecked)> windows = new();
        private static readonly List<AssetAction> actions = new();

        public static void RegisterWindow(string path, Action open, Func<bool> isChecked = null)
        {
            windows.RemoveAll(each => each.path == path);
            windows.Add((path, open, isChecked));
        }

        public static void RegisterAssetAction(string label, Func<Object, bool> applies, Action<Object> run)
        {
            actions.RemoveAll(each => each.Label == label);
            actions.Add(new AssetAction(label, applies, run));
        }

        public static ToolbarMenu Menu()
        {
            var tools = new ToolbarMenu { text = "Tools" };
            tools.AddToClassList("cg-toolbar__tools");
            tools.menu.AppendAction("Combat Studio", _ => CombatStudioWindow.Open());
            tools.menu.AppendAction("Unit Creator", _ => CombatStudioWindow.OpenUnits());
            tools.menu.AppendAction("Pipeline Tester", _ => PipelineTesterWindow.Open());
            tools.menu.AppendAction("Database", _ => CombatDatabaseWindow.Open());
            tools.menu.AppendSeparator();
            tools.menu.AppendAction("Validate Definition IDs", _ => DefinitionIdValidator.Validate());
            if(windows.Count == 0) return tools;
            tools.menu.AppendSeparator();
            foreach(var (path, open, isChecked) in windows)
            {
                var run = open;
                var check = isChecked;
                tools.menu.AppendAction(path, _ => run(), _ => check != null && check() ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            return tools;
        }

        public static void AppendAssetActions(DropdownMenu menu, Object asset)
        {
            bool first = true;
            foreach(var action in actions)
            {
                if(asset == null || !action.Applies(asset)) continue;
                if(first) menu.AppendSeparator();
                first = false;
                var run = action.Run;
                menu.AppendAction(action.Label, _ => run(asset));
            }
        }

        public static void AppendAssetActions(GenericMenu menu, Object asset)
        {
            bool first = true;
            foreach(var action in actions)
            {
                if(asset == null || !action.Applies(asset)) continue;
                if(first) menu.AddSeparator(string.Empty);
                first = false;
                var run = action.Run;
                menu.AddItem(new GUIContent(action.Label), false, () => run(asset));
            }
        }
    }
}
