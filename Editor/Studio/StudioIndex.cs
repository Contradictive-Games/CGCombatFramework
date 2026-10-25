using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal sealed class StudioIndexEntry
    {
        public readonly HashSet<Type> Types = new();
        public int DirtyCount;
        public string Text = string.Empty;
        public int EmptySlots;
        public bool MissingTypes;

        public bool HasProblems => EmptySlots > 0 || MissingTypes;
    }

    internal static class StudioIndex
    {
        private static readonly Dictionary<ScriptableObject, StudioIndexEntry> entries = new();

        public static void Clear() => entries.Clear();

        public static StudioIndexEntry Of(ScriptableObject asset)
        {
            int dirty = EditorUtility.GetDirtyCount(asset);
            if(entries.TryGetValue(asset, out var entry) && entry.DirtyCount == dirty) return entry;
            entry = Build(asset, dirty);
            entries[asset] = entry;
            return entry;
        }

        public static string[] Terms(string query)
        {
            if(string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();
            return query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool Matches(string text, string[] terms)
        {
            foreach(var term in terms)
            {
                if(text.IndexOf(term, StringComparison.Ordinal) < 0) return false;
            }
            return true;
        }

        private static StudioIndexEntry Build(ScriptableObject asset, int dirty)
        {
            var entry = new StudioIndexEntry { DirtyCount = dirty };
            var text = new StringBuilder();
            text.Append(StudioKinds.NameOf(asset)).Append(' ').Append(asset.name).Append(' ');
            if(asset is GameDefinition definition) text.Append(definition.Id).Append(' ');

            using(var serializedObject = new SerializedObject(asset))
            {
                var iterator = serializedObject.GetIterator();
                while(iterator.NextVisible(true))
                {
                    if(iterator.propertyType != SerializedPropertyType.ManagedReference) continue;
                    var type = BlockTypes.ValueType(iterator);
                    if(type == null)
                    {
                        entry.EmptySlots++;
                        continue;
                    }
                    if(entry.Types.Add(type)) text.Append(BlockTypes.MenuPath(type)).Append(' ');
                }
            }

            entry.MissingTypes = SerializationUtility.HasManagedReferencesWithMissingTypes(asset);
            entry.Text = text.ToString().ToLowerInvariant();
            return entry;
        }
    }
}
