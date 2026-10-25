using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal static class DefinitionAssets
    {
        public static T Create<T>(string folder, string name) where T : ScriptableObject
        {
            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Safe(name)}.asset");
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            ResetId(asset, path);
            return asset;
        }

        public static T Find<T>(string name) where T : GameDefinition
        {
            if(string.IsNullOrWhiteSpace(name)) return null;
            string key = Key(name);
            foreach(var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if(asset != null && (Key(asset.name) == key || Key(asset.DisplayName) == key)) return asset;
            }
            return null;
        }

        public static string FolderOf<T>() where T : Object
        {
            foreach(var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if(path.StartsWith("Assets/")) return Path.GetDirectoryName(path)?.Replace('\\', '/');
            }
            return null;
        }

        public static void ResetId(ScriptableObject target, string path)
        {
            if(!(target is GameDefinition)) return;
            using(var serialized = new SerializedObject(target))
            {
                var id = serialized.FindProperty("id");
                if(id == null) return;
                id.stringValue = Path.GetFileNameWithoutExtension(path).Trim().ToLowerInvariant().Replace(' ', '_');
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void EnsureFolder(string folder)
        {
            if(string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Replace('\\', '/').TrimEnd('/').Split('/');
            string current = parts[0];
            for(int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static string Safe(string name)
        {
            if(string.IsNullOrWhiteSpace(name)) return "New";
            var invalid = Path.GetInvalidFileNameChars();
            string clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(clean) ? "New" : clean;
        }

        public static string Key(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        public static string ProjectRelative(string absolute)
        {
            if(string.IsNullOrEmpty(absolute)) return null;
            string full = absolute.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');
            if(full == data) return "Assets";
            return full.StartsWith(data + "/") ? "Assets" + full.Substring(data.Length) : null;
        }
    }
}
