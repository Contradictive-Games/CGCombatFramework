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
        public readonly string Description;
        public readonly string Group;
        public readonly bool IsSection;
        public readonly List<Object> Items = new();

        public DatabaseCategory(string key, string title, Type type, string style, string description = null, string group = null, bool isSection = false)
        {
            Key = key;
            Title = title;
            Type = type;
            Style = style;
            Description = description;
            Group = group;
            IsSection = isSection;
        }

        public bool IsAll => Key == DatabaseCatalog.k_All;
        public bool IsMixed => Type == null;
        public bool IsUnits => Type == typeof(Unit);
        public bool CanCreate => Type != null && typeof(ScriptableObject).IsAssignableFrom(Type) && !Type.IsAbstract;
        public Look Look => DatabaseCatalog.LookOf(Type);
    }

    internal sealed class CategoryNode
    {
        public readonly DatabaseCategory Category;
        public readonly List<CategoryNode> Children = new();

        public CategoryNode(DatabaseCategory category) => Category = category;
    }

    internal static class DatabaseCatalog
    {
        public const string k_All = "all";
        public const string k_Combat = "Combat";
        public const string k_Other = "Other";
        public const string k_Definition = "cg-kind-definition";
        private const string k_SectionKey = "section:";
        private const BindingFlags k_Public = BindingFlags.Instance | BindingFlags.Public;

        private static readonly Type[] known =
        {
            typeof(Unit), typeof(FirableDefinition), typeof(StatusEffectDefinition), typeof(DamagePipeline), typeof(UnitStatProfile), typeof(StatType),
            typeof(ResourceType), typeof(DamageType), typeof(StatModifierDefinition), typeof(CombatTag), typeof(Faction), typeof(FirableSlot)
        };

        private static readonly Dictionary<Type, FieldInfo> sprites = new();
        private static readonly Dictionary<Type, FieldInfo> colors = new();
        private static readonly Dictionary<Type, FieldInfo> descriptions = new();

        public static List<DatabaseCategory> Build()
        {
            var categories = Categories(DefinitionTypes());
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

        public static IEnumerable<Type> DefinitionTypes()
        {
            return TypeCache.GetTypesDerivedFrom<GameDefinition>().Where(t => !UnitParts.IsTestAssembly(t.Assembly));
        }

        public static List<DatabaseCategory> Categories(IEnumerable<Type> definitionTypes)
        {
            var categories = new List<DatabaseCategory>();
            var seen = new HashSet<Type>(known);
            foreach(var type in known)
            {
                categories.Add(CategoryFor(type));
            }
            foreach(var type in definitionTypes)
            {
                if(type == null || type.IsAbstract || type.IsGenericType || !typeof(GameDefinition).IsAssignableFrom(type) || !seen.Add(type)) continue;
                categories.Add(CategoryFor(type));
            }
            categories.Sort(CompareCategories);
            categories.Insert(0, new DatabaseCategory(k_All, "Everything", null, k_Definition));
            return categories;
        }

        public static List<CategoryNode> Layout(List<DatabaseCategory> categories, bool grouped, Predicate<DatabaseCategory> keep = null)
        {
            var roots = new List<CategoryNode>();
            var all = categories.FirstOrDefault(c => c.IsAll);
            if(all != null) roots.Add(new CategoryNode(all));
            if(!grouped)
            {
                foreach(var category in categories)
                {
                    if(!category.IsAll && (keep == null || keep(category))) roots.Add(new CategoryNode(category));
                }
                return roots;
            }

            var top = new List<SectionBuilder>();
            var byPath = new Dictionary<string, SectionBuilder>(StringComparer.OrdinalIgnoreCase);
            foreach(var category in categories)
            {
                if(category.IsAll || (keep != null && !keep(category))) continue;
                SectionFor(category.Group ?? k_Other, top, byPath).Categories.Add(category);
            }
            top.Sort(CompareTop);
            foreach(var section in top)
            {
                roots.Add(section.ToNode());
            }
            return roots;
        }

        public static string SectionKey(string path) => k_SectionKey + (path ?? string.Empty).ToLowerInvariant();

        public static string NormalizeGroup(string group)
        {
            if(string.IsNullOrWhiteSpace(group)) return null;
            var segments = group.Split('/').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if(segments.Length == 0) return null;
            if(string.Equals(segments[0], k_Combat, StringComparison.OrdinalIgnoreCase)) segments[0] = k_Combat;
            else if(string.Equals(segments[0], k_Other, StringComparison.OrdinalIgnoreCase)) segments[0] = k_Other;
            return string.Join("/", segments);
        }

        public static string SectionOf(Type type)
        {
            if(type == null) return null;
            return NormalizeGroup(Looks.DefinitionGroup(type)) ?? (IsKnown(type) ? k_Combat : k_Other);
        }

        public static Type KindOf(Object asset)
        {
            if(asset == null) return null;
            return UnitLibrary.IsUnit(asset) ? typeof(Unit) : asset.GetType();
        }

        public static string TitleOf(Type type)
        {
            if(type == null) return null;
            string own = Looks.Own<DefinitionInfoAttribute>(type)?.Title;
            if(!string.IsNullOrWhiteSpace(own)) return own.Trim();
            return Plural(SingularOf(type));
        }

        public static string SingularOf(Type type) => type == null ? null : ObjectNames.NicifyVariableName(Trim(type.Name));

        public static string DescriptionOf(Type type)
        {
            string own = Looks.Own<DefinitionInfoAttribute>(type)?.Description;
            return string.IsNullOrWhiteSpace(own) ? null : own.Trim();
        }

        public static int OrderOf(Type type) => Looks.DefinitionOrder(type);

        public static string StyleOf(Object asset) => StyleOf(KindOf(asset));

        public static string StyleOf(Type type) => BuiltInStyles.Of(type) ?? k_Definition;

        public static Look LookOf(Object asset) => LookOf(KindOf(asset));

        public static Look LookOf(Type type)
        {
            if(type == null || IsKnown(type) || !typeof(GameDefinition).IsAssignableFrom(type)) return default;
            return Looks.OfDefinition(type);
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

        public static string AssetDescriptionOf(Object asset)
        {
            if(asset == null || asset is GameObject) return null;
            var field = DescriptionField(asset.GetType());
            string text = field?.GetValue(asset) as string;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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
            string parts = Count(UnitParts.CountAll(unit), "part");
            return faction != null ? $"{StudioKinds.NameOf(faction)} · {parts}" : parts;
        }

        public static string IdOf(Object asset) => asset is GameDefinition definition ? definition.Id : null;

        private static bool IsKnown(Type type) => type != null && Array.IndexOf(known, type) >= 0;

        private static DatabaseCategory CategoryFor(Type type) => new(type.FullName, TitleOf(type), type, StyleOf(type), DescriptionOf(type), SectionOf(type));

        private static int CompareCategories(DatabaseCategory a, DatabaseCategory b)
        {
            int order = OrderOf(a.Type).CompareTo(OrderOf(b.Type));
            if(order != 0) return order;
            int title = Compare(a.Title, b.Title);
            return title != 0 ? title : string.CompareOrdinal(a.Key, b.Key);
        }

        private static SectionBuilder SectionFor(string path, List<SectionBuilder> top, Dictionary<string, SectionBuilder> byPath)
        {
            if(byPath.TryGetValue(path, out var found)) return found;
            int slash = path.LastIndexOf('/');
            var section = new SectionBuilder(path, slash >= 0 ? path.Substring(slash + 1) : path);
            byPath[path] = section;
            if(slash < 0) top.Add(section);
            else SectionFor(path.Substring(0, slash), top, byPath).Sections.Add(section);
            return section;
        }

        private static int CompareTop(SectionBuilder a, SectionBuilder b)
        {
            int rank = RankOf(a.Path).CompareTo(RankOf(b.Path));
            return rank != 0 ? rank : Compare(a.Title, b.Title);
        }

        private static int RankOf(string path)
        {
            if(string.Equals(path, k_Combat, StringComparison.OrdinalIgnoreCase)) return 0;
            if(string.Equals(path, k_Other, StringComparison.OrdinalIgnoreCase)) return 2;
            return 1;
        }

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

        private static int Compare(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

        private static FieldInfo Find(Type type, Type fieldType, Dictionary<Type, FieldInfo> cache)
        {
            if(cache.TryGetValue(type, out var cached)) return cached;
            var field = type.GetFields(k_Public).FirstOrDefault(f => f.FieldType == fieldType && f.Name == "Icon")
                    ?? type.GetFields(k_Public).FirstOrDefault(f => f.FieldType == fieldType);
            cache[type] = field;
            return field;
        }

        private static FieldInfo DescriptionField(Type type)
        {
            if(descriptions.TryGetValue(type, out var cached)) return cached;
            FieldInfo found = null;
            for(var current = type; current != null && found == null && current != typeof(ScriptableObject); current = current.BaseType)
            {
                foreach(var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if(field.FieldType != typeof(string) || !string.Equals(field.Name, "description", StringComparison.OrdinalIgnoreCase)) continue;
                    if(!field.IsPublic && !field.IsDefined(typeof(SerializeField), false)) continue;
                    found = field;
                    break;
                }
            }
            descriptions[type] = found;
            return found;
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

        private sealed class SectionBuilder
        {
            public readonly string Path;
            public readonly string Title;
            public readonly List<DatabaseCategory> Categories = new();
            public readonly List<SectionBuilder> Sections = new();

            public SectionBuilder(string path, string title)
            {
                Path = path;
                Title = title;
            }

            public CategoryNode ToNode()
            {
                var section = new DatabaseCategory(SectionKey(Path), Title, null, k_Definition, null, Path, true);
                var node = new CategoryNode(section);
                foreach(var category in Categories)
                {
                    node.Children.Add(new CategoryNode(category));
                    section.Items.AddRange(category.Items);
                }
                Sections.Sort((a, b) => Compare(a.Title, b.Title));
                foreach(var child in Sections)
                {
                    var childNode = child.ToNode();
                    node.Children.Add(childNode);
                    section.Items.AddRange(childNode.Category.Items);
                }
                return node;
            }
        }
    }
}
