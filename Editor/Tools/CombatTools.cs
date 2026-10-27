using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal static class CombatTools
    {
        public const string k_Menu = StudioWindows.k_Menu;
        private const string k_StyleSheet = "CombatTools";

        private static StyleSheet sheet;

        [InitializeOnLoadMethod]
        private static void Register()
        {
            StudioWindows.RegisterWindow(CombatDoctorWindow.k_Title, CombatDoctorWindow.Open);
            StudioWindows.RegisterWindow(UsageFinderWindow.k_Title, UsageFinderWindow.Open);
            StudioWindows.RegisterWindow(CombatMonitorWindow.k_Title, CombatMonitorWindow.Open);
            StudioWindows.RegisterWindow(FirableLabWindow.k_Title, FirableLabWindow.Open);
            StudioWindows.RegisterWindow(EffectTimelineWindow.k_Title, EffectTimelineWindow.Open);
            StudioWindows.RegisterWindow(FactionMatrixWindow.k_Title, FactionMatrixWindow.Open);
            StudioWindows.RegisterWindow(TagTreeWindow.k_Title, TagTreeWindow.Open);

            StudioWindows.RegisterAssetAction("Find Usages", asset => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)), UsageFinderWindow.Open);
            StudioWindows.RegisterAssetAction("Check with Combat Doctor", asset => CombatDoctorWindow.Covers(asset), CombatDoctorWindow.Open);
            StudioWindows.RegisterAssetAction("Open in Firable Lab", asset => asset is FirableDefinition, asset => FirableLabWindow.Open((FirableDefinition)asset));
            StudioWindows.RegisterAssetAction("Open in Effect Timeline", asset => asset is StatusEffectDefinition, asset => EffectTimelineWindow.Open((StatusEffectDefinition)asset));
            StudioWindows.RegisterAssetAction("Show in Tag Tree", asset => asset is CombatTag, asset => TagTreeWindow.Open((CombatTag)asset));
            StudioWindows.RegisterAssetAction("Show in Faction Matrix", asset => asset is Faction, asset => FactionMatrixWindow.Open());
        }

        [MenuItem("Assets/CG Combat Framework/Find Usages", false, 2000)]
        private static void FindUsagesOfSelection() => UsageFinderWindow.Open(Selection.activeObject);

        [MenuItem("Assets/CG Combat Framework/Find Usages", true)]
        private static bool CanFindUsagesOfSelection() => Selection.activeObject != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));

        public static void Apply(VisualElement root, string className)
        {
            CombatEditorStyles.Apply(root);
            if(sheet == null) sheet = Load();
            if(sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.AddToClassList("cg-studio");
            root.AddToClassList("cg-tools");
            root.AddToClassList(className);
        }

        public static Toolbar Toolbar(string title)
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("cg-toolbar");
            toolbar.Add(CombatEditorStyles.Text(title, "cg-toolbar__title"));
            return toolbar;
        }

        public static void Finish(Toolbar toolbar, Action<Rect> view)
        {
            if(view != null)
            {
                ToolbarButton button = null;
                button = new ToolbarButton(() => view(button.worldBound)) { text = "View" };
                toolbar.Add(button);
            }
            toolbar.Add(StudioWindows.Menu());
        }

        public static Button Small(string text, Action run, string tooltip = null)
        {
            var button = new Button(run) { text = text, tooltip = tooltip };
            button.AddToClassList("cg-card__button");
            return button;
        }

        public static void Jump(Object asset, string path = null)
        {
            if(asset == null) return;
            if(asset is Component component) asset = component.gameObject;
            if(asset is GameObject gameObject && !UnitLibrary.IsUnit(gameObject) && gameObject.transform.root != gameObject.transform) asset = gameObject.transform.root.gameObject;
            if(asset is ScriptableObject scriptable && StudioKinds.IndexOf(scriptable.GetType()) >= 0)
            {
                CombatStudioWindow.Open(scriptable, path);
                return;
            }
            if(asset is GameObject prefab && UnitLibrary.IsUnit(prefab))
            {
                CombatStudioWindow.OpenUnit(prefab);
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static string Describe(SerializedObject serializedObject, string path)
        {
            if(serializedObject == null || string.IsNullOrEmpty(path)) return string.Empty;
            var parts = new List<string>();
            var segments = path.Split('.');
            string current = null;
            for(int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if(segment == "Array" && i + 1 < segments.Length && segments[i + 1].StartsWith("data[", StringComparison.Ordinal))
                {
                    string data = segments[++i];
                    current = $"{current}.Array.{data}";
                    if(int.TryParse(data.Substring(5, data.Length - 6), out int index) && parts.Count > 0) parts[parts.Count - 1] += $" {index + 1}";
                    AddType(serializedObject.FindProperty(current), parts);
                    continue;
                }
                current = current == null ? segment : $"{current}.{segment}";
                var property = serializedObject.FindProperty(current);
                parts.Add(property != null ? property.displayName : ObjectNames.NicifyVariableName(segment));
                AddType(property, parts);
            }
            return string.Join(" › ", parts);
        }

        public static string PathOfComponent(Component component)
        {
            if(component == null) return string.Empty;
            var root = component.transform.root;
            string path = component.transform == root ? string.Empty : AnimationUtility.CalculateTransformPath(component.transform, root) + " › ";
            return path + ObjectNames.NicifyVariableName(component.GetType().Name);
        }

        public static IEnumerable<SerializedProperty> Walk(SerializedObject serializedObject)
        {
            var iterator = serializedObject.GetIterator();
            bool enter = true;
            while(iterator.Next(enter))
            {
                enter = iterator.propertyType == SerializedPropertyType.Generic || iterator.propertyType == SerializedPropertyType.ManagedReference;
                if(iterator.propertyPath == "m_Script") continue;
                yield return iterator;
            }
        }

        public static List<ScriptableObject> CombatAssets()
        {
            var assets = new List<ScriptableObject>();
            foreach(var guid in AssetDatabase.FindAssets("t:GameDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(asset != null) assets.Add(asset);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:DamagePipeline"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<DamagePipeline>(AssetDatabase.GUIDToAssetPath(guid));
                if(asset != null) assets.Add(asset);
            }
            return assets;
        }

        public static Texture SeverityIcon(int severity)
        {
            switch(severity)
            {
                case 0: return EditorGUIUtility.IconContent("console.erroricon.sml").image;
                case 1: return EditorGUIUtility.IconContent("console.warnicon.sml").image;
                default: return EditorGUIUtility.IconContent("console.infoicon.sml").image;
            }
        }

        private static void AddType(SerializedProperty property, List<string> parts)
        {
            if(property == null || property.propertyType != SerializedPropertyType.ManagedReference) return;
            var type = BlockTypes.ValueType(property);
            parts.Add(type != null ? BlockTypes.DisplayName(type) : "Empty");
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
