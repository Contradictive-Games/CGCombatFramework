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
