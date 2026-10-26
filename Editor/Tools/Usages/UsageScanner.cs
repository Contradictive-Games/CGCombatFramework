using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal sealed class Usage
    {
        public readonly Object Asset;
        public readonly string Path;
        public readonly string Where;
        public readonly int Line;

        public Usage(Object asset, string path, string where, int line = 0)
        {
            Asset = asset;
            Path = path;
            Where = where;
            Line = line;
        }
    }

    internal static class UsageScanner
    {
        private const string k_Progress = "Finding Usages";
        private static readonly string[] textExtensions = { ".cs", ".json", ".txt", ".csv", ".xml", ".yaml", ".yml" };

        public static List<string> Candidates(bool allPrefabs, bool scenes, bool allScriptables)
        {
            var paths = new HashSet<string>();
            foreach(var asset in CombatTools.CombatAssets())
            {
                paths.Add(AssetDatabase.GetAssetPath(asset));
            }
            foreach(var unit in UnitLibrary.All)
            {
                if(unit != null) paths.Add(AssetDatabase.GetAssetPath(unit));
            }
            if(allPrefabs) AddAll("t:Prefab", paths);
            if(scenes) AddAll("t:Scene", paths);
            if(allScriptables) AddAll("t:ScriptableObject", paths);
            paths.RemoveWhere(string.IsNullOrEmpty);
            return paths.ToList();
        }

        public static List<Usage> References(Object target, bool allPrefabs, bool scenes)
        {
            var usages = new List<Usage>();
            string targetPath = AssetDatabase.GetAssetPath(target);
            if(string.IsNullOrEmpty(targetPath)) return usages;
            bool main = AssetDatabase.IsMainAsset(target);
            var candidates = Candidates(allPrefabs, scenes, true);
            try
            {
                for(int i = 0; i < candidates.Count; i++)
                {
                    string path = candidates[i];
                    if(i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar(k_Progress, path, i / (float)candidates.Count)) break;
                    if(path == targetPath) continue;
                    if(Array.IndexOf(AssetDatabase.GetDependencies(path, false), targetPath) < 0) continue;
                    if(path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        usages.Add(new Usage(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), null, "Somewhere in this scene"));
                        continue;
                    }
                    int before = usages.Count;
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    foreach(var owner in OwnersOf(asset, path))
                    {
                        Scan(owner, asset, value => value == target || (main && value != null && AssetDatabase.IsSubAsset(value) && AssetDatabase.GetAssetPath(value) == targetPath), usages);
                    }
                    if(usages.Count == before) usages.Add(new Usage(asset, null, "Depends on it, but not through a field this tool can see"));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return usages;
        }

        public static List<Usage> TextMentions(string text)
        {
            var usages = new List<Usage>();
            if(string.IsNullOrWhiteSpace(text)) return usages;
            var files = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories)
                .Where(f => textExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
            try
            {
                for(int i = 0; i < files.Count; i++)
                {
                    if(i % 100 == 0 && EditorUtility.DisplayCancelableProgressBar(k_Progress, "Searching text for " + text, i / (float)files.Count)) break;
                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(files[i]);
                    }
                    catch(IOException)
                    {
                        continue;
                    }
                    for(int line = 0; line < lines.Length; line++)
                    {
                        if(lines[line].IndexOf(text, StringComparison.Ordinal) < 0) continue;
                        string relative = DefinitionAssets.ProjectRelative(files[i]);
                        var asset = relative != null ? AssetDatabase.LoadMainAssetAtPath(relative) : null;
                        if(asset == null) continue;
                        usages.Add(new Usage(asset, null, $"Line {line + 1}: {lines[line].Trim()}", line + 1));
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return usages;
        }

        public static List<Usage> BlockUsages(Type type)
        {
            var usages = new List<Usage>();
            if(type == null) return usages;
            foreach(var asset in CombatTools.CombatAssets())
            {
                Scan(asset, asset, null, usages, type);
            }
            foreach(var unit in UnitLibrary.All)
            {
                if(unit == null) continue;
                foreach(var component in unit.GetComponentsInChildren<Component>(true))
                {
                    if(component != null) Scan(component, unit, null, usages, type);
                }
            }
            return usages;
        }

        public static List<Object> Unused(bool scenes)
        {
            var referenced = new HashSet<string>();
            var candidates = Candidates(true, scenes, true);
            try
            {
                for(int i = 0; i < candidates.Count; i++)
                {
                    if(i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar(k_Progress, "Collecting references", i / (float)candidates.Count)) break;
                    foreach(var dependency in AssetDatabase.GetDependencies(candidates[i], false))
                    {
                        if(dependency != candidates[i]) referenced.Add(dependency);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return CombatTools.CombatAssets().Where(a => !referenced.Contains(AssetDatabase.GetAssetPath(a))).Cast<Object>().ToList();
        }

        private static IEnumerable<Object> OwnersOf(Object asset, string path)
        {
            if(asset is GameObject prefab)
            {
                foreach(var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if(component != null) yield return component;
                }
                yield break;
            }
            foreach(var each in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if(each != null && !(each is GameObject) && !(each is Component)) yield return each;
            }
        }

        private static void Scan(Object owner, Object asset, Func<Object, bool> matches, List<Usage> into, Type blockType = null)
        {
            using(var serialized = new SerializedObject(owner))
            {
                foreach(var property in CombatTools.Walk(serialized))
                {
                    bool hit = blockType != null
                        ? property.propertyType == SerializedPropertyType.ManagedReference && BlockTypes.ValueType(property) == blockType
                        : property.propertyType == SerializedPropertyType.ObjectReference && matches(property.objectReferenceValue);
                    if(!hit) continue;
                    string where = CombatTools.Describe(serialized, property.propertyPath);
                    if(owner is Component component) where = $"{CombatTools.PathOfComponent(component)} › {where}";
                    into.Add(new Usage(asset, property.propertyPath, where));
                }
            }
        }

        private static void AddAll(string filter, HashSet<string> into)
        {
            foreach(var guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
            {
                into.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
        }
    }
}
