using System;
using System.Collections.Generic;
using UnityEditor;

namespace CG.CombatFramework.Editor
{
    public static class AssetChanges
    {
        private static readonly HashSet<string> changed = new();
        private static readonly HashSet<string> structure = new();
        private static readonly HashSet<string> imported = new();
        private static bool queued;
        private static bool objects;

        public static event Action<IReadOnlyCollection<string>> Changed;
        public static event Action<IReadOnlyCollection<string>> CreatedOrDeleted;
        public static event Action<IReadOnlyCollection<string>> Imported;
        public static event Action ObjectsChanged;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            ObjectChangeEvents.changesPublished -= OnChanges;
            ObjectChangeEvents.changesPublished += OnChanges;
        }

        public static string GuidOf(UnityEngine.Object asset)
        {
            if(asset == null) return null;
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        public static bool AnyCombat(IReadOnlyCollection<string> guids)
        {
            foreach(var guid in guids)
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if(type == null) continue;
                if(typeof(GameDefinition).IsAssignableFrom(type) || typeof(DamagePipeline).IsAssignableFrom(type) || type == typeof(UnityEngine.GameObject)) return true;
            }
            return false;
        }

        public static bool AnyCombatPath(IReadOnlyCollection<string> paths)
        {
            foreach(var path in paths)
            {
                if(IsCombatPath(path)) return true;
            }
            return false;
        }

        public static bool IsCombatPath(string path)
        {
            if(string.IsNullOrEmpty(path)) return false;
            if(path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return UnitLibrary.Touches(path);
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if(type == null) return path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
            return typeof(GameDefinition).IsAssignableFrom(type) || typeof(DamagePipeline).IsAssignableFrom(type);
        }

        public static bool Contains(IReadOnlyCollection<string> guids, UnityEngine.Object asset)
        {
            string guid = GuidOf(asset);
            if(guid == null) return false;
            foreach(var each in guids)
            {
                if(each == guid) return true;
            }
            return false;
        }

        internal static void OnImported(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var paths = new List<string>(importedAssets.Length + deletedAssets.Length + movedAssets.Length + movedFromAssetPaths.Length);
            paths.AddRange(importedAssets);
            paths.AddRange(deletedAssets);
            paths.AddRange(movedAssets);
            paths.AddRange(movedFromAssetPaths);
            if(paths.Count == 0) return;
            UnitLibrary.OnImported(paths);
            imported.UnionWith(paths);
            Queue();
        }

        private static void OnChanges(ref ObjectChangeEventStream stream)
        {
            for(int i = 0; i < stream.length; i++)
            {
                switch(stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var change);
                        changed.Add(change.guid.ToString());
                        break;
                    case ObjectChangeKind.CreateAssetObject:
                        stream.GetCreateAssetObjectEvent(i, out var created);
                        structure.Add(created.guid.ToString());
                        break;
                    case ObjectChangeKind.DestroyAssetObject:
                        stream.GetDestroyAssetObjectEvent(i, out var destroyed);
                        structure.Add(destroyed.guid.ToString());
                        break;
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectParent:
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    case ObjectChangeKind.UpdatePrefabInstances:
                        objects = true;
                        break;
                }
            }
            if(changed.Count > 0 || structure.Count > 0 || objects) Queue();
        }

        private static void Queue()
        {
            if(queued) return;
            queued = true;
            EditorApplication.delayCall += Publish;
        }

        private static void Publish()
        {
            queued = false;
            if(changed.Count > 0)
            {
                var guids = new List<string>(changed);
                changed.Clear();
                Changed?.Invoke(guids);
            }
            if(structure.Count > 0)
            {
                var guids = new List<string>(structure);
                structure.Clear();
                CreatedOrDeleted?.Invoke(guids);
            }
            if(imported.Count > 0)
            {
                var paths = new List<string>(imported);
                imported.Clear();
                Imported?.Invoke(paths);
                UnitLibrary.EndBatch();
            }
            if(objects)
            {
                objects = false;
                ObjectsChanged?.Invoke();
            }
        }
    }

    internal sealed class CombatAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            AssetChanges.OnImported(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}
