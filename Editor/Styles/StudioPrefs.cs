using System;
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

        public static event Action Changed;

        public static AssetLabel Label
        {
            get => (AssetLabel)GetInt(k_Label, 0);
            set => SetInt(k_Label, (int)value);
        }

        public static bool DragToReorder => Get(k_Drag, true);

        public static bool Get(string key, bool fallback) => EditorPrefs.GetBool(k_Prefix + key, fallback);
        public static int GetInt(string key, int fallback) => EditorPrefs.GetInt(k_Prefix + key, fallback);
        public static float GetFloat(string key, float fallback) => EditorPrefs.GetFloat(k_Prefix + key, fallback);

        public static void Set(string key, bool value)
        {
            EditorPrefs.SetBool(k_Prefix + key, value);
            Changed?.Invoke();
        }

        public static void SetInt(string key, int value)
        {
            EditorPrefs.SetInt(k_Prefix + key, value);
            Changed?.Invoke();
        }

        public static void Store(string key, bool value) => EditorPrefs.SetBool(k_Prefix + key, value);
        public static void Store(string key, int value) => EditorPrefs.SetInt(k_Prefix + key, value);
        public static void Store(string key, float value) => EditorPrefs.SetFloat(k_Prefix + key, value);
        public static void Store(string key, string value) => EditorPrefs.SetString(k_Prefix + key, value ?? string.Empty);
        public static string GetString(string key, string fallback = "") => EditorPrefs.GetString(k_Prefix + key, fallback);

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
    }
}
