using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    public static class DefinitionIdValidator
    {
        [MenuItem("Tools/CG Combat Framework/Validate Definition IDs", false, 40)]
        public static void Validate()
        {
            var seen = new Dictionary<string, GameDefinition>();
            int duplicates = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                if (seen.TryGetValue(def.Id, out var first))
                {
                    duplicates++;
                    Debug.LogError($"Duplicate ID '{def.Id}': '{first.name}' and '{def.name}'", def);
                }
                else seen.Add(def.Id, def);
            }
            if (duplicates == 0) Debug.Log($"All {seen.Count} definition IDs are unique");
        }
    }
}
