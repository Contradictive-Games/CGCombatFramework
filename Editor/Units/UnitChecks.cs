using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal sealed class UnitIssue
    {
        public readonly string Message;
        public readonly HelpBoxMessageType Severity;
        public readonly string FixLabel;
        public readonly Action Fix;

        public UnitIssue(string message, HelpBoxMessageType severity, string fixLabel = null, Action fix = null)
        {
            Message = message;
            Severity = severity;
            FixLabel = fixLabel;
            Fix = fix;
        }
    }

    internal static class UnitChecks
    {
        public static List<UnitIssue> Of(GameObject prefab)
        {
            var issues = new List<UnitIssue>();
            if(prefab == null) return issues;

            var stats = prefab.GetComponent<StatsComponent>();
            var resources = prefab.GetComponent<ResourceComponent>();
            var health = prefab.GetComponent<HealthComponent>();
            var statProfile = UnitLibrary.Read<UnitStatProfile>(stats, "profile");
            var resourceProfile = UnitLibrary.Read<UnitStatProfile>(resources, "profile");

            if(health != null)
            {
                var healthType = UnitLibrary.HealthOf(prefab);
                if(resources == null)
                {
                    issues.Add(new UnitIssue("Health needs a Resource Component to hold its health pool", HelpBoxMessageType.Error, "Add Resource Component",
                        () => UnitEdits.AddPart(prefab, UnitParts.Of(UnitPart.Resources))));
                }
                if(healthType == null) issues.Add(new UnitIssue("Health has no Health Type, so it can't take damage", HelpBoxMessageType.Error));
                else if(resourceProfile != null && UnitLibrary.ResourceOf(resourceProfile, healthType) == null)
                {
                    issues.Add(new UnitIssue($"{StudioKinds.NameOf(healthType)} isn't in {StudioKinds.NameOf(resourceProfile)}, so the unit can't take damage", HelpBoxMessageType.Error, "Add to Profile",
                        () => AddResource(resourceProfile, healthType)));
                }
                if(UnitLibrary.PipelineOf(prefab) == null) issues.Add(new UnitIssue("No damage pipeline, so damage is applied as-is", HelpBoxMessageType.Info));
            }

            if(stats != null && statProfile == null) issues.Add(new UnitIssue("Stats has no profile, so every stat uses its default", HelpBoxMessageType.Warning));
            if(resources != null && resourceProfile == null) issues.Add(new UnitIssue("Resources has no profile, so the unit has no pools", HelpBoxMessageType.Warning));
            if(statProfile != null && resourceProfile != null && statProfile != resourceProfile)
            {
                issues.Add(new UnitIssue("Stats and Resources use different profiles", HelpBoxMessageType.Info, $"Use {StudioKinds.NameOf(statProfile)} for Both",
                    () => UnitEdits.SetProfile(prefab, statProfile)));
            }

            var profile = resourceProfile ?? statProfile;
            if(profile != null)
            {
                foreach(var entry in profile.Resources ?? Array.Empty<ResourceEntry>())
                {
                    var maxStat = entry?.Type != null ? entry.Type.MaxStat : null;
                    if(maxStat == null || UnitLibrary.HasStat(statProfile ?? profile, maxStat)) continue;
                    var target = statProfile ?? profile;
                    issues.Add(new UnitIssue($"{StudioKinds.NameOf(entry.Type)} gets its max from {StudioKinds.NameOf(maxStat)}, which isn't in the profile, so it starts at {maxStat.DefaultValue:0.##}", HelpBoxMessageType.Warning,
                        $"Add {StudioKinds.NameOf(maxStat)}", () => AddStat(target, maxStat, entry.BaseMax)));
                }
            }

            if(prefab.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                issues.Add(new UnitIssue("No colliders, so queries and projectiles pass right through", HelpBoxMessageType.Warning, "Add Capsule Collider",
                    () => UnitEdits.AddCollider(prefab)));
            }

            var loadout = prefab.GetComponent<FirableLoadout>();
            if(loadout != null)
            {
                using(var serialized = new SerializedObject(loadout))
                {
                    var entries = serialized.FindProperty("startingLoadout");
                    int missing = 0;
                    for(int i = 0; entries != null && i < entries.arraySize; i++)
                    {
                        var entry = entries.GetArrayElementAtIndex(i);
                        if(entry.FindPropertyRelative("Slot").objectReferenceValue == null || entry.FindPropertyRelative("Firable").objectReferenceValue == null) missing++;
                    }
                    if(missing > 0) issues.Add(new UnitIssue(missing == 1 ? "1 loadout entry is missing a slot or firable" : $"{missing} loadout entries are missing a slot or firable", HelpBoxMessageType.Warning));
                }
            }

            if(prefab.GetComponent<TransformAimProvider>() != null && prefab.GetComponent<GroundPlaneAimProvider>() != null)
            {
                issues.Add(new UnitIssue("Two aim providers. Only the first one is used", HelpBoxMessageType.Warning));
            }
            return issues;
        }

        public static int Problems(GameObject prefab) => Of(prefab).Count(issue => issue.Severity != HelpBoxMessageType.Info);

        private static void AddResource(UnitStatProfile profile, ResourceType type)
        {
            Undo.RecordObject(profile, "Add Resource");
            var list = (profile.Resources ?? Array.Empty<ResourceEntry>()).ToList();
            list.Add(new ResourceEntry { Type = type });
            profile.Resources = list.ToArray();
            EditorUtility.SetDirty(profile);
        }

        private static void AddStat(UnitStatProfile profile, StatType type, float value)
        {
            Undo.RecordObject(profile, "Add Stat");
            var list = (profile.Stats ?? Array.Empty<StatEntry>()).ToList();
            list.Add(new StatEntry { Type = type, BaseValue = value });
            profile.Stats = list.ToArray();
            EditorUtility.SetDirty(profile);
        }
    }

    internal static class UnitEdits
    {
        public static void Modify(GameObject prefab, Action<GameObject> change)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            if(string.IsNullOrEmpty(path)) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            UnitLibrary.Clear();
        }

        public static void AddPart(GameObject prefab, PartInfo part)
        {
            if(part == null) return;
            var profile = UnitLibrary.ProfileOf(prefab);
            var health = UnitLibrary.ResourceProfileOf(prefab)?.Resources?.FirstOrDefault(r => r?.Type != null)?.Type;
            Modify(prefab, root =>
            {
                if(root.GetComponent(part.Type) != null) return;
                foreach(var other in UnitParts.All)
                {
                    if((part.Excludes & other.Part) != 0) Remove(root, other.Type);
                }
                if(part.Part == UnitPart.Knockback && !root.TryGetComponent<Rigidbody>(out _)) root.AddComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation;
                var component = root.AddComponent(part.Type);
                using(var serialized = new SerializedObject(component))
                {
                    var field = serialized.FindProperty("profile");
                    if(field != null) field.objectReferenceValue = profile;
                    var healthType = serialized.FindProperty("healthType");
                    if(healthType != null) healthType.objectReferenceValue = health;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }

        public static void RemovePart(GameObject prefab, PartInfo part)
        {
            if(part == null) return;
            Modify(prefab, root =>
            {
                foreach(var other in UnitParts.All)
                {
                    if((other.Requires & part.Part) != 0) Remove(root, other.Type);
                }
                Remove(root, part.Type);
            });
        }

        public static void SetProfile(GameObject prefab, UnitStatProfile profile)
        {
            foreach(var component in new Component[] { prefab.GetComponent<StatsComponent>(), prefab.GetComponent<ResourceComponent>() })
            {
                if(component == null) continue;
                using(var serialized = new SerializedObject(component))
                {
                    serialized.FindProperty("profile").objectReferenceValue = profile;
                    serialized.ApplyModifiedProperties();
                }
            }
        }

        public static void AddCollider(GameObject prefab)
        {
            Modify(prefab, root =>
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 1f, 0f);
                capsule.height = 2f;
                capsule.radius = 0.5f;
            });
        }

        public static void AddHitbox(GameObject prefab)
        {
            Modify(prefab, root =>
            {
                var child = new GameObject(GameObjectUtility.GetUniqueNameForSibling(root.transform, "Hitbox"));
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                child.AddComponent<SphereCollider>().radius = 0.25f;
                var hitbox = child.AddComponent<Hitbox>();
                hitbox.Owner = root.GetComponent<Unit>();
                hitbox.DamageMultiplier = 2f;
            });
        }

        public static GameObject MakeProfileUnique(GameObject prefab)
        {
            var profile = UnitLibrary.ProfileOf(prefab);
            if(profile == null) return prefab;
            string from = AssetDatabase.GetAssetPath(profile);
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(prefab))?.Replace('\\', '/');
            string to = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{DefinitionAssets.Safe(prefab.name)} Profile.asset");
            if(!AssetDatabase.CopyAsset(from, to)) return prefab;
            var copy = AssetDatabase.LoadAssetAtPath<UnitStatProfile>(to);
            DefinitionAssets.ResetId(copy, to);
            SetProfile(prefab, copy);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void Remove(GameObject root, Type type)
        {
            var component = root.GetComponent(type);
            if(component != null) Object.DestroyImmediate(component, true);
        }
    }
}
