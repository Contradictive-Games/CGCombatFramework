using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal enum Severity { Error, Warning, Info }

    internal sealed class Finding
    {
        public readonly DoctorRule Rule;
        public readonly Severity Severity;
        public readonly Object Asset;
        public readonly string Path;
        public readonly string Where;
        public readonly string Message;
        public readonly string FixLabel;
        public readonly Action Fix;
        public readonly bool Safe;

        public Finding(DoctorRule rule, Severity severity, Object asset, string path, string where, string message, string fixLabel = null, Action fix = null, bool safe = false)
        {
            Rule = rule;
            Severity = severity;
            Asset = asset;
            Path = path;
            Where = where;
            Message = message;
            FixLabel = fixLabel;
            Fix = fix;
            Safe = safe;
        }
    }

    internal sealed class DoctorScan
    {
        public readonly List<ScriptableObject> Assets;
        public readonly List<GameObject> Units;

        public DoctorScan(IEnumerable<Object> only)
        {
            var filter = only?.Where(o => o != null).ToList();
            Assets = CombatTools.CombatAssets();
            Units = UnitLibrary.All.Where(u => u != null).ToList();
            if(filter == null || filter.Count == 0) return;
            Assets = Assets.Where(a => filter.Contains(a)).ToList();
            Units = Units.Where(u => filter.Contains(u)).ToList();
        }

        public IEnumerable<T> All<T>() where T : Object => Assets.OfType<T>();
    }

    internal sealed class DoctorRule
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        public readonly bool DefaultOn;
        private readonly Action<DoctorRule, DoctorScan, List<Finding>> check;

        public DoctorRule(string id, string title, string description, bool defaultOn, Action<DoctorRule, DoctorScan, List<Finding>> check)
        {
            Id = id;
            Title = title;
            Description = description;
            DefaultOn = defaultOn;
            this.check = check;
        }

        public string PrefKey => "Doctor.Rule." + Id;
        public bool Enabled => StudioPrefs.Get(PrefKey, DefaultOn);

        public void Check(DoctorScan scan, List<Finding> into) => check(this, scan, into);
    }

    internal static class DoctorRules
    {
        private static readonly (Type type, string field, Severity severity, string message)[] required =
        {
            (typeof(ArmorStep), "armorStat", Severity.Warning, "Armor does nothing without an armor stat"),
            (typeof(CritStrikeStep), "critChanceStat", Severity.Warning, "Never crits without a crit chance stat"),
            (typeof(EvadeStep), "evadeChance", Severity.Warning, "Never evades without an evade chance stat"),
            (typeof(FlatReductionStep), "reduction", Severity.Warning, "Does nothing without a reduction stat"),
            (typeof(ShieldAbsorbStep), "shield", Severity.Warning, "Does nothing without a shield resource"),
            (typeof(ImmuneWithTagStep), "tag", Severity.Info, "Only checks damage type immunity tags without a tag"),
            (typeof(ApplyStatusEffectPayload), "effect", Severity.Warning, "Applies nothing without an effect"),
            (typeof(RemoveEffectPayload), "effect", Severity.Warning, "Removes nothing without an effect"),
            (typeof(ConsumeStacksPayload), "effect", Severity.Warning, "Consumes nothing without an effect"),
            (typeof(DispelEffectsPayload), "category", Severity.Warning, "Dispels nothing without a category"),
            (typeof(ModifyResourcePayload), "resource", Severity.Warning, "Does nothing without a resource"),
            (typeof(SpawnUnitPayload), "prefab", Severity.Warning, "Spawns nothing without a prefab"),
            (typeof(ProjectileEmission), "prefab", Severity.Warning, "Fires nothing without a projectile prefab"),
            (typeof(HasStatusEffectCondition), "effect", Severity.Warning, "Always fails without an effect"),
            (typeof(HasTagCondition), "tag", Severity.Warning, "Always fails without a tag"),
            (typeof(HitZoneCondition), "zoneTag", Severity.Warning, "Always fails without a zone tag"),
            (typeof(StatCompareCondition), "stat", Severity.Warning, "Compares against nothing without a stat"),
            (typeof(ResourceThresholdCondition), "resource", Severity.Warning, "Always fails without a resource"),
            (typeof(StatScaler), "stat", Severity.Warning, "Scales by nothing without a stat"),
            (typeof(PercentOfResourceScaler), "resource", Severity.Warning, "Scales by nothing without a resource"),
            (typeof(EffectStacksScaler), "effect", Severity.Warning, "Counts no stacks without an effect"),
            (typeof(ResourceCostGate), "resource", Severity.Error, "Can never fire without a resource to pay with")
        };

        public static readonly DoctorRule[] All =
        {
            new DoctorRule("missing-types", "Missing block types", "Blocks whose class was renamed or deleted. They load as nothing and stop the asset from saving cleanly.", true, MissingTypes),
            new DoctorRule("empty-slots", "Empty slots", "Block slots or list entries with no type chosen.", true, EmptySlots),
            new DoctorRule("required-references", "Unset references", "Blocks that do nothing, or always fail, until a stat, resource, tag or effect is set.", true, RequiredReferences),
            new DoctorRule("duplicate-ids", "Duplicate IDs", "Two definitions with the same ID. Lookups by ID will find the wrong one.", true, DuplicateIds),
            new DoctorRule("empty-ids", "Empty IDs", "Definitions with no ID set.", true, EmptyIds),
            new DoctorRule("id-format", "ID format", "IDs with spaces or capitals. Not wrong, but harder to type in data and code.", false, IdFormat),
            new DoctorRule("tag-cycles", "Tag parent loops", "Tags whose parents loop back to themselves, which makes tag checks loop forever.", true, TagCycles),
            new DoctorRule("faction-conflicts", "Faction conflicts", "Factions listed as both friendly and hostile, or listing themselves.", true, FactionConflicts),
            new DoctorRule("firables", "Firable setup", "Firables with no emissions, or Full Auto with nothing limiting the fire rate.", true, Firables),
            new DoctorRule("effects", "Status effect setup", "Effects that do nothing, or tick payloads that can never run.", true, Effects),
            new DoctorRule("profiles", "Stat profiles", "Duplicate or empty entries, and resources whose max stat is missing.", true, Profiles),
            new DoctorRule("modifiers", "Stat modifiers", "Stat modifier definitions with no target stat.", true, Modifiers),
            new DoctorRule("resistances", "Unused resistances", "Damage types whose resistance stat no stat profile has.", true, Resistances),
            new DoctorRule("units", "Units", "The Unit Creator's checks for every unit prefab.", true, Units),
            new DoctorRule("pipeline-order", "Pipeline order tips", "Minimum Damage placed before other steps that lower damage.", false, PipelineOrder)
        };

        private static string Name(Object asset) => StudioKinds.NameOf(asset);

        private static void MissingTypes(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var asset in scan.Assets)
            {
                if(!SerializationUtility.HasManagedReferencesWithMissingTypes(asset)) continue;
                var names = SerializationUtility.GetManagedReferencesWithMissingTypes(asset).Select(m => m.className).Distinct();
                var target = asset;
                into.Add(new Finding(rule, Severity.Error, asset, null, null, $"Uses block types that no longer exist: {string.Join(", ", names)}", "Remove Them", () =>
                {
                    Undo.RegisterCompleteObjectUndo(target, "Remove Missing Blocks");
                    SerializationUtility.ClearAllManagedReferencesWithMissingTypes(target);
                    EditorUtility.SetDirty(target);
                }));
            }
        }

        private static void EmptySlots(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var asset in scan.Assets)
            {
                using(var serialized = new SerializedObject(asset))
                {
                    foreach(var property in CombatTools.Walk(serialized))
                    {
                        if(property.propertyType != SerializedPropertyType.ManagedReference || property.managedReferenceValue != null) continue;
                        string path = property.propertyPath;
                        string where = CombatTools.Describe(serialized, path);
                        if(asset is FirableDefinition && path == "Mode")
                        {
                            into.Add(new Finding(rule, Severity.Info, asset, path, where, "No fire mode, so it fires once per press like Semi Auto"));
                            continue;
                        }
                        if(!BlockEdits.TrySplit(path, out _, out _))
                        {
                            into.Add(new Finding(rule, Severity.Warning, asset, path, where, "This slot is empty"));
                            continue;
                        }
                        var target = asset;
                        into.Add(new Finding(rule, Severity.Warning, asset, path, where, "Empty list entry", "Remove Entry", () =>
                        {
                            using(var edit = new SerializedObject(target))
                            {
                                BlockEdits.Remove(edit, path);
                            }
                        }, true));
                    }
                }
            }
        }

        private static void RequiredReferences(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var asset in scan.Assets)
            {
                using(var serialized = new SerializedObject(asset))
                {
                    foreach(var property in CombatTools.Walk(serialized))
                    {
                        if(property.propertyType != SerializedPropertyType.ManagedReference) continue;
                        var type = BlockTypes.ValueType(property);
                        if(type == null) continue;
                        foreach(var (blockType, field, severity, message) in required)
                        {
                            if(blockType != type) continue;
                            var reference = property.FindPropertyRelative(field);
                            if(reference == null || reference.propertyType != SerializedPropertyType.ObjectReference || reference.objectReferenceValue != null) continue;
                            into.Add(new Finding(rule, severity, asset, property.propertyPath, CombatTools.Describe(serialized, property.propertyPath), message));
                        }
                    }
                }
            }
        }

        private static void DuplicateIds(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            var all = CombatTools.CombatAssets().OfType<GameDefinition>().ToList();
            var taken = new HashSet<string>(all.Select(d => d.Id));
            foreach(var group in all.GroupBy(d => d.Id).Where(g => g.Count() > 1))
            {
                foreach(var definition in group)
                {
                    if(!scan.Assets.Contains(definition)) continue;
                    var others = group.Where(d => d != definition).Select(Name);
                    var target = definition;
                    into.Add(new Finding(rule, Severity.Error, definition, null, null, $"ID {definition.Id} is also used by {string.Join(", ", others)}", "Make Unique", () => MakeUnique(target, taken)));
                }
            }
        }

        private static void EmptyIds(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var definition in scan.All<GameDefinition>())
            {
                using(var serialized = new SerializedObject(definition))
                {
                    var id = serialized.FindProperty("id");
                    if(id == null || !string.IsNullOrWhiteSpace(id.stringValue)) continue;
                }
                var target = definition;
                into.Add(new Finding(rule, Severity.Warning, definition, null, null, "No ID set", "Use File Name", () => DefinitionAssets.ResetId(target, AssetDatabase.GetAssetPath(target)), true));
            }
        }

        private static void IdFormat(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var definition in scan.All<GameDefinition>())
            {
                string raw;
                using(var serialized = new SerializedObject(definition))
                {
                    raw = serialized.FindProperty("id")?.stringValue;
                }
                if(string.IsNullOrEmpty(raw) || (raw == raw.ToLowerInvariant() && !raw.Contains(' '))) continue;
                var target = definition;
                string clean = raw.Trim().ToLowerInvariant().Replace(' ', '_');
                into.Add(new Finding(rule, Severity.Info, definition, null, null, $"ID '{raw}' could be '{clean}'", "Use " + clean, () => SetId(target, clean)));
            }
        }

        private static void TagCycles(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var tag in scan.All<CombatTag>())
            {
                var chain = new List<CombatTag>();
                for(var each = tag; each != null; each = each.Parent)
                {
                    if(chain.Contains(each))
                    {
                        if(each != tag) break;
                        var target = tag;
                        into.Add(new Finding(rule, Severity.Error, tag, "Parent", "Parent", $"Parents loop: {string.Join(" → ", chain.Select(Name))} → {Name(tag)}", "Clear Parent", () =>
                        {
                            Undo.RecordObject(target, "Clear Tag Parent");
                            target.Parent = null;
                            EditorUtility.SetDirty(target);
                        }));
                        break;
                    }
                    chain.Add(each);
                }
            }
        }

        private static void FactionConflicts(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var faction in scan.All<Faction>())
            {
                var target = faction;
                if((faction.HostileTo ?? Array.Empty<Faction>()).Contains(faction) || (faction.FriendlyTo ?? Array.Empty<Faction>()).Contains(faction))
                {
                    into.Add(new Finding(rule, Severity.Info, faction, null, null, "Lists itself. A faction is always friendly to itself", "Remove Self", () => Remove(target, target), true));
                }
                foreach(var friend in faction.FriendlyTo ?? Array.Empty<Faction>())
                {
                    if(friend == null || friend == faction) continue;
                    bool hostile = (faction.HostileTo ?? Array.Empty<Faction>()).Contains(friend) || (friend.HostileTo ?? Array.Empty<Faction>()).Contains(faction);
                    if(!hostile) continue;
                    var other = friend;
                    into.Add(new Finding(rule, Severity.Warning, faction, "FriendlyTo", "Friendly To", $"Lists {Name(friend)} as friendly, but one of them lists the other as hostile, so they are hostile", "Remove from Friendly To", () => RemoveFriend(target, other)));
                }
            }
        }

        private static void Firables(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var firable in scan.All<FirableDefinition>())
            {
                if(firable.Emissions == null || firable.Emissions.Length == 0) into.Add(new Finding(rule, Severity.Warning, firable, "Emissions", "Emissions", "Fires but does nothing: no emissions"));
                bool limited = (firable.Gates ?? Array.Empty<IFireGate>()).Any(g => g is CooldownGate || g is ChargesGate || g is AmmoGate || g is ResourceCostGate);
                if(firable.Mode is FullAutoFire && !limited) into.Add(new Finding(rule, Severity.Warning, firable, "Gates", "Gates", "Full Auto with no Cooldown, Ammo, Charges or Cost fires every frame"));
            }
        }

        private static void Effects(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var effect in scan.All<StatusEffectDefinition>())
            {
                bool tags = (effect.GrantedTags ?? Array.Empty<CombatTag>()).Any(t => t != null);
                bool behaviors = (effect.Behaviors ?? Array.Empty<IStatusEffectBehavior>()).Any(b => b != null);
                if(!behaviors && !tags) into.Add(new Finding(rule, Severity.Warning, effect, "Behaviors", "Behaviors", "Does nothing: no behaviors and no granted tags"));
                if(effect.Stacking == EffectStackingMode.AddDuration && effect.MaxDuration <= 0f && effect.MaxStacks > 1) into.Add(new Finding(rule, Severity.Info, effect, "MaxDuration", "Max Duration", "Add Duration stacking with no Max Duration can stack forever"));

                using(var serialized = new SerializedObject(effect))
                {
                    var list = serialized.FindProperty("Behaviors");
                    for(int i = 0; list != null && i < list.arraySize; i++)
                    {
                        var behavior = list.GetArrayElementAtIndex(i);
                        if(BlockTypes.ValueType(behavior) != typeof(PayloadsBehavior)) continue;
                        var when = behavior.FindPropertyRelative("when");
                        if(when == null) continue;
                        var flags = (PayloadEvent)when.enumValueFlag;
                        string where = CombatTools.Describe(serialized, behavior.propertyPath);
                        if((flags & PayloadEvent.Tick) != 0 && effect.TickInterval <= 0f) into.Add(new Finding(rule, Severity.Warning, effect, behavior.propertyPath, where, "Runs on Tick, but Tick Interval is 0 so it never ticks"));
                        if((flags & PayloadEvent.Expired) != 0 && effect.IsPermanent) into.Add(new Finding(rule, Severity.Info, effect, behavior.propertyPath, where, "Runs on Expired, but the effect is permanent (Duration 0) so it never expires"));
                        if(flags == PayloadEvent.None) into.Add(new Finding(rule, Severity.Warning, effect, behavior.propertyPath, where, "When is set to nothing, so the payloads never run"));
                    }
                }
            }
        }

        private static void Profiles(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var profile in scan.All<UnitStatProfile>())
            {
                var stats = profile.Stats ?? Array.Empty<StatEntry>();
                var seen = new HashSet<StatType>();
                for(int i = 0; i < stats.Length; i++)
                {
                    int index = i;
                    var target = profile;
                    if(stats[i].Type == null)
                    {
                        into.Add(new Finding(rule, Severity.Warning, profile, $"Stats.Array.data[{i}]", $"Stats {i + 1}", "Stat entry with no stat type", "Remove Entry", () => RemoveStat(target, index), true));
                        continue;
                    }
                    if(!seen.Add(stats[i].Type)) into.Add(new Finding(rule, Severity.Warning, profile, $"Stats.Array.data[{i}]", $"Stats {i + 1}", $"{Name(stats[i].Type)} is listed twice. Only the first one counts", "Remove Duplicate", () => RemoveStat(target, index), true));
                }

                var resources = profile.Resources ?? Array.Empty<ResourceEntry>();
                var seenResources = new HashSet<ResourceType>();
                for(int i = 0; i < resources.Length; i++)
                {
                    int index = i;
                    var target = profile;
                    var entry = resources[i];
                    if(entry == null || entry.Type == null)
                    {
                        into.Add(new Finding(rule, Severity.Warning, profile, $"Resources.Array.data[{i}]", $"Resources {i + 1}", "Resource entry with no resource type", "Remove Entry", () => RemoveResource(target, index), true));
                        continue;
                    }
                    if(!seenResources.Add(entry.Type)) into.Add(new Finding(rule, Severity.Warning, profile, $"Resources.Array.data[{i}]", $"Resources {i + 1}", $"{Name(entry.Type)} is listed twice. Only the first one counts", "Remove Duplicate", () => RemoveResource(target, index), true));
                    var maxStat = entry.Type.MaxStat;
                    if(maxStat != null && !seen.Contains(maxStat))
                    {
                        float max = entry.BaseMax;
                        into.Add(new Finding(rule, Severity.Info, profile, $"Resources.Array.data[{i}]", $"Resources {i + 1}", $"{Name(entry.Type)} takes its max from {Name(maxStat)}, which isn't listed, so it starts at {maxStat.DefaultValue:0.##}", $"Add {Name(maxStat)}", () => AddStat(target, maxStat, max)));
                    }
                }
            }
        }

        private static void Modifiers(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var modifier in scan.All<StatModifierDefinition>())
            {
                if(modifier.TargetStat == null) into.Add(new Finding(rule, Severity.Warning, modifier, "TargetStat", "Target Stat", "Modifies nothing without a target stat"));
            }
        }

        private static void Resistances(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            var listed = new HashSet<StatType>(CombatTools.CombatAssets().OfType<UnitStatProfile>().SelectMany(p => p.Stats ?? Array.Empty<StatEntry>()).Select(e => e.Type).Where(t => t != null));
            foreach(var type in scan.All<DamageType>())
            {
                if(type.ResistanceStat == null || listed.Contains(type.ResistanceStat)) continue;
                into.Add(new Finding(rule, Severity.Info, type, "ResistanceStat", "Resistance Stat", $"No stat profile has {Name(type.ResistanceStat)}, so {Name(type)} is only resisted by status effects"));
            }
        }

        private static void Units(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var unit in scan.Units)
            {
                foreach(var issue in UnitChecks.Of(unit))
                {
                    var severity = issue.Severity == HelpBoxMessageType.Error ? Severity.Error : issue.Severity == HelpBoxMessageType.Warning ? Severity.Warning : Severity.Info;
                    into.Add(new Finding(rule, severity, unit, null, null, issue.Message, issue.FixLabel, issue.Fix));
                }
            }
        }

        private static void PipelineOrder(DoctorRule rule, DoctorScan scan, List<Finding> into)
        {
            foreach(var pipeline in scan.All<DamagePipeline>())
            {
                var steps = pipeline.Steps ?? Array.Empty<IDamageStep>();
                int minimum = Array.FindIndex(steps, s => s is MinimumDamageStep);
                if(minimum < 0) continue;
                for(int i = minimum + 1; i < steps.Length; i++)
                {
                    if(!(steps[i] is ArmorStep || steps[i] is DamageResistanceStep || steps[i] is FlatReductionStep || steps[i] is ShieldAbsorbStep)) continue;
                    into.Add(new Finding(rule, Severity.Info, pipeline, $"Steps.Array.data[{minimum}]", $"Steps {minimum + 1}", $"Minimum Damage runs before {BlockTypes.DisplayName(steps[i].GetType())}, so the floor can be undercut"));
                    break;
                }
            }
        }

        private static void MakeUnique(GameDefinition definition, HashSet<string> taken)
        {
            string raw;
            using(var serialized = new SerializedObject(definition))
            {
                raw = serialized.FindProperty("id")?.stringValue ?? definition.name.ToLowerInvariant();
            }
            string prefix = definition.Id.Substring(0, definition.Id.Length - raw.Length);
            for(int n = 2; n < 1000; n++)
            {
                string candidate = $"{raw}_{n}";
                if(taken.Contains(prefix + candidate)) continue;
                taken.Add(prefix + candidate);
                SetId(definition, candidate);
                return;
            }
        }

        private static void SetId(GameDefinition definition, string id)
        {
            using(var serialized = new SerializedObject(definition))
            {
                var property = serialized.FindProperty("id");
                if(property == null) return;
                property.stringValue = id;
                serialized.ApplyModifiedProperties();
            }
        }

        private static void Remove(Faction faction, Faction other)
        {
            Undo.RecordObject(faction, "Fix Faction");
            faction.HostileTo = (faction.HostileTo ?? Array.Empty<Faction>()).Where(f => f != other).ToArray();
            faction.FriendlyTo = (faction.FriendlyTo ?? Array.Empty<Faction>()).Where(f => f != other).ToArray();
            EditorUtility.SetDirty(faction);
        }

        private static void RemoveFriend(Faction faction, Faction other)
        {
            Undo.RecordObject(faction, "Fix Faction");
            faction.FriendlyTo = (faction.FriendlyTo ?? Array.Empty<Faction>()).Where(f => f != other).ToArray();
            EditorUtility.SetDirty(faction);
        }

        private static void RemoveStat(UnitStatProfile profile, int index)
        {
            Undo.RecordObject(profile, "Fix Stat Profile");
            var list = (profile.Stats ?? Array.Empty<StatEntry>()).ToList();
            if(index >= 0 && index < list.Count) list.RemoveAt(index);
            profile.Stats = list.ToArray();
            EditorUtility.SetDirty(profile);
        }

        private static void RemoveResource(UnitStatProfile profile, int index)
        {
            Undo.RecordObject(profile, "Fix Stat Profile");
            var list = (profile.Resources ?? Array.Empty<ResourceEntry>()).ToList();
            if(index >= 0 && index < list.Count) list.RemoveAt(index);
            profile.Resources = list.ToArray();
            EditorUtility.SetDirty(profile);
        }

        private static void AddStat(UnitStatProfile profile, StatType type, float value)
        {
            Undo.RecordObject(profile, "Fix Stat Profile");
            var list = (profile.Stats ?? Array.Empty<StatEntry>()).ToList();
            list.Add(new StatEntry { Type = type, BaseValue = value });
            profile.Stats = list.ToArray();
            EditorUtility.SetDirty(profile);
        }
    }
}
