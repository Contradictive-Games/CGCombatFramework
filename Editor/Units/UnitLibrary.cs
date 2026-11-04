using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal static class UnitLibrary
    {
        private const BindingFlags k_Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static List<GameObject> units;
        private static readonly Dictionary<string, GameObject> byPath = new();
        private static readonly HashSet<string> pending = new();
        private static readonly HashSet<string> recent = new();
        private static readonly Dictionary<string, string[]> dependencies = new();
        private static readonly Dictionary<(Type, string), FieldInfo> fields = new();
        private static string scriptPath;
        private static bool scriptFound;
        private static bool saveQueued;

        public static IReadOnlyList<GameObject> All
        {
            get
            {
                if(units == null) Scan();
                else if(pending.Count > 0) ApplyPending();
                return units;
            }
        }

        internal static int LastChecked { get; private set; }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Persist;
            EditorApplication.quitting += Persist;
        }

        private static void Persist() => UnitScanCache.instance.Persist();

        public static void Clear()
        {
            units = null;
            byPath.Clear();
            pending.Clear();
            dependencies.Clear();
        }

        internal static void ResetCache()
        {
            Clear();
            scriptFound = false;
            UnitScanCache.instance.Reset(string.Empty);
        }

        internal static void OnImported(IReadOnlyCollection<string> paths)
        {
            dependencies.Clear();
            if(units == null) return;
            foreach(var path in paths)
            {
                if(!IsPrefabPath(path)) continue;
                pending.Add(path);
                if(byPath.ContainsKey(path)) recent.Add(path);
            }
        }

        internal static void EndBatch() => recent.Clear();

        public static bool Touches(string path)
        {
            if(!IsPrefabPath(path)) return false;
            if(recent.Contains(path)) return true;
            _ = All;
            return byPath.ContainsKey(path) || recent.Contains(path);
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
            var info = FieldOf(component.GetType(), field);
            if(info == null) return ReadSerialized<T>(component, field);
            var value = info.GetValue(component) as T;
            return value != null ? value : null;
        }

        public static T[] ReadArray<T>(Component component, string field) where T : Object
        {
            if(component == null) return Array.Empty<T>();
            var info = FieldOf(component.GetType(), field);
            if(info == null) return ReadArraySerialized<T>(component, field);
            var found = new List<T>();
            if(info.GetValue(component) is IEnumerable<T> values)
            {
                foreach(var each in values)
                {
                    if(each != null) found.Add(each);
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
            var cache = UnitScanCache.instance;
            string script = ScriptPath();
            string scriptGuid = script != null ? AssetDatabase.AssetPathToGUID(script) : string.Empty;
            if(cache.Script != scriptGuid) cache.Reset(scriptGuid);

            units = new List<GameObject>();
            byPath.Clear();
            pending.Clear();
            LastChecked = 0;
            var seen = new HashSet<string>();
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                seen.Add(guid);
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
                if(!cache.TryGet(guid, hash, out bool isUnit))
                {
                    isUnit = Check(path, script);
                    cache.Set(guid, hash, isUnit);
                    LastChecked++;
                }
                if(isUnit) Add(path);
            }
            cache.Keep(seen);
            QueueSave();
        }

        private static void ApplyPending()
        {
            var cache = UnitScanCache.instance;
            string script = ScriptPath();
            foreach(var path in pending)
            {
                if(byPath.TryGetValue(path, out var old))
                {
                    byPath.Remove(path);
                    units.Remove(old);
                }
                if(AssetDatabase.GetMainAssetTypeAtPath(path) == null) continue;
                bool isUnit = Check(path, script);
                cache.Set(AssetDatabase.AssetPathToGUID(path), AssetDatabase.GetAssetDependencyHash(path).ToString(), isUnit);
                if(isUnit) Add(path);
            }
            pending.Clear();
            units.RemoveAll(unit => unit == null);
            QueueSave();
        }

        private static void Add(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab == null || byPath.ContainsKey(path)) return;
            byPath[path] = prefab;
            if(!units.Contains(prefab)) units.Add(prefab);
        }

        private static bool Check(string path, string script)
        {
            if(script != null && Array.IndexOf(AssetDatabase.GetDependencies(path, true), script) < 0) return false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null && prefab.TryGetComponent<Unit>(out _);
        }

        private static bool IsPrefabPath(string path) => path != null && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        private static void QueueSave()
        {
            if(saveQueued || !UnitScanCache.instance.Dirty) return;
            saveQueued = true;
            EditorApplication.delayCall += () =>
            {
                saveQueued = false;
                UnitScanCache.instance.Persist();
            };
        }

        private static FieldInfo FieldOf(Type type, string name)
        {
            var key = (type, name);
            if(fields.TryGetValue(key, out var field)) return field;
            for(var current = type; current != null && field == null; current = current.BaseType)
            {
                field = current.GetField(name, k_Fields);
            }
            fields[key] = field;
            return field;
        }

        private static T ReadSerialized<T>(Component component, string field) where T : Object
        {
            using(var serialized = new SerializedObject(component))
            {
                return serialized.FindProperty(field)?.objectReferenceValue as T;
            }
        }

        private static T[] ReadArraySerialized<T>(Component component, string field) where T : Object
        {
            var found = new List<T>();
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

        private static string ScriptPath()
        {
            if(scriptFound) return scriptPath;
            scriptFound = true;
            scriptPath = null;
            foreach(var guid in AssetDatabase.FindAssets($"{nameof(Unit)} t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if(script != null && script.GetClass() == typeof(Unit))
                {
                    scriptPath = path;
                    break;
                }
            }
            return scriptPath;
        }
    }
}
