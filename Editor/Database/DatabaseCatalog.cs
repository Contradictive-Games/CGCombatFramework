using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal sealed class DatabaseCategory
    {
        public readonly string Key;
        public readonly string Title;
        public readonly Type Type;
        public readonly string Style;
        public readonly List<Object> Items = new();

        public DatabaseCategory(string key, string title, Type type, string style)
        {
            Key = key;
            Title = title;
            Type = type;
            Style = style;
        }

        public bool IsAll => Type == null;
        public bool IsUnits => Type == typeof(Unit);
        public bool CanCreate => Type != null && typeof(ScriptableObject).IsAssignableFrom(Type) && !Type.IsAbstract;
    }

    internal static class DatabaseCatalog
    {
        public const string k_All = "all";
        private const BindingFlags k_Public = BindingFlags.Instance | BindingFlags.Public;

        private static readonly (Type type, string title, string style)[] known =
        {
            (typeof(Unit), "Units", "cg-kind-unit"),
            (typeof(FirableDefinition), "Firables", "cg-kind-firable"),
            (typeof(StatusEffectDefinition), "Status Effects", "cg-kind-effect"),
            (typeof(DamagePipeline), "Damage Pipelines", "cg-kind-pipeline"),
            (typeof(UnitStatProfile), "Stat Profiles", "cg-kind-profile"),
            (typeof(StatType), "Stat Types", "cg-kind-stat"),
            (typeof(ResourceType), "Resource Types", "cg-kind-resource"),
            (typeof(DamageType), "Damage Types", "cg-kind-damage"),
            (typeof(StatModifierDefinition), "Stat Modifiers", "cg-kind-modifier"),
            (typeof(CombatTag), "Tags", "cg-kind-tag"),
            (typeof(Faction), "Factions", "cg-kind-faction"),
            (typeof(FirableSlot), "Firable Slots", "cg-kind-slot")
        };

        private static readonly Dictionary<Type, FieldInfo> sprites = new();
        private static readonly Dictionary<Type, FieldInfo> colors = new();

        public static List<DatabaseCategory> Build()
        {
            var categories = new List<DatabaseCategory> { new DatabaseCategory(k_All, "Everything", null, "cg-kind-definition") };
            foreach(var (type, title, style) in known)
            {
                categories.Add(new DatabaseCategory(type.FullName, title, type, style));
            }
            foreach(var type in TypeCache.GetTypesDerivedFrom<GameDefinition>().Where(t => !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.Name))
            {
                if(categories.Any(c => c.Type == type)) continue;
                categories.Add(new DatabaseCategory(type.FullName, Plural(ObjectNames.NicifyVariableName(Trim(type.Name))), type, "cg-kind-definition"));
            }

            var byType = categories.Where(c => c.Type != null).ToDictionary(c => c.Type);
            foreach(var guid in AssetDatabase.FindAssets("t:GameDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(asset != null && byType.TryGetValue(asset.GetType(), out var category)) category.Items.Add(asset);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:DamagePipeline"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<DamagePipeline>(AssetDatabase.GUIDToAssetPath(guid));
                if(asset != null) byType[typeof(DamagePipeline)].Items.Add(asset);
            }
            byType[typeof(Unit)].Items.AddRange(UnitLibrary.All.Where(u => u != null));

            var all = categories[0];
            foreach(var category in categories.Skip(1))
            {
                all.Items.AddRange(category.Items);
            }
            return categories;
        }

        public static DatabaseCategory CategoryOf(List<DatabaseCategory> categories, Object asset)
        {
            if(asset == null) return null;
            var type = UnitLibrary.IsUnit(asset) ? typeof(Unit) : asset.GetType();
            return categories.FirstOrDefault(c => c.Type == type);
        }

        public static string StyleOf(Object asset)
        {
            if(asset == null) return "cg-kind-definition";
            var type = UnitLibrary.IsUnit(asset) ? typeof(Unit) : asset.GetType();
            foreach(var (each, _, style) in known)
            {
                if(each == type) return style;
            }
            return "cg-kind-definition";
        }

        public static Sprite SpriteOf(Object asset)
        {
            if(asset == null || asset is GameObject) return null;
            var field = Find(asset.GetType(), typeof(Sprite), sprites);
            return field?.GetValue(asset) as Sprite;
        }

        public static bool HasSpriteField(Object asset) => asset != null && !(asset is GameObject) && Find(asset.GetType(), typeof(Sprite), sprites) != null;

        public static Color? ColorOf(Object asset)
        {
            if(asset == null || asset is GameObject) return null;
            var field = Find(asset.GetType(), typeof(Color), colors);
            return field != null ? (Color)field.GetValue(asset) : (Color?)null;
        }

        public static string InfoOf(Object asset)
        {
            switch(asset)
            {
                case GameObject unit when UnitLibrary.IsUnit(unit):
                    return UnitInfo(unit);
                case FirableDefinition firable:
                    return $"{BlockTypes.DisplayName(firable.Mode?.GetType())} · {Count(firable.Gates?.Length ?? 0, "gate")} · {Count(firable.Emissions?.Length ?? 0, "emission")}";
                case StatusEffectDefinition effect:
                    string duration = effect.IsPermanent ? "Permanent" : $"{effect.Duration:0.##}s";
                    return effect.MaxStacks > 1 ? $"{duration} · up to {effect.MaxStacks} stacks" : duration;
                case DamagePipeline pipeline:
                    return Count(pipeline.Steps?.Length ?? 0, "step");
                case UnitStatProfile profile:
                    return $"{Count(profile.Stats?.Length ?? 0, "stat")} · {Count(profile.Resources?.Length ?? 0, "resource")}";
                case StatType stat:
                    return stat.ClampValue ? $"Default {stat.DefaultValue:0.##} · {stat.MinValue:0.##} to {stat.MaxValue:0.##}" : $"Default {stat.DefaultValue:0.##}";
                case ResourceType resource:
                    return resource.MaxStat != null ? $"Max from {StudioKinds.NameOf(resource.MaxStat)}" : "Fixed max";
                case DamageType damage:
                    return damage.ResistanceStat != null ? $"Resisted by {StudioKinds.NameOf(damage.ResistanceStat)}" : "No resistance stat";
                case StatModifierDefinition modifier:
                    return $"{Signed(modifier.Value, modifier.Type)} {(modifier.TargetStat != null ? StudioKinds.NameOf(modifier.TargetStat) : "no stat")}";
                case CombatTag tag:
                    return tag.Parent != null ? $"Under {StudioKinds.NameOf(tag.Parent)}" : "Top level";
                case Faction faction:
                    return $"Hostile to {faction.HostileTo?.Length ?? 0} · Friendly to {faction.FriendlyTo?.Length ?? 0}";
                case null:
                    return string.Empty;
                default:
                    return ObjectNames.NicifyVariableName(asset.GetType().Name);
            }
        }

        private static string UnitInfo(GameObject unit)
        {
            var faction = UnitLibrary.FactionOf(unit);
            string parts = Count(UnitParts.Count(UnitParts.Present(unit)), "part");
            return faction != null ? $"{StudioKinds.NameOf(faction)} · {parts}" : parts;
        }

        public static string IdOf(Object asset) => asset is GameDefinition definition ? definition.Id : null;

        private static string Signed(float value, StatModifierType type)
        {
            switch(type)
            {
                case StatModifierType.PercentAdd: return $"{(value >= 0f ? "+" : "")}{value * 100f:0.##}%";
                case StatModifierType.PercentMult: return $"x{1f + value:0.##}";
                default: return $"{(value >= 0f ? "+" : "")}{value:0.##}";
            }
        }

        private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

        private static FieldInfo Find(Type type, Type fieldType, Dictionary<Type, FieldInfo> cache)
        {
            if(cache.TryGetValue(type, out var cached)) return cached;
            var field = type.GetFields(k_Public).FirstOrDefault(f => f.FieldType == fieldType && f.Name == "Icon")
                     ?? type.GetFields(k_Public).FirstOrDefault(f => f.FieldType == fieldType);
            cache[type] = field;
            return field;
        }

        private static string Trim(string name)
        {
            return name.EndsWith("Definition", StringComparison.Ordinal) && name.Length > 10 ? name.Substring(0, name.Length - 10) : name;
        }

        private static string Plural(string title)
        {
            if(title.EndsWith("s", StringComparison.Ordinal) || title.EndsWith("x", StringComparison.Ordinal)) return title + "es";
            if(title.EndsWith("y", StringComparison.Ordinal) && title.Length > 1 && "aeiou".IndexOf(title[title.Length - 2]) < 0) return title.Substring(0, title.Length - 1) + "ies";
            return title + "s";
        }
    }
}
