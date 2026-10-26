using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal static class UnitLibrary
    {
        private static List<GameObject> units;
        private static readonly Dictionary<string, string[]> dependencies = new();

        public static IReadOnlyList<GameObject> All
        {
            get
            {
                if(units == null) Scan();
                return units;
            }
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.projectChanged += Clear;

        public static void Clear()
        {
            units = null;
            dependencies.Clear();
        }

        public static bool IsUnit(Object asset) => asset is GameObject prefab && prefab.TryGetComponent<Unit>(out _);

        public static List<GameObject> UsersOf(Object asset)
        {
            var users = new List<GameObject>();
            string path = AssetDatabase.GetAssetPath(asset);
            if(string.IsNullOrEmpty(path)) return users;
            foreach(var unit in All)
            {
                if(unit == null) continue;
                string unitPath = AssetDatabase.GetAssetPath(unit);
                if(!dependencies.TryGetValue(unitPath, out var found))
                {
                    found = AssetDatabase.GetDependencies(unitPath, false);
                    dependencies[unitPath] = found;
                }
                if(Array.IndexOf(found, path) >= 0) users.Add(unit);
            }
            return users;
        }

        public static T Read<T>(Component component, string field) where T : Object
        {
            if(component == null) return null;
            using(var serialized = new SerializedObject(component))
            {
                return serialized.FindProperty(field)?.objectReferenceValue as T;
            }
        }

        public static T[] ReadArray<T>(Component component, string field) where T : Object
        {
            var found = new List<T>();
            if(component == null) return found.ToArray();
            using(var serialized = new SerializedObject(component))
            {
                var array = serialized.FindProperty(field);
                if(array == null || !array.isArray) return found.ToArray();
                for(int i = 0; i < array.arraySize; i++)
                {
                    if(array.GetArrayElementAtIndex(i).objectReferenceValue is T each) found.Add(each);
                }
            }
            return found.ToArray();
        }

        public static UnitStatProfile ProfileOf(GameObject prefab)
        {
            if(prefab == null) return null;
            var profile = Read<UnitStatProfile>(prefab.GetComponent<StatsComponent>(), "profile");
            return profile != null ? profile : Read<UnitStatProfile>(prefab.GetComponent<ResourceComponent>(), "profile");
        }

        public static UnitStatProfile ResourceProfileOf(GameObject prefab)
        {
            if(prefab == null) return null;
            var profile = Read<UnitStatProfile>(prefab.GetComponent<ResourceComponent>(), "profile");
            return profile != null ? profile : ProfileOf(prefab);
        }

        public static Faction FactionOf(GameObject prefab) => prefab == null ? null : Read<Faction>(prefab.GetComponent<Unit>(), "faction");
        public static CombatTag[] TagsOf(GameObject prefab) => prefab == null ? Array.Empty<CombatTag>() : ReadArray<CombatTag>(prefab.GetComponent<Unit>(), "startingTags");
        public static DamagePipeline PipelineOf(GameObject prefab) => prefab == null ? null : Read<DamagePipeline>(prefab.GetComponent<HealthComponent>(), "pipeline");
        public static ResourceType HealthOf(GameObject prefab) => prefab == null ? null : Read<ResourceType>(prefab.GetComponent<HealthComponent>(), "healthType");

        public static float StatValue(UnitStatProfile profile, StatType type)
        {
            if(type == null) return 0f;
            if(profile != null)
            {
                foreach(var entry in profile.Stats ?? Array.Empty<StatEntry>())
                {
                    if(entry.Type == type) return entry.BaseValue;
                }
            }
            return type.DefaultValue;
        }

        public static bool HasStat(UnitStatProfile profile, StatType type)
        {
            if(profile == null || type == null) return false;
            foreach(var entry in profile.Stats ?? Array.Empty<StatEntry>())
            {
                if(entry.Type == type) return true;
            }
            return false;
        }

        public static ResourceEntry ResourceOf(UnitStatProfile profile, ResourceType type)
        {
            if(profile == null || type == null) return null;
            foreach(var entry in profile.Resources ?? Array.Empty<ResourceEntry>())
            {
                if(entry != null && entry.Type == type) return entry;
            }
            return null;
        }

        public static float MaxOf(UnitStatProfile profile, ResourceEntry entry)
        {
            if(entry == null) return 0f;
            return entry.Type != null && entry.Type.MaxStat != null ? StatValue(profile, entry.Type.MaxStat) : entry.BaseMax;
        }

        private static void Scan()
        {
            string script = ScriptPath();
            units = Find(script);
            if(units.Count == 0 && script != null) units = Find(null);
        }

        private static List<GameObject> Find(string script)
        {
            var found = new List<GameObject>();
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if(script != null && Array.IndexOf(AssetDatabase.GetDependencies(path, true), script) < 0) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab != null && prefab.TryGetComponent<Unit>(out _)) found.Add(prefab);
            }
            return found;
        }

        private static string ScriptPath()
        {
            foreach(var guid in AssetDatabase.FindAssets($"{nameof(Unit)} t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if(script != null && script.GetClass() == typeof(Unit)) return path;
            }
            return null;
        }
    }
}
