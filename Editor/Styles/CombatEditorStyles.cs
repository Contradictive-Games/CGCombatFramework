using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public static class CombatEditorStyles
    {
        private const string k_StyleSheet = "CGCombatEditor";

        private static StyleSheet sheet;

        public static void Apply(VisualElement element)
        {
            if(sheet == null) sheet = Load();
            if(sheet != null && !element.styleSheets.Contains(sheet)) element.styleSheets.Add(sheet);
            element.AddToClassList("cg-themed");
            element.EnableInClassList("cg-dark", EditorGUIUtility.isProSkin);
            element.EnableInClassList("cg-light", !EditorGUIUtility.isProSkin);
        }

        public static Label Text(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        public static void SetText(VisualElement element, string text)
        {
            if(!(element is Label label)) return;
            label.text = text ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public static void Show(VisualElement element, bool visible)
        {
            if(element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static Image Icon(string name, string className)
        {
            var image = new Image { image = Texture(name), scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList(className);
            return image;
        }

        public static Texture Texture(string name)
        {
            if(EditorGUIUtility.isProSkin)
            {
                var dark = EditorGUIUtility.FindTexture("d_" + name);
                if(dark != null) return dark;
            }
            return EditorGUIUtility.FindTexture(name);
        }

        public static VisualElement Header(string title)
        {
            var header = new VisualElement();
            header.AddToClassList("cg-pane__header");
            header.Add(Text(title, "cg-pane__title"));
            return header;
        }

        public static Button DropButton(string text, Action<Rect> show, string tooltip = null)
        {
            Button button = null;
            button = new Button(() => show(button.worldBound)) { tooltip = tooltip };
            button.AddToClassList("cg-view-button");
            button.Add(Text(text, "cg-view-button__label"));
            button.Add(Icon("icon dropdown", "cg-picker__arrow"));
            return button;
        }

        public static VisualElement Chip(string text, string className = null)
        {
            var chip = Text(text, "cg-chip");
            if(className != null) chip.AddToClassList(className);
            return chip;
        }

        public static VisualElement Section(string title, string key, out VisualElement content)
        {
            var foldout = new Foldout { text = title, value = true, viewDataKey = key };
            foldout.AddToClassList("cg-section");
            content = foldout.contentContainer;
            return foldout;
        }

        public static string Number(float value, int decimals)
        {
            return value.ToString("F" + Mathf.Clamp(decimals, 0, 4));
        }

        private static StyleSheet Load()
        {
            foreach(var guid in AssetDatabase.FindAssets($"{k_StyleSheet} t:StyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if(Path.GetFileNameWithoutExtension(path) == k_StyleSheet) return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            return null;
        }
    }
}
