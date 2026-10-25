using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal enum BodyShape { None, Capsule, Cube, Sphere, Cylinder, Model }
    internal enum ProfileMode { NewProfile, ExistingProfile }
    internal enum PipelineMode { None, Existing, NewFromTemplate }
    internal enum RegenPreset { None, PerSecond, PercentOfMax }

    [Serializable]
    internal sealed class StatSpec
    {
        public StatType Type;
        public string Name;
        public float Value;

        public string Label => Type != null ? StudioKinds.NameOf(Type) : Name;
        public bool IsNew => Type == null && !string.IsNullOrWhiteSpace(Name);
    }

    [Serializable]
    internal sealed class ResourceSpec
    {
        public ResourceType Type;
        public string Name;
        [Tooltip("Stat that sets the max of a new resource type")]
        public string MaxStat;
        [Tooltip("Max when the resource has no max stat, and the max stat's value when it is added for you")]
        public float Max = 100f;
        public ResourceStart Start = ResourceStart.Full;
        public RegenPreset Regen;
        [Tooltip("Per second, or percent of max per second")]
        public float Rate = 5f;
        [Tooltip("Seconds after the last loss before regen starts")]
        public float Delay;

        public string Label => Type != null ? StudioKinds.NameOf(Type) : Name;
        public bool IsNew => Type == null && !string.IsNullOrWhiteSpace(Name);
    }

    [Serializable]
    internal sealed class LoadoutSpec
    {
        public FirableSlot Slot;
        public string SlotName;
        public FirableDefinition Firable;

        public string Label => Slot != null ? StudioKinds.NameOf(Slot) : SlotName;
        public bool IsNew => Slot == null && !string.IsNullOrWhiteSpace(SlotName);
    }

    [Serializable]
    internal sealed class HitboxSpec
    {
        public string Name = "Head";
        public float Multiplier = 2f;
        public CombatTag Zone;
        public float Height = 1.6f;
        public float Radius = 0.25f;
    }

    [Serializable]
    internal sealed class UnitBlueprint
    {
        public string Archetype;
        public string FileName = "New Unit";
        public string DisplayName;
        public Faction Faction;
        public CombatTag[] Tags = Array.Empty<CombatTag>();
        public bool SpawnOnStart = true;
        public BodyShape Body = BodyShape.Capsule;
        public GameObject Model;
        [Tooltip("Gives the body a collider so queries and projectiles can hit it")]
        public bool Collider = true;

        public ProfileMode Profile;
        public UnitStatProfile ExistingProfile;
        public List<StatSpec> Stats = new();
        public List<ResourceSpec> Resources = new();

        public UnitPart Parts = UnitPart.Stats | UnitPart.Resources | UnitPart.Health;
        public string HealthResource = "Health";
        public PipelineMode Pipeline;
        public DamagePipeline ExistingPipeline;
        public int PipelineTemplate;
        [Tooltip("Fill a new pipeline's steps with this unit's matching stats, like Armor and Crit Chance")]
        public bool WirePipeline = true;
        public CombatTag DeadTag;
        public StatusEffectDefinition[] InnateEffects = Array.Empty<StatusEffectDefinition>();
        public List<LoadoutSpec> Loadout = new();
        public List<HitboxSpec> Hitboxes = new();

        public string Folder = "Assets/Units";
        [Tooltip("Where new stat, resource and slot types go")]
        public string TypesFolder = "Assets/Units/Types";
        [Tooltip("Put this unit's files in their own folder")]
        public bool OwnFolder = true;
        public bool PlaceInScene;

        public bool Has(UnitPart part) => (Parts & part) == part;

        public List<string> ResourceLabels()
        {
            var labels = new List<string>();
            if(Profile == ProfileMode.ExistingProfile)
            {
                if(ExistingProfile == null) return labels;
                foreach(var entry in ExistingProfile.Resources ?? Array.Empty<ResourceEntry>())
                {
                    if(entry != null && entry.Type != null) labels.Add(StudioKinds.NameOf(entry.Type));
                }
                return labels;
            }
            foreach(var spec in Resources)
            {
                if(!string.IsNullOrWhiteSpace(spec.Label)) labels.Add(spec.Label);
            }
            return labels;
        }

        public void ClearContent()
        {
            DisplayName = string.Empty;
            Faction = null;
            Tags = Array.Empty<CombatTag>();
            SpawnOnStart = true;
            Body = BodyShape.Capsule;
            Model = null;
            Collider = true;
            Profile = ProfileMode.NewProfile;
            ExistingProfile = null;
            Stats = new List<StatSpec>();
            Resources = new List<ResourceSpec>();
            Parts = UnitPart.None;
            HealthResource = string.Empty;
            Pipeline = PipelineMode.None;
            ExistingPipeline = null;
            PipelineTemplate = 0;
            WirePipeline = true;
            DeadTag = null;
            InnateEffects = Array.Empty<StatusEffectDefinition>();
            Loadout = new List<LoadoutSpec>();
            Hitboxes = new List<HitboxSpec>();
        }
    }

    internal static class SpecFields
    {
        public static VisualElement NamedRow(SerializedProperty property, string asset, string name, string placeholder, params string[] rest)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-inline");
            var assetProperty = property.FindPropertyRelative(asset);
            var assetField = InlineFields.Field(assetProperty, asset);
            row.Add(assetField);
            var nameField = InlineFields.Field(property.FindPropertyRelative(name), name, placeholder);
            row.Add(nameField);
            var badge = CombatEditorStyles.Chip("new", "cg-chip--new");
            badge.tooltip = "Created for you when the unit is made";
            row.Add(badge);
            foreach(var each in rest)
            {
                var child = property.FindPropertyRelative(each);
                if(child != null) row.Add(InlineFields.Field(child, each));
            }

            void Refresh(SerializedProperty changed)
            {
                bool empty = changed.objectReferenceValue == null;
                CombatEditorStyles.Show(nameField, empty);
                CombatEditorStyles.Show(badge, empty);
                assetField.EnableInClassList("cg-inline__field--narrow", empty);
            }

            Refresh(assetProperty);
            row.TrackPropertyValue(assetProperty, Refresh);
            return row;
        }
    }

    [CustomPropertyDrawer(typeof(StatSpec))]
    internal sealed class StatSpecDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return SpecFields.NamedRow(property, "Type", "Name", "New stat name", "Value");
        }
    }

    [CustomPropertyDrawer(typeof(ResourceSpec))]
    internal sealed class ResourceSpecDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList("cg-spec");
            root.Add(SpecFields.NamedRow(property, "Type", "Name", "New resource name", "Max", "Start"));
            var second = new VisualElement();
            second.AddToClassList("cg-inline");
            second.AddToClassList("cg-inline--second");
            var typeProperty = property.FindPropertyRelative("Type");
            var maxStat = InlineFields.Field(property.FindPropertyRelative("MaxStat"), "MaxStat", "Max stat (optional)");
            second.Add(maxStat);
            second.Add(InlineFields.Field(property.FindPropertyRelative("Regen"), "Regen"));
            var rate = InlineFields.Field(property.FindPropertyRelative("Rate"), "Rate");
            var delay = InlineFields.Field(property.FindPropertyRelative("Delay"), "Delay");
            second.Add(rate);
            second.Add(delay);
            root.Add(second);

            var regenProperty = property.FindPropertyRelative("Regen");
            void Refresh(SerializedProperty _)
            {
                CombatEditorStyles.Show(maxStat, typeProperty.objectReferenceValue == null);
                bool regen = regenProperty.enumValueIndex != (int)RegenPreset.None;
                CombatEditorStyles.Show(rate, regen);
                CombatEditorStyles.Show(delay, regen);
            }

            Refresh(null);
            maxStat.TrackPropertyValue(typeProperty, Refresh);
            rate.TrackPropertyValue(regenProperty, Refresh);
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(LoadoutSpec))]
    internal sealed class LoadoutSpecDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return SpecFields.NamedRow(property, "Slot", "SlotName", "New slot name", "Firable");
        }
    }

    [CustomPropertyDrawer(typeof(HitboxSpec))]
    internal sealed class HitboxSpecDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return InlineFields.Row(property, "Name", "Multiplier", "Zone", "Height", "Radius");
        }
    }

    [CustomPropertyDrawer(typeof(StatEntry))]
    internal sealed class StatEntryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => InlineFields.Row(property, "Type", "BaseValue");
    }

    [CustomPropertyDrawer(typeof(FirableLoadout.SlotEntry))]
    internal sealed class SlotEntryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => InlineFields.Row(property, "Slot", "Firable");
    }
}
