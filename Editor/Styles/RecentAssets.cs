using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public static class RecentAssets
    {
        private const string k_Key = "Recent.";
        private const int k_Max = 24;

        private static List<string> guids;

        private static string Key => k_Key + PlayerSettings.productGUID.ToString("N");

        [InitializeOnLoadMethod]
        private static void Hook() => Selection.selectionChanged += () => Remember(Selection.activeObject);

        public static void Remember(Object asset)
        {
            if(asset == null || !EditorUtility.IsPersistent(asset)) return;
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _)) return;
            var list = Load();
            if(list.Count > 0 && list[0] == guid) return;
            list.Remove(guid);
            list.Insert(0, guid);
            if(list.Count > k_Max) list.RemoveRange(k_Max, list.Count - k_Max);
            StudioPrefs.Store(Key, string.Join("|", list));
        }

        public static T Latest<T>() where T : Object
        {
            foreach(var guid in Load())
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if(string.IsNullOrEmpty(path)) continue;
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if(type == null || !typeof(T).IsAssignableFrom(type)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if(asset != null) return asset;
            }
            return null;
        }

        private static List<string> Load()
        {
            guids ??= new List<string>(StudioPrefs.GetString(Key).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            return guids;
        }
    }
}
