using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal readonly struct PlannedFile
    {
        public readonly string Kind;
        public readonly string Label;
        public readonly string Path;
        public readonly string Style;
        public readonly bool Created;

        public PlannedFile(string kind, string label, string path, string style, bool created)
        {
            Kind = kind;
            Label = label;
            Path = path;
            Style = style;
            Created = created;
        }
    }

    internal static class UnitBuilder
    {
        private static readonly (Type step, string field, string[] names, Type asset)[] wiring =
        {
            (typeof(ArmorStep), "armorStat", new[] { "Armor" }, typeof(StatType)),
            (typeof(CritStrikeStep), "critChanceStat", new[] { "Crit Chance", "Critical Chance" }, typeof(StatType)),
            (typeof(CritStrikeStep), "critDamageStat", new[] { "Crit Damage", "Critical Damage" }, typeof(StatType)),
            (typeof(EvadeStep), "evadeChance", new[] { "Evade Chance", "Evade", "Dodge Chance" }, typeof(StatType)),
            (typeof(FlatReductionStep), "reduction", new[] { "Toughness", "Flat Reduction" }, typeof(StatType)),
            (typeof(HitboxMultiplierStep), "weakPointBonus", new[] { "Weak Point Bonus" }, typeof(StatType)),
            (typeof(ShieldAbsorbStep), "shield", new[] { "Shield" }, typeof(ResourceType)),
            (typeof(ImmuneWithTagStep), "tag", new[] { "Invulnerable" }, typeof(CombatTag))
        };

        public static string FolderFor(UnitBlueprint b)
        {
            string folder = string.IsNullOrWhiteSpace(b.Folder) ? "Assets" : b.Folder.Trim().TrimEnd('/');
            return b.OwnFolder ? $"{folder}/{DefinitionAssets.Safe(b.FileName)}" : folder;
        }

        public static string TypesFolderFor(UnitBlueprint b)
        {
            return string.IsNullOrWhiteSpace(b.TypesFolder) ? FolderFor(b) : b.TypesFolder.Trim().TrimEnd('/');
        }

        public static string PrefabPath(UnitBlueprint b) => $"{FolderFor(b)}/{DefinitionAssets.Safe(b.FileName)}.prefab";

        public static bool NeedsProfile(UnitBlueprint b)
        {
            return b.Profile == ProfileMode.NewProfile && (b.Has(UnitPart.Stats) || b.Has(UnitPart.Resources)) && (b.Stats.Count > 0 || b.Resources.Count > 0);
        }

        public static List<PlannedFile> Plan(UnitBlueprint b)
        {
            var files = new List<PlannedFile>();
            string folder = FolderFor(b);
            string types = TypesFolderFor(b);
            string name = DefinitionAssets.Safe(b.FileName);
            files.Add(new PlannedFile("Prefab", b.FileName, PrefabPath(b), "cg-kind-unit", true));

            if(NeedsProfile(b)) files.Add(new PlannedFile("Stat Profile", $"{name} Profile", $"{folder}/{name} Profile.asset", "cg-kind-profile", true));
            else if(b.Profile == ProfileMode.ExistingProfile && b.ExistingProfile != null) files.Add(new PlannedFile("Stat Profile", StudioKinds.NameOf(b.ExistingProfile), AssetDatabase.GetAssetPath(b.ExistingProfile), "cg-kind-profile", false));

            if(b.Has(UnitPart.Health))
            {
                if(b.Pipeline == PipelineMode.NewFromTemplate) files.Add(new PlannedFile("Damage Pipeline", $"{name} Pipeline", $"{folder}/{name} Pipeline.asset", "cg-kind-pipeline", true));
                else if(b.Pipeline == PipelineMode.Existing && b.ExistingPipeline != null) files.Add(new PlannedFile("Damage Pipeline", StudioKinds.NameOf(b.ExistingPipeline), AssetDatabase.GetAssetPath(b.ExistingPipeline), "cg-kind-pipeline", false));
            }

            var seen = new HashSet<string>();
            if(b.Profile == ProfileMode.NewProfile)
            {
                foreach(var stat in NewStatNames(b))
                {
                    if(seen.Add("stat:" + DefinitionAssets.Key(stat))) files.Add(new PlannedFile("Stat Type", stat, $"{types}/{DefinitionAssets.Safe(stat)}.asset", "cg-kind-stat", true));
                }
                foreach(var spec in b.Resources.Where(r => r.IsNew))
                {
                    if(seen.Add("resource:" + DefinitionAssets.Key(spec.Name))) files.Add(new PlannedFile("Resource Type", spec.Name, $"{types}/{DefinitionAssets.Safe(spec.Name)}.asset", "cg-kind-resource", true));
                }
            }
            if(b.Has(UnitPart.Loadout))
            {
                foreach(var spec in b.Loadout.Where(l => l.IsNew))
                {
                    if(seen.Add("slot:" + DefinitionAssets.Key(spec.SlotName))) files.Add(new PlannedFile("Firable Slot", spec.SlotName, $"{types}/{DefinitionAssets.Safe(spec.SlotName)}.asset", "cg-kind-slot", true));
                }
            }
            return files;
        }

        public static List<string> Errors(UnitBlueprint b)
        {
            var errors = new List<string>();
            if(string.IsNullOrWhiteSpace(b.FileName)) errors.Add("Give the unit a file name");
            else if(AssetDatabase.LoadAssetAtPath<Object>(PrefabPath(b)) != null) errors.Add($"{PrefabPath(b)} already exists");
            if(!(b.Folder ?? string.Empty).StartsWith("Assets")) errors.Add("The folder has to be inside Assets");
            if(b.Has(UnitPart.Health))
            {
                if(string.IsNullOrWhiteSpace(b.HealthResource)) errors.Add("Health needs a resource to use as its health pool");
                else if(!b.ResourceLabels().Any(label => DefinitionAssets.Key(label) == DefinitionAssets.Key(b.HealthResource))) errors.Add($"There's no '{b.HealthResource}' resource for Health to use");
                if(b.Pipeline == PipelineMode.Existing && b.ExistingPipeline == null) errors.Add("Pick a damage pipeline, or choose None or New From Template");
            }
            if(b.Profile == ProfileMode.ExistingProfile && b.ExistingProfile == null && (b.Has(UnitPart.Stats) || b.Has(UnitPart.Resources))) errors.Add("Pick a stat profile, or make a new one");
            if(b.Body == BodyShape.Model && b.Model == null) errors.Add("Pick a model, or choose another body");
            return errors;
        }

        public static List<string> Warnings(UnitBlueprint b)
        {
            var warnings = new List<string>();
            if(!b.Collider && b.Body != BodyShape.Model && b.Hitboxes.Count == 0) warnings.Add("Without a collider or hitboxes, queries and projectiles can't hit this unit");
            if(b.Collider && b.Hitboxes.Count > 0 && b.Body != BodyShape.None) warnings.Add("Hitboxes sit inside the body collider. Put the body collider on a layer your queries skip so the hitboxes get hit");
            if(b.Has(UnitPart.Health) && b.Pipeline == PipelineMode.None) warnings.Add("No damage pipeline, so damage is applied as-is");
            if(b.Stats.Any(s => s.Type == null && string.IsNullOrWhiteSpace(s.Name))) warnings.Add("A stat row has no type or name and will be skipped");
            if(b.Has(UnitPart.Loadout) && b.Loadout.Any(l => l.Slot == null && string.IsNullOrWhiteSpace(l.SlotName))) warnings.Add("A loadout row has no slot and will be skipped");
            return warnings;
        }

        public static GameObject Build(UnitBlueprint b)
        {
            string folder = FolderFor(b);
            string types = TypesFolderFor(b);
            string name = DefinitionAssets.Safe(b.FileName);
            var made = new Dictionary<string, GameDefinition>();
            DefinitionAssets.EnsureFolder(folder);

            var profile = b.Profile == ProfileMode.ExistingProfile ? b.ExistingProfile : null;
            if(NeedsProfile(b)) profile = BuildProfile(b, folder, types, name, made);

            DamagePipeline pipeline = null;
            if(b.Has(UnitPart.Health))
            {
                if(b.Pipeline == PipelineMode.Existing) pipeline = b.ExistingPipeline;
                else if(b.Pipeline == PipelineMode.NewFromTemplate) pipeline = BuildPipeline(b, folder, name, profile);
            }

            var root = new GameObject(name);
            try
            {
                BuildBody(b, root);
                var unit = root.AddComponent<Unit>();
                Configure(unit, serialized =>
                {
                    serialized.FindProperty("displayName").stringValue = b.DisplayName ?? string.Empty;
                    serialized.FindProperty("faction").objectReferenceValue = b.Faction;
                    serialized.FindProperty("spawnOnStart").boolValue = b.SpawnOnStart;
                    Fill(serialized.FindProperty("startingTags"), b.Tags);
                });

                if(b.Has(UnitPart.Stats)) Configure(root.AddComponent<StatsComponent>(), s => s.FindProperty("profile").objectReferenceValue = profile);
                if(b.Has(UnitPart.Resources)) Configure(root.AddComponent<ResourceComponent>(), s => s.FindProperty("profile").objectReferenceValue = profile);
                if(b.Has(UnitPart.Health))
                {
                    var healthType = ResolveHealth(b, profile);
                    Configure(root.AddComponent<HealthComponent>(), s =>
                    {
                        s.FindProperty("healthType").objectReferenceValue = healthType;
                        s.FindProperty("pipeline").objectReferenceValue = pipeline;
                        s.FindProperty("deadTag").objectReferenceValue = b.DeadTag;
                    });
                }
                if(b.Has(UnitPart.StatusEffects)) Configure(root.AddComponent<StatusEffectsComponent>(), s => Fill(s.FindProperty("innateEffects"), b.InnateEffects));
                if(b.Has(UnitPart.Loadout)) Configure(root.AddComponent<FirableLoadout>(), s => FillLoadout(s.FindProperty("startingLoadout"), b, types, made));
                if(b.Has(UnitPart.ViewBinder)) root.AddComponent<UnitViewBinder>();
                if(b.Has(UnitPart.Knockback))
                {
                    var body = root.AddComponent<Rigidbody>();
                    body.constraints = RigidbodyConstraints.FreezeRotation;
                    root.AddComponent<RigidbodyKnockback>();
                }
                if(b.Has(UnitPart.TransformAim)) root.AddComponent<TransformAimProvider>();
                else if(b.Has(UnitPart.GroundAim)) root.AddComponent<GroundPlaneAimProvider>();

                foreach(var spec in b.Hitboxes)
                {
                    BuildHitbox(spec, root, unit);
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GenerateUniqueAssetPath(PrefabPath(b)));
                AssetDatabase.SaveAssets();
                if(b.PlaceInScene && prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Place Unit");
                    Selection.activeGameObject = instance;
                }
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static IEnumerable<string> NewStatNames(UnitBlueprint b)
        {
            foreach(var spec in b.Stats.Where(s => s.IsNew))
            {
                yield return spec.Name;
            }
            foreach(var spec in b.Resources.Where(r => r.IsNew && !string.IsNullOrWhiteSpace(r.MaxStat)))
            {
                bool listed = b.Stats.Any(s => DefinitionAssets.Key(s.Label) == DefinitionAssets.Key(spec.MaxStat));
                if(!listed && DefinitionAssets.Find<StatType>(spec.MaxStat) == null) yield return spec.MaxStat;
            }
        }

        private static UnitStatProfile BuildProfile(UnitBlueprint b, string folder, string types, string name, Dictionary<string, GameDefinition> made)
        {
            var stats = new List<StatEntry>();
            foreach(var spec in b.Stats)
            {
                var type = spec.Type != null ? spec.Type : Stat(spec.Name, b, types, made);
                if(type == null || stats.Any(e => e.Type == type)) continue;
                stats.Add(new StatEntry { Type = type, BaseValue = spec.Value });
            }

            var resources = new List<ResourceEntry>();
            foreach(var spec in b.Resources)
            {
                var type = spec.Type != null ? spec.Type : Resource(spec, b, types, made);
                if(type == null || resources.Any(e => e.Type == type)) continue;
                resources.Add(new ResourceEntry { Type = type, BaseMax = spec.Max, Start = spec.Start, RegenRules = Regen(spec) });
                if(type.MaxStat != null && stats.All(e => e.Type != type.MaxStat)) stats.Add(new StatEntry { Type = type.MaxStat, BaseValue = spec.Max });
            }

            var profile = DefinitionAssets.Create<UnitStatProfile>(folder, $"{name} Profile");
            profile.Stats = stats.ToArray();
            profile.Resources = resources.ToArray();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static StatType Stat(string label, UnitBlueprint b, string types, Dictionary<string, GameDefinition> made)
        {
            if(string.IsNullOrWhiteSpace(label)) return null;
            string key = "stat:" + DefinitionAssets.Key(label);
            if(made.TryGetValue(key, out var cached)) return (StatType)cached;
            var listed = b.Stats.FirstOrDefault(s => s.Type != null && DefinitionAssets.Key(s.Label) == DefinitionAssets.Key(label));
            var type = listed != null ? listed.Type : DefinitionAssets.Find<StatType>(label);
            if(type == null) type = DefinitionAssets.Create<StatType>(types, label);
            made[key] = type;
            return type;
        }

        private static ResourceType Resource(ResourceSpec spec, UnitBlueprint b, string types, Dictionary<string, GameDefinition> made)
        {
            if(string.IsNullOrWhiteSpace(spec.Name)) return null;
            string key = "resource:" + DefinitionAssets.Key(spec.Name);
            if(made.TryGetValue(key, out var cached)) return (ResourceType)cached;
            var type = DefinitionAssets.Find<ResourceType>(spec.Name);
            if(type == null)
            {
                type = DefinitionAssets.Create<ResourceType>(types, spec.Name);
                type.MaxStat = Stat(spec.MaxStat, b, types, made);
                if(DefinitionAssets.Key(spec.Name).Contains("shield")) type.Color = new Color(0.3f, 0.6f, 1f);
                EditorUtility.SetDirty(type);
            }
            made[key] = type;
            return type;
        }

        private static IRegenRule[] Regen(ResourceSpec spec)
        {
            IRegenRule rule;
            switch(spec.Regen)
            {
                case RegenPreset.PerSecond:
                    rule = StudioTemplates.Make<FlatRegen>(("perSecond", spec.Rate));
                    break;
                case RegenPreset.PercentOfMax:
                    rule = StudioTemplates.Make<PercentOfMaxRegen>(("percentPerSecond", Mathf.Clamp(spec.Rate, 0f, 100f)));
                    break;
                default:
                    return Array.Empty<IRegenRule>();
            }
            if(spec.Delay > 0f) rule = StudioTemplates.Make<DelayAfterLoss>(("delay", spec.Delay), ("then", rule));
            return new[] { rule };
        }

        private static DamagePipeline BuildPipeline(UnitBlueprint b, string folder, string name, UnitStatProfile profile)
        {
            var templates = StudioTemplates.Pipelines;
            var pipeline = DefinitionAssets.Create<DamagePipeline>(folder, $"{name} Pipeline");
            if(templates.Length > 0) templates[Mathf.Clamp(b.PipelineTemplate, 0, templates.Length - 1)].Build(pipeline);
            if(b.WirePipeline) Wire(pipeline, profile);
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        private static void Wire(DamagePipeline pipeline, UnitStatProfile profile)
        {
            foreach(var step in pipeline.Steps ?? Array.Empty<IDamageStep>())
            {
                if(step == null) continue;
                foreach(var (type, field, names, asset) in wiring)
                {
                    if(type != step.GetType()) continue;
                    var value = Lookup(asset, names, profile);
                    if(value != null) StudioTemplates.Set(step, field, value);
                }
            }
        }

        private static Object Lookup(Type asset, string[] names, UnitStatProfile profile)
        {
            foreach(var name in names)
            {
                string key = DefinitionAssets.Key(name);
                if(profile != null && asset == typeof(StatType))
                {
                    foreach(var entry in profile.Stats ?? Array.Empty<StatEntry>())
                    {
                        if(entry.Type != null && DefinitionAssets.Key(StudioKinds.NameOf(entry.Type)) == key) return entry.Type;
                    }
                }
                if(profile != null && asset == typeof(ResourceType))
                {
                    foreach(var entry in profile.Resources ?? Array.Empty<ResourceEntry>())
                    {
                        if(entry?.Type != null && DefinitionAssets.Key(StudioKinds.NameOf(entry.Type)) == key) return entry.Type;
                    }
                }
                Object found = null;
                if(asset == typeof(StatType)) found = DefinitionAssets.Find<StatType>(name);
                else if(asset == typeof(ResourceType)) found = DefinitionAssets.Find<ResourceType>(name);
                else if(asset == typeof(CombatTag)) found = DefinitionAssets.Find<CombatTag>(name);
                if(found != null) return found;
            }
            return null;
        }

        private static ResourceType ResolveHealth(UnitBlueprint b, UnitStatProfile profile)
        {
            string key = DefinitionAssets.Key(b.HealthResource);
            if(profile != null)
            {
                foreach(var entry in profile.Resources ?? Array.Empty<ResourceEntry>())
                {
                    if(entry?.Type != null && DefinitionAssets.Key(StudioKinds.NameOf(entry.Type)) == key) return entry.Type;
                }
            }
            return DefinitionAssets.Find<ResourceType>(b.HealthResource);
        }

        private static void FillLoadout(SerializedProperty array, UnitBlueprint b, string types, Dictionary<string, GameDefinition> made)
        {
            var entries = new List<(FirableSlot slot, FirableDefinition firable)>();
            foreach(var spec in b.Loadout)
            {
                var slot = spec.Slot;
                if(slot == null && !string.IsNullOrWhiteSpace(spec.SlotName))
                {
                    string key = "slot:" + DefinitionAssets.Key(spec.SlotName);
                    if(made.TryGetValue(key, out var cached)) slot = (FirableSlot)cached;
                    else
                    {
                        slot = DefinitionAssets.Find<FirableSlot>(spec.SlotName) ?? DefinitionAssets.Create<FirableSlot>(types, spec.SlotName);
                        made[key] = slot;
                    }
                }
                if(slot != null) entries.Add((slot, spec.Firable));
            }
            array.arraySize = entries.Count;
            for(int i = 0; i < entries.Count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Slot").objectReferenceValue = entries[i].slot;
                element.FindPropertyRelative("Firable").objectReferenceValue = entries[i].firable;
            }
        }

        private static void BuildBody(UnitBlueprint b, GameObject root)
        {
            PrimitiveType? primitive = null;
            float height = 1f;
            switch(b.Body)
            {
                case BodyShape.Capsule:
                    primitive = PrimitiveType.Capsule;
                    height = 2f;
                    break;
                case BodyShape.Cube:
                    primitive = PrimitiveType.Cube;
                    break;
                case BodyShape.Sphere:
                    primitive = PrimitiveType.Sphere;
                    break;
                case BodyShape.Cylinder:
                    primitive = PrimitiveType.Cylinder;
                    height = 2f;
                    break;
                case BodyShape.Model:
                    if(b.Model != null && PrefabUtility.InstantiatePrefab(b.Model, root.transform) is GameObject model) model.name = "Model";
                    break;
            }

            if(primitive.HasValue)
            {
                var body = GameObject.CreatePrimitive(primitive.Value);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                if(!b.Collider) Object.DestroyImmediate(body.GetComponent<Collider>());
                return;
            }

            if(!b.Collider) return;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.5f;
        }

        private static void BuildHitbox(HitboxSpec spec, GameObject root, Unit unit)
        {
            var child = new GameObject(string.IsNullOrWhiteSpace(spec.Name) ? "Hitbox" : spec.Name);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0f, spec.Height, 0f);
            var sphere = child.AddComponent<SphereCollider>();
            sphere.radius = Mathf.Max(0.01f, spec.Radius);
            var hitbox = child.AddComponent<Hitbox>();
            hitbox.Owner = unit;
            hitbox.DamageMultiplier = spec.Multiplier;
            hitbox.Zone = spec.Zone;
        }

        private static void Configure(Component component, Action<SerializedObject> change)
        {
            using(var serialized = new SerializedObject(component))
            {
                change(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Fill<T>(SerializedProperty array, T[] values) where T : Object
        {
            var list = (values ?? Array.Empty<T>()).Where(v => v != null).ToArray();
            array.arraySize = list.Length;
            for(int i = 0; i < list.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
        }
    }
}
