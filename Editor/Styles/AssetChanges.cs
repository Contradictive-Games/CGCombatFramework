using System;
using System.Collections.Generic;
using UnityEditor;

namespace CG.CombatFramework.Editor
{
    public static class AssetChanges
    {
        private static readonly HashSet<string> changed = new();
        private static readonly HashSet<string> structure = new();
        private static bool queued;

        public static event Action<IReadOnlyCollection<string>> Changed;
        public static event Action<IReadOnlyCollection<string>> CreatedOrDeleted;

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
                }
            }
            if(queued || (changed.Count == 0 && structure.Count == 0)) return;
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
        }
    }
}
