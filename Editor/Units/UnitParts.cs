using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace CG.CombatFramework.Editor
{
    [Flags]
    internal enum UnitPart
    {
        None = 0,
        Stats = 1 << 0,
        Resources = 1 << 1,
        Health = 1 << 2,
        StatusEffects = 1 << 3,
        Loadout = 1 << 4,
        ViewBinder = 1 << 5,
        Knockback = 1 << 6,
        TransformAim = 1 << 7,
        GroundAim = 1 << 8,
        DespawnOnDeath = 1 << 9
    }

    internal sealed class PartInfo
    {
        public const string k_CustomStyle = "cg-kind-part";
        public const string k_SequenceStyle = "cg-kind-sequence";

        public readonly UnitPart Part;
        public readonly Type Type;
        public readonly string Label;
        public readonly string Description;
        public readonly string Style;
        public readonly UnitPart Requires;
        public readonly UnitPart Excludes;
        public readonly bool IsCustom;
        public readonly string Group;
        public readonly Type[] Needs;
        public readonly bool InChildren;

        public PartInfo(UnitPart part, Type type, string label, string description, string style, UnitPart requires = UnitPart.None, UnitPart excludes = UnitPart.None)
        {
            Part = part;
            Type = type;
            Label = label;
            Description = description;
            Style = style;
            Requires = requires;
            Excludes = excludes;
            Needs = Array.Empty<Type>();
        }

        public PartInfo(Type type, string label, string description, string group, Type[] needs, bool inChildren)
        {
            Part = UnitPart.None;
            Type = type;
            Label = label;
            Description = description;
            Style = inChildren ? k_SequenceStyle : k_CustomStyle;
            IsCustom = true;
            Group = group;
            Needs = needs ?? Array.Empty<Type>();
            InChildren = inChildren;
        }

        public Look Look => IsCustom ? Looks.OfPart(Type) : default;
    }

    internal static class UnitParts
    {
        public const string k_Sequences = "Sequences";

        private static readonly string[] testReferences = { "nunit.framework", "UnityEngine.TestRunner", "UnityEditor.TestRunner" };
        private static readonly Dictionary<Assembly, bool> testAssemblies = new();
        private static IReadOnlyList<PartInfo> custom;
        private static PartInfo[] every;

        public static readonly PartInfo[] All =
        {
            new PartInfo(UnitPart.Stats, typeof(StatsComponent), "Stats", "Base stat values from a stat profile. Damage steps, scalers and costs read these.", "cg-kind-stat"),
            new PartInfo(UnitPart.Resources, typeof(ResourceComponent), "Resources", "Health, shields, mana and other pools from the profile. A pool's max can follow a stat.", "cg-kind-resource"),
            new PartInfo(UnitPart.Health, typeof(HealthComponent), "Health", "Takes damage through a damage pipeline and dies when its health pool runs out.", "cg-kind-damage", UnitPart.Resources),
            new PartInfo(UnitPart.DespawnOnDeath, typeof(DespawnOnDeath), "Despawn On Death", "Despawns the unit a few seconds after it dies, so bodies clean themselves up.", "cg-kind-damage", UnitPart.Health),
            new PartInfo(UnitPart.StatusEffects, typeof(StatusEffectsComponent), "Status Effects", "Lets effects be applied, with optional innate effects on every spawn.", "cg-kind-effect"),
            new PartInfo(UnitPart.Loadout, typeof(FirableLoadout), "Firable Loadout", "Slots that hold firables, equipped when the unit spawns.", "cg-kind-firable"),
            new PartInfo(UnitPart.ViewBinder, typeof(UnitViewBinder), "View Binder", "Binds IUnitView components in children, like health bars and hit flashes.", "cg-kind-view"),
            new PartInfo(UnitPart.Knockback, typeof(RigidbodyKnockback), "Rigidbody Knockback", "Knockback and teleports through a Rigidbody, which is added for you.", "cg-kind-modifier"),
            new PartInfo(UnitPart.TransformAim, typeof(TransformAimProvider), "Transform Aim", "Aims where a transform faces, or at its target. Good for AI and turrets.", "cg-kind-aim", UnitPart.None, UnitPart.GroundAim),
            new PartInfo(UnitPart.GroundAim, typeof(GroundPlaneAimProvider), "Ground Plane Aim", "Aims at the mouse on the ground plane. Good for top-down players.", "cg-kind-aim", UnitPart.None, UnitPart.TransformAim)
        };

        public static IReadOnlyList<PartInfo> Custom => custom ??= Discover(Candidates());

        public static IReadOnlyList<PartInfo> Every => every ??= All.Concat(Custom).ToArray();

        internal static void UseCustom(IReadOnlyList<PartInfo> parts)
        {
            custom = parts;
            every = null;
        }

        public static PartInfo Of(UnitPart part)
        {
            foreach(var info in All)
            {
                if(info.Part == part) return info;
            }
            return null;
        }

        public static PartInfo OfType(Type type)
        {
            if(type == null) return null;
            foreach(var info in Every)
            {
                if(info.Type == type) return info;
            }
            return null;
        }

        public static UnitPart Present(GameObject prefab)
        {
            var parts = UnitPart.None;
            if(prefab == null) return parts;
            foreach(var info in All)
            {
                if(prefab.GetComponent(info.Type) != null) parts |= info.Part;
            }
            return parts;
        }

        public static List<PartInfo> PresentCustom(GameObject prefab)
        {
            var found = new List<PartInfo>();
            var parts = Custom;
            if(prefab == null || parts.Count == 0) return found;
            var onRoot = TypesOn(prefab.GetComponents<MonoBehaviour>());
            HashSet<Type> anywhere = null;
            foreach(var info in parts)
            {
                if(info.InChildren)
                {
                    anywhere ??= TypesOn(prefab.GetComponentsInChildren<MonoBehaviour>(true));
                    if(anywhere.Contains(info.Type)) found.Add(info);
                }
                else if(onRoot.Contains(info.Type))
                {
                    found.Add(info);
                }
            }
            return found;
        }

        public static List<PartInfo> PresentAll(GameObject prefab)
        {
            var found = new List<PartInfo>();
            if(prefab == null) return found;
            var present = Present(prefab);
            foreach(var info in All)
            {
                if((present & info.Part) != 0) found.Add(info);
            }
            found.AddRange(PresentCustom(prefab));
            return found;
        }

        public static int CountAll(GameObject prefab) => Count(Present(prefab)) + PresentCustom(prefab).Count;

        public static IEnumerable<string> Labels(GameObject prefab) => PresentAll(prefab).Select(p => p.Label);

        public static bool Has(GameObject prefab, PartInfo info) => prefab != null && info != null && Find(prefab, info).Count > 0;

        public static List<Component> Find(GameObject prefab, PartInfo info)
        {
            var found = new List<Component>();
            if(prefab == null || info == null) return found;
            var candidates = info.InChildren ? prefab.GetComponentsInChildren(info.Type, true) : prefab.GetComponents(info.Type);
            foreach(var component in candidates)
            {
                if(component != null && (!info.IsCustom || component.GetType() == info.Type)) found.Add(component);
            }
            return found;
        }

        public static List<string> Locations(GameObject prefab, PartInfo info)
        {
            var paths = new List<string>();
            foreach(var component in Find(prefab, info))
            {
                paths.Add(component.transform == prefab.transform ? prefab.name : AnimationUtility.CalculateTransformPath(component.transform, prefab.transform));
            }
            return paths;
        }

        public static List<PartInfo> Requirements(PartInfo part)
        {
            var found = new List<PartInfo>();
            if(part == null) return found;
            foreach(var info in All)
            {
                if((part.Requires & info.Part) != 0) found.Add(info);
            }
            foreach(var need in part.Needs)
            {
                var info = OfType(need);
                if(info != null && info != part && !found.Contains(info)) found.Add(info);
            }
            return found;
        }

        public static bool DependsOn(PartInfo part, PartInfo required)
        {
            if(part == null || required == null || part == required) return false;
            if(required.Part != UnitPart.None && (part.Requires & required.Part) != 0) return true;
            foreach(var need in part.Needs)
            {
                if(need.IsAssignableFrom(required.Type)) return true;
            }
            return false;
        }

        public static List<PartInfo> Dependents(GameObject prefab, PartInfo part)
        {
            var result = new List<PartInfo>();
            if(prefab == null || part == null) return result;
            var present = PresentAll(prefab);
            var removed = new List<PartInfo> { part };
            bool changed = true;
            while(changed)
            {
                changed = false;
                foreach(var other in present)
                {
                    if(removed.Contains(other) || !removed.Any(r => DependsOn(other, r))) continue;
                    removed.Add(other);
                    result.Add(other);
                    changed = true;
                }
            }
            return result;
        }

        public static string NeedsLabel(PartInfo info)
        {
            if(info == null) return null;
            var names = new List<string>();
            foreach(var each in All)
            {
                if((info.Requires & each.Part) != 0) names.Add(each.Label);
            }
            foreach(var need in info.Needs)
            {
                string name = OfType(need)?.Label ?? ObjectNames.NicifyVariableName(need.Name);
                if(!names.Contains(name)) names.Add(name);
            }
            return names.Count == 0 ? null : "Needs " + string.Join(", ", names);
        }

        public static IEnumerable<IGrouping<string, PartInfo>> Groups(IEnumerable<PartInfo> parts) => parts.GroupBy(p => p.Group);

        public static UnitPart Toggle(UnitPart parts, UnitPart part, bool on)
        {
            if(!on)
            {
                parts &= ~part;
                var removed = part;
                bool changed = true;
                while(changed)
                {
                    changed = false;
                    foreach(var other in All)
                    {
                        if((parts & other.Part) == 0 || (other.Requires & removed) == 0) continue;
                        parts &= ~other.Part;
                        removed |= other.Part;
                        changed = true;
                    }
                }
                return parts;
            }
            var adding = WithRequires(part);
            foreach(var info in All)
            {
                if((adding & info.Part) != 0) parts &= ~info.Excludes;
            }
            return parts | adding;
        }

        public static void ToggleCustom(UnitBlueprint blueprint, PartInfo info, bool on)
        {
            if(blueprint == null || info == null) return;
            string name = NameOf(info.Type);
            if(!on)
            {
                blueprint.CustomParts.Remove(name);
                Prune(blueprint);
                return;
            }
            if(blueprint.CustomParts.Contains(name)) return;
            blueprint.CustomParts.Add(name);
            foreach(var required in Requirements(info))
            {
                if(required.IsCustom) ToggleCustom(blueprint, required, true);
                else blueprint.Parts = Toggle(blueprint.Parts, required.Part, true);
            }
        }

        public static void Prune(UnitBlueprint blueprint)
        {
            if(blueprint == null) return;
            bool changed = true;
            while(changed)
            {
                changed = false;
                for(int i = blueprint.CustomParts.Count - 1; i >= 0; i--)
                {
                    var info = OfType(Resolve(blueprint.CustomParts[i]));
                    if(info == null || Satisfied(blueprint, info)) continue;
                    blueprint.CustomParts.RemoveAt(i);
                    changed = true;
                }
            }
        }

        public static int Count(UnitPart parts)
        {
            int count = 0;
            foreach(var info in All)
            {
                if((parts & info.Part) != 0) count++;
            }
            return count;
        }

        public static string NameOf(Type type) => type == null ? null : $"{type.FullName}, {type.Assembly.GetName().Name}";

        public static Type Resolve(string name) => string.IsNullOrWhiteSpace(name) ? null : Type.GetType(name, false);

        public static string ShortName(string name)
        {
            if(string.IsNullOrWhiteSpace(name)) return string.Empty;
            int comma = name.IndexOf(',');
            string full = comma >= 0 ? name.Substring(0, comma) : name;
            int dot = Math.Max(full.LastIndexOf('.'), full.LastIndexOf('+'));
            return ObjectNames.NicifyVariableName(dot >= 0 ? full.Substring(dot + 1) : full);
        }

        public static IReadOnlyList<PartInfo> Discover(IEnumerable<Type> types)
        {
            var found = new List<PartInfo>();
            var seen = new HashSet<Type>();
            foreach(var type in types)
            {
                if(type == null || !seen.Add(type)) continue;
                var attribute = Looks.Own<UnitPartAttribute>(type);
                if(!typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    if(attribute != null) Debug.LogWarning($"[CG Combat Framework] {type.Name} has [UnitPart] but isn't a MonoBehaviour, so it isn't a unit part");
                    continue;
                }
                if(type.IsAbstract || type.IsGenericType || type == typeof(Unit) || IsBuiltIn(type)) continue;

                bool arrival = typeof(IArrivalSequence).IsAssignableFrom(type);
                bool departure = typeof(IDepartureSequence).IsAssignableFrom(type);
                bool sequence = arrival || departure;
                if(attribute == null && !sequence && !typeof(IUnitComponent).IsAssignableFrom(type)) continue;
                if(attribute != null && attribute.Hidden) continue;
                var menu = (AddComponentMenu)Attribute.GetCustomAttribute(type, typeof(AddComponentMenu), false);
                if(menu != null && string.IsNullOrWhiteSpace(menu.componentMenu)) continue;

                string group = Looks.PartGroup(type) ?? (sequence ? k_Sequences : null);
                found.Add(new PartInfo(type, LabelOf(type, attribute, menu), DescriptionOf(attribute, arrival, departure), group, NeedsOf(type), sequence));
            }
            found.Sort(Compare);
            return found;
        }

        public static bool IsTestAssembly(Assembly assembly)
        {
            if(assembly == null) return false;
            if(testAssemblies.TryGetValue(assembly, out bool cached)) return cached;
            bool test = ReferencesTests(assembly) || ConstrainedToTests(assembly.GetName().Name);
            testAssemblies[assembly] = test;
            return test;
        }

        private static IEnumerable<Type> Candidates()
        {
            return TypeCache.GetTypesDerivedFrom<IUnitComponent>()
                .Concat(TypeCache.GetTypesWithAttribute<UnitPartAttribute>())
                .Concat(TypeCache.GetTypesDerivedFrom<IArrivalSequence>())
                .Concat(TypeCache.GetTypesDerivedFrom<IDepartureSequence>())
                .Where(t => !IsTestAssembly(t.Assembly));
        }

        private static bool Satisfied(UnitBlueprint blueprint, PartInfo info)
        {
            foreach(var required in Requirements(info))
            {
                if(required.IsCustom ? !blueprint.CustomParts.Contains(NameOf(required.Type)) : !blueprint.Has(required.Part)) return false;
            }
            return true;
        }

        private static UnitPart WithRequires(UnitPart parts)
        {
            bool changed = true;
            while(changed)
            {
                changed = false;
                foreach(var info in All)
                {
                    if((parts & info.Part) == 0 || (parts & info.Requires) == info.Requires) continue;
                    parts |= info.Requires;
                    changed = true;
                }
            }
            return parts;
        }

        private static HashSet<Type> TypesOn(MonoBehaviour[] components)
        {
            var types = new HashSet<Type>();
            foreach(var component in components)
            {
                if(component != null) types.Add(component.GetType());
            }
            return types;
        }

        private static bool IsBuiltIn(Type type)
        {
            foreach(var info in All)
            {
                if(info.Type == type) return true;
            }
            return false;
        }

        private static string LabelOf(Type type, UnitPartAttribute attribute, AddComponentMenu menu)
        {
            if(!string.IsNullOrWhiteSpace(attribute?.Label)) return attribute.Label.Trim();
            if(menu != null)
            {
                string path = menu.componentMenu.Trim().TrimEnd('/');
                int slash = path.LastIndexOf('/');
                string last = (slash >= 0 ? path.Substring(slash + 1) : path).Trim();
                if(last.Length > 0) return last;
            }
            return ObjectNames.NicifyVariableName(type.Name);
        }

        private static string DescriptionOf(UnitPartAttribute attribute, bool arrival, bool departure)
        {
            if(!string.IsNullOrWhiteSpace(attribute?.Description)) return attribute.Description;
            if(arrival && departure) return "Plays when the unit arrives and leaves";
            if(arrival) return "Plays when the unit arrives";
            if(departure) return "Plays when the unit leaves";
            return null;
        }

        private static Type[] NeedsOf(Type type)
        {
            var needs = new List<Type>();
            foreach(RequireComponent require in Attribute.GetCustomAttributes(type, typeof(RequireComponent), true))
            {
                AddNeed(needs, require.m_Type0, type);
                AddNeed(needs, require.m_Type1, type);
                AddNeed(needs, require.m_Type2, type);
            }
            return needs.ToArray();
        }

        private static void AddNeed(List<Type> needs, Type need, Type owner)
        {
            if(need == null || need == owner || need == typeof(Transform) || needs.Contains(need)) return;
            needs.Add(need);
        }

        private static int Compare(PartInfo a, PartInfo b)
        {
            int rank = Rank(a.Group).CompareTo(Rank(b.Group));
            if(rank != 0) return rank;
            int group = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
            return group != 0 ? group : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        }

        private static int Rank(string group) => group == null ? 0 : group == k_Sequences ? 2 : 1;

        private static bool ReferencesTests(Assembly assembly)
        {
            foreach(var reference in assembly.GetReferencedAssemblies())
            {
                if(Array.IndexOf(testReferences, reference.Name) >= 0) return true;
            }
            return false;
        }

        private static bool ConstrainedToTests(string name)
        {
            string path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
            if(string.IsNullOrEmpty(path)) return false;
            var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
            if(asset == null) return false;
            try
            {
                return JsonUtility.FromJson<AsmdefFile>(asset.text)?.IsTest ?? false;
            }
            catch(ArgumentException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class AsmdefFile
        {
            [SerializeField] private string[] defineConstraints;
            [SerializeField] private string[] optionalUnityReferences;

            public bool IsTest => Contains(defineConstraints, "UNITY_INCLUDE_TESTS") || Contains(optionalUnityReferences, "TestAssemblies");

            private static bool Contains(string[] values, string value) => values != null && Array.IndexOf(values, value) >= 0;
        }
    }
}
