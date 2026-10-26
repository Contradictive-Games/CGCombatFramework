using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public static class AssetDeletion
    {
        private const int k_Listed = 6;

        public static bool CanDelete(Object asset)
        {
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsMainAsset(asset);
        }

        public static bool Confirm(Object asset)
        {
            if(asset == null) return false;
            string name = StudioKinds.NameOf(asset);
            if(!CanDelete(asset))
            {
                EditorUtility.DisplayDialog("Can't Delete", $"{name} is inside a package or another asset, so it can't be deleted here.", "OK");
                return false;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            var users = UsersOf(path);
            string used = users.Count == 0
                ? "Nothing in your combat assets, units or scenes uses it."
                : $"It's used by {users.Count}: {string.Join(", ", users.Take(k_Listed).Select(System.IO.Path.GetFileNameWithoutExtension))}{(users.Count > k_Listed ? ", ..." : string.Empty)}. Those references will become empty.";
            if(!EditorUtility.DisplayDialog($"Delete {name}?", $"{used}\n\n{path}\n\nIt goes to your system's trash, so it can be restored from there.", "Delete", "Cancel")) return false;

            if(!AssetDatabase.MoveAssetToTrash(path)) return false;
            UnitLibrary.Clear();
            StudioIndex.Clear();
            return true;
        }

        public static List<string> UsersOf(string path)
        {
            var users = new List<string>();
            foreach(var candidate in Candidates())
            {
                if(candidate == path) continue;
                if(Array.IndexOf(AssetDatabase.GetDependencies(candidate, false), path) >= 0) users.Add(candidate);
            }
            return users;
        }

        private static IEnumerable<string> Candidates()
        {
            var paths = new HashSet<string>();
            foreach(var filter in new[] { "t:GameDefinition", "t:DamagePipeline", "t:Scene" })
            {
                foreach(var guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }
            foreach(var unit in UnitLibrary.All)
            {
                if(unit != null) paths.Add(AssetDatabase.GetAssetPath(unit));
            }
            return paths;
        }
    }
}
