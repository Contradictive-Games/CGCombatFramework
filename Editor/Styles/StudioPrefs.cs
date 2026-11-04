using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    public enum AssetLabel { DisplayName, Id, FileName }

    public static class StudioPrefs
    {
        private const string k_Prefix = "CG.CombatStudio.";
        private const string k_Label = "Label";
        private const string k_Drag = "DragReorder";

        private static readonly Dictionary<string, bool> bools = new();
        private static readonly Dictionary<string, int> ints = new();
        private static readonly Dictionary<string, float> floats = new();
        private static readonly Dictionary<string, string> strings = new();
        private static readonly HashSet<string> missing = new();

        public static event Action Changed;

        public static AssetLabel Label
        {
            get => (AssetLabel)GetInt(k_Label, 0);
            set => SetInt(k_Label, (int)value);
        }

        public static bool DragToReorder => Get(k_Drag, true);

        public static bool Get(string key, bool fallback)
        {
            if(bools.TryGetValue(key, out bool value)) return value;
            if(!Exists(key)) return fallback;
            value = EditorPrefs.GetBool(k_Prefix + key, fallback);
            bools[key] = value;
            return value;
        }

        public static int GetInt(string key, int fallback)
        {
            if(ints.TryGetValue(key, out int value)) return value;
            if(!Exists(key)) return fallback;
            value = EditorPrefs.GetInt(k_Prefix + key, fallback);
            ints[key] = value;
            return value;
        }

        public static float GetFloat(string key, float fallback)
        {
            if(floats.TryGetValue(key, out float value)) return value;
            if(!Exists(key)) return fallback;
            value = EditorPrefs.GetFloat(k_Prefix + key, fallback);
            floats[key] = value;
            return value;
        }

        public static string GetString(string key, string fallback = "")
        {
            if(strings.TryGetValue(key, out var value)) return value;
            if(!Exists(key)) return fallback;
            value = EditorPrefs.GetString(k_Prefix + key, fallback);
            strings[key] = value;
            return value;
        }

        public static void Set(string key, bool value)
        {
            Store(key, value);
            Changed?.Invoke();
        }

        public static void SetInt(string key, int value)
        {
            Store(key, value);
            Changed?.Invoke();
        }

        public static void Store(string key, bool value)
        {
            bools[key] = value;
            missing.Remove(key);
            EditorPrefs.SetBool(k_Prefix + key, value);
        }

        public static void Store(string key, int value)
        {
            ints[key] = value;
            missing.Remove(key);
            EditorPrefs.SetInt(k_Prefix + key, value);
        }

        public static void Store(string key, float value)
        {
            floats[key] = value;
            missing.Remove(key);
            EditorPrefs.SetFloat(k_Prefix + key, value);
        }

        public static void Store(string key, string value)
        {
            strings[key] = value ?? string.Empty;
            missing.Remove(key);
            EditorPrefs.SetString(k_Prefix + key, value ?? string.Empty);
        }

        public static void Remove(string key)
        {
            bools.Remove(key);
            ints.Remove(key);
            floats.Remove(key);
            strings.Remove(key);
            missing.Add(key);
            EditorPrefs.DeleteKey(k_Prefix + key);
        }

        internal static void Forget()
        {
            bools.Clear();
            ints.Clear();
            floats.Clear();
            strings.Clear();
            missing.Clear();
        }

        public static string LabelOf(UnityEngine.Object asset) => LabelOf(asset, Label);

        public static string LabelOf(UnityEngine.Object asset, AssetLabel label)
        {
            if(asset == null) return "None";
            switch(label)
            {
                case AssetLabel.Id: return asset is GameDefinition definition ? definition.Id : asset.name;
                case AssetLabel.FileName: return asset.name;
                default: return StudioKinds.NameOf(asset);
            }
        }

        public static void Toggle(GenericMenu menu, string label, string key, bool fallback)
        {
            bool value = Get(key, fallback);
            menu.AddItem(new GUIContent(label), value, () => Set(key, !value));
        }

        public static void Choice(GenericMenu menu, string label, string key, int value, int fallback)
        {
            menu.AddItem(new GUIContent(label), GetInt(key, fallback) == value, () => SetInt(key, value));
        }

        public static void Labels(GenericMenu menu, string prefix = "")
        {
            menu.AddDisabledItem(new GUIContent(prefix + "Show As"));
            Choice(menu, prefix + "Display Name", k_Label, (int)AssetLabel.DisplayName, 0);
            Choice(menu, prefix + "ID", k_Label, (int)AssetLabel.Id, 0);
            Choice(menu, prefix + "File Name", k_Label, (int)AssetLabel.FileName, 0);
        }

        public static void Behavior(GenericMenu menu, string prefix = "")
        {
            Toggle(menu, prefix + "Drag to Reorder", k_Drag, true);
        }

        private static bool Exists(string key)
        {
            if(missing.Contains(key)) return false;
            if(EditorPrefs.HasKey(k_Prefix + key)) return true;
            missing.Add(key);
            return false;
        }
    }
}
