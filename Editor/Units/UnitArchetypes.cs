using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal sealed class UnitArchetype
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string Style;
        private readonly Action<UnitBlueprint> fill;

        public UnitArchetype(string name, string description, string style, Action<UnitBlueprint> fill)
        {
            Name = name;
            Description = description;
            Style = style;
            this.fill = fill;
        }

        public void Apply(UnitBlueprint blueprint)
        {
            bool rename = string.IsNullOrWhiteSpace(blueprint.FileName) || blueprint.FileName == "New Unit" || UnitArchetypes.All.Any(each => each.Name == blueprint.FileName);
            blueprint.ClearContent();
            blueprint.Archetype = Name;
            if(rename) blueprint.FileName = Name;
            fill(blueprint);
        }
    }

    internal static class UnitArchetypes
    {
        private const UnitPart k_Core = UnitPart.Stats | UnitPart.Resources | UnitPart.Health;

        public static readonly UnitArchetype[] All =
        {
            new UnitArchetype("Player", "A hero you control. Health that recovers after a few seconds out of combat, status effects, two loadout slots, mouse aim and knockback.", "cg-kind-unit", b =>
            {
                b.Parts = k_Core | UnitPart.StatusEffects | UnitPart.Loadout | UnitPart.ViewBinder | UnitPart.Knockback | UnitPart.GroundAim;
                b.Stats.AddRange(new[] { Stat("Max Health", 100f), Stat("Armor", 0f), Stat("Crit Chance", 0.05f), Stat("Crit Damage", 1.5f) });
                b.Resources.Add(Pool("Health", "Max Health", 100f, RegenPreset.PerSecond, 5f, 4f));
                b.Loadout.AddRange(new[] { Slot("Primary"), Slot("Secondary") });
                b.Hitboxes.Add(Hitbox("Head", 2f, 1.6f));
                Health(b, PipelineMode.NewFromTemplate);
            }),

            new UnitArchetype("Enemy", "A standard foe with armor, status effects, one loadout slot, transform aim and knockback.", "cg-kind-damage", b =>
            {
                b.Parts = k_Core | UnitPart.StatusEffects | UnitPart.Loadout | UnitPart.ViewBinder | UnitPart.Knockback | UnitPart.TransformAim;
                b.Stats.AddRange(new[] { Stat("Max Health", 50f), Stat("Armor", 10f) });
                b.Resources.Add(Pool("Health", "Max Health", 50f));
                b.Loadout.Add(Slot("Primary"));
                b.Hitboxes.Add(Hitbox("Head", 1.5f, 1.6f));
                b.SpawnOnStart = true;
                Health(b, PipelineMode.NewFromTemplate);
            }),

            new UnitArchetype("Boss", "A big fight. Lots of health behind a recharging shield, heavy armor, two loadout slots and a weak point.", "cg-kind-faction", b =>
            {
                b.Parts = k_Core | UnitPart.StatusEffects | UnitPart.Loadout | UnitPart.ViewBinder | UnitPart.TransformAim;
                b.Stats.AddRange(new[] { Stat("Max Health", 2000f), Stat("Max Shield", 500f), Stat("Armor", 50f) });
                b.Resources.Add(Pool("Health", "Max Health", 2000f));
                b.Resources.Add(Pool("Shield", "Max Shield", 500f, RegenPreset.PerSecond, 50f, 6f));
                b.Loadout.AddRange(new[] { Slot("Primary"), Slot("Special") });
                b.Hitboxes.Add(Hitbox("Weak Point", 2f, 1.2f));
                Health(b, PipelineMode.NewFromTemplate);
            }),

            new UnitArchetype("Destructible", "Crates, barrels and doors. Health and nothing else, with no damage mitigation.", "cg-kind-slot", b =>
            {
                b.Parts = k_Core | UnitPart.ViewBinder;
                b.Stats.Add(Stat("Max Health", 30f));
                b.Resources.Add(Pool("Health", "Max Health", 30f));
                b.Body = BodyShape.Cube;
                Health(b, PipelineMode.None);
            }),

            new UnitArchetype("Summon", "Turrets and pets. Spawned by another unit so its kills credit the owner. Health, effects, one firable and aim.", "cg-kind-firable", b =>
            {
                b.Parts = k_Core | UnitPart.StatusEffects | UnitPart.Loadout | UnitPart.ViewBinder | UnitPart.TransformAim;
                b.Stats.Add(Stat("Max Health", 40f));
                b.Resources.Add(Pool("Health", "Max Health", 40f));
                b.Loadout.Add(Slot("Primary"));
                b.Body = BodyShape.Cylinder;
                b.SpawnOnStart = false;
                Health(b, PipelineMode.Existing);
            }),

            new UnitArchetype("Training Dummy", "Soaks damage and heals back to full two seconds after the last hit. Pairs well with the Pipeline Tester.", "cg-kind-stat", b =>
            {
                b.Parts = k_Core | UnitPart.StatusEffects | UnitPart.ViewBinder;
                b.Stats.AddRange(new[] { Stat("Max Health", 10000f), Stat("Armor", 0f) });
                b.Resources.Add(Pool("Health", "Max Health", 10000f, RegenPreset.PercentOfMax, 100f, 2f));
                b.Hitboxes.Add(Hitbox("Head", 2f, 1.6f));
                Health(b, PipelineMode.Existing);
            }),

            new UnitArchetype("Blank", "Only the Unit component. Pick the parts yourself.", "cg-kind-definition", b =>
            {
                b.Body = BodyShape.Capsule;
            })
        };

        public static UnitArchetype Find(string name) => All.FirstOrDefault(each => each.Name == name);

        public static void CopyFrom(GameObject prefab, UnitBlueprint b)
        {
            if(prefab == null) return;
            string file = b.FileName;
            b.ClearContent();
            b.Archetype = null;
            b.FileName = string.IsNullOrWhiteSpace(file) || file == "New Unit" ? prefab.name + " Variant" : file;
            var unit = prefab.GetComponent<Unit>();
            b.Faction = UnitLibrary.FactionOf(prefab);
            b.Tags = UnitLibrary.TagsOf(prefab);
            b.Parts = UnitParts.Present(prefab);
            b.SpawnOnStart = unit == null || ReadBool(unit, "spawnOnStart", true);

            var profile = UnitLibrary.ProfileOf(prefab);
            b.Profile = profile != null ? ProfileMode.ExistingProfile : ProfileMode.NewProfile;
            b.ExistingProfile = profile;

            var health = prefab.GetComponent<HealthComponent>();
            var healthType = UnitLibrary.HealthOf(prefab);
            b.HealthResource = healthType != null ? StudioKinds.NameOf(healthType) : string.Empty;
            var pipeline = UnitLibrary.PipelineOf(prefab);
            b.Pipeline = pipeline != null ? PipelineMode.Existing : PipelineMode.None;
            b.ExistingPipeline = pipeline;
            b.DeadTag = UnitLibrary.Read<CombatTag>(health, "deadTag");
            b.InnateEffects = UnitLibrary.ReadArray<StatusEffectDefinition>(prefab.GetComponent<StatusEffectsComponent>(), "innateEffects");

            var loadout = prefab.GetComponent<FirableLoadout>();
            if(loadout != null)
            {
                using(var serialized = new UnityEditor.SerializedObject(loadout))
                {
                    var entries = serialized.FindProperty("startingLoadout");
                    for(int i = 0; entries != null && i < entries.arraySize; i++)
                    {
                        var entry = entries.GetArrayElementAtIndex(i);
                        b.Loadout.Add(new LoadoutSpec
                        {
                            Slot = entry.FindPropertyRelative("Slot").objectReferenceValue as FirableSlot,
                            Firable = entry.FindPropertyRelative("Firable").objectReferenceValue as FirableDefinition
                        });
                    }
                }
            }

            foreach(var hitbox in prefab.GetComponentsInChildren<Hitbox>(true))
            {
                b.Hitboxes.Add(new HitboxSpec
                {
                    Name = hitbox.name,
                    Multiplier = hitbox.DamageMultiplier,
                    Zone = hitbox.Zone,
                    Height = hitbox.transform.localPosition.y,
                    Radius = hitbox.TryGetComponent<SphereCollider>(out var sphere) ? sphere.radius : 0.25f
                });
            }
        }

        private static bool ReadBool(Component component, string field, bool fallback)
        {
            using(var serialized = new UnityEditor.SerializedObject(component))
            {
                var property = serialized.FindProperty(field);
                return property != null ? property.boolValue : fallback;
            }
        }

        private static void Health(UnitBlueprint b, PipelineMode pipeline)
        {
            b.HealthResource = "Health";
            b.Pipeline = pipeline;
            if(pipeline == PipelineMode.Existing)
            {
                b.ExistingPipeline = FirstPipeline();
                if(b.ExistingPipeline == null) b.Pipeline = PipelineMode.NewFromTemplate;
            }
        }

        private static DamagePipeline FirstPipeline()
        {
            foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:DamagePipeline"))
            {
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<DamagePipeline>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if(found != null) return found;
            }
            return null;
        }

        private static StatSpec Stat(string name, float value)
        {
            return new StatSpec { Type = DefinitionAssets.Find<StatType>(name), Name = name, Value = value };
        }

        private static ResourceSpec Pool(string name, string maxStat, float max, RegenPreset regen = RegenPreset.None, float rate = 0f, float delay = 0f)
        {
            return new ResourceSpec { Type = DefinitionAssets.Find<ResourceType>(name), Name = name, MaxStat = maxStat, Max = max, Regen = regen, Rate = rate, Delay = delay };
        }

        private static LoadoutSpec Slot(string name)
        {
            return new LoadoutSpec { Slot = DefinitionAssets.Find<FirableSlot>(name), SlotName = name };
        }

        private static HitboxSpec Hitbox(string name, float multiplier, float height)
        {
            return new HitboxSpec { Name = name, Multiplier = multiplier, Height = height, Zone = DefinitionAssets.Find<CombatTag>(name) };
        }

        public static List<string> Contents(UnitArchetype archetype)
        {
            var blueprint = new UnitBlueprint();
            archetype.Apply(blueprint);
            var lines = new List<string>();
            foreach(var info in UnitParts.All)
            {
                if(blueprint.Has(info.Part)) lines.Add(info.Label);
            }
            if(blueprint.Stats.Count > 0) lines.Add(string.Join(", ", blueprint.Stats.Select(s => $"{s.Label} {s.Value:0.##}")));
            if(blueprint.Loadout.Count > 0) lines.Add($"Slots: {string.Join(", ", blueprint.Loadout.Select(s => s.Label))}");
            return lines;
        }
    }
}
