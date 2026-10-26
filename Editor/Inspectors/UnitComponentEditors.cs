using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    [CustomEditor(typeof(StatsComponent)), CanEditMultipleObjects]
    public sealed class StatsComponentEditor : CombatInspector
    {
        private readonly List<(Label value, StatType stat)> rows = new();

        protected override string Style => "cg-kind-stat";
        protected override string About => Description(typeof(StatsComponent));

        protected override void Extras(VisualElement root) => ProfileSection.Add(root, serializedObject, "Stats", "Profile Stats");

        protected override void Live(VisualElement root)
        {
            var profile = UnitLibrary.Read<UnitStatProfile>((Component)target, "profile");
            foreach(var entry in profile != null ? profile.Stats ?? Array.Empty<StatEntry>() : Array.Empty<StatEntry>())
            {
                if(entry.Type == null) continue;
                var row = new VisualElement();
                row.AddToClassList("cg-inspector__row");
                row.Add(CombatEditorStyles.Text(StudioKinds.NameOf(entry.Type), "cg-inspector__row-name"));
                var value = CombatEditorStyles.Text(null, "cg-inspector__row-value");
                row.Add(value);
                root.Add(row);
                rows.Add((value, entry.Type));
            }
            Refresh();
        }

        protected override void Refresh()
        {
            var stats = (StatsComponent)target;
            foreach(var (value, type) in rows)
            {
                if(!stats.TryGetStat(type, out var stat))
                {
                    value.text = "-";
                    continue;
                }
                bool changed = !Mathf.Approximately(stat.Value, stat.BaseValue);
                value.text = changed ? $"{stat.BaseValue:0.##} → {stat.Value:0.##}" : $"{stat.Value:0.##}";
                value.EnableInClassList("cg-inspector__row-value--changed", changed);
            }
        }
    }

    [CustomEditor(typeof(ResourceComponent)), CanEditMultipleObjects]
    public sealed class ResourceComponentEditor : CombatInspector
    {
        private readonly List<(VisualElement fill, Label value, IReadonlyResource resource)> bars = new();

        protected override string Style => "cg-kind-resource";
        protected override string About => Description(typeof(ResourceComponent));

        protected override void Extras(VisualElement root)
        {
            var gameObject = Owner;
            var stats = UnitLibrary.Read<UnitStatProfile>(gameObject.GetComponent<StatsComponent>(), "profile");
            var mine = UnitLibrary.Read<UnitStatProfile>((Component)target, "profile");
            if(stats != null && mine != stats)
            {
                var box = new HelpBox($"Stats uses {StudioKinds.NameOf(stats)} but this uses {(mine != null ? StudioKinds.NameOf(mine) : "nothing")}. Max stats are read from Stats", HelpBoxMessageType.Info);
                box.Add(Small($"Use {StudioKinds.NameOf(stats)}", () =>
                {
                    serializedObject.FindProperty("profile").objectReferenceValue = stats;
                    serializedObject.ApplyModifiedProperties();
                }));
                root.Add(box);
            }
            ProfileSection.Add(root, serializedObject, "Resources", "Profile Resources");
        }

        protected override void Live(VisualElement root)
        {
            foreach(var resource in ((ResourceComponent)target).All)
            {
                root.Add(Bar(out var fill, out var value, StudioKinds.NameOf(resource.Type), resource.Type != null ? resource.Type.Color : Color.gray));
                bars.Add((fill, value, resource));
            }
            Refresh();
        }

        protected override void Refresh()
        {
            foreach(var (fill, value, resource) in bars)
            {
                SetBar(fill, value, resource);
            }
        }
    }

    [CustomEditor(typeof(HealthComponent)), CanEditMultipleObjects]
    public sealed class HealthComponentEditor : CombatInspector
    {
        private VisualElement summary;
        private VisualElement fill;
        private Label value;
        private Label state;

        protected override string Style => "cg-kind-damage";
        protected override string About => Description(typeof(HealthComponent));

        protected override void Extras(VisualElement root)
        {
            summary = new VisualElement();
            root.Add(summary);
            var watcher = new VisualElement();
            watcher.AddToClassList("cg-watcher");
            root.Add(watcher);
            watcher.TrackSerializedObjectValue(serializedObject, _ => Summarize());
            Summarize();
        }

        private void Summarize()
        {
            summary.Clear();
            var gameObject = Owner;
            var type = UnitLibrary.HealthOf(gameObject);
            var profile = UnitLibrary.ResourceProfileOf(gameObject);
            var entry = UnitLibrary.ResourceOf(profile, type);
            if(type == null) summary.Add(new HelpBox("Pick a Health Type, or the unit can't take damage", HelpBoxMessageType.Error));
            else if(entry == null) summary.Add(new HelpBox(profile == null ? "There's no resource profile to hold the health pool" : $"{StudioKinds.NameOf(type)} isn't in {StudioKinds.NameOf(profile)}, so the unit can't take damage", HelpBoxMessageType.Error));
            else
            {
                string from = type.MaxStat != null ? $" from {StudioKinds.NameOf(type.MaxStat)}" : string.Empty;
                summary.Add(CombatEditorStyles.Text($"Max {UnitLibrary.MaxOf(UnitLibrary.ProfileOf(gameObject), entry):0.##}{from}", "cg-note"));
            }

            var pipeline = UnitLibrary.PipelineOf(gameObject);
            var prefab = PrefabOf(gameObject);
            if(pipeline == null) summary.Add(CombatEditorStyles.Text("No damage pipeline, so damage is applied as-is", "cg-note"));
            int steps = pipeline != null ? pipeline.Steps?.Length ?? 0 : 0;
            summary.Add(Row(
                pipeline != null ? CombatEditorStyles.Chip(steps == 1 ? "1 step" : $"{steps} steps") : null,
                pipeline != null ? Small("Edit Pipeline", () => CombatStudioWindow.Open(pipeline)) : null,
                prefab != null ? Small("Test Damage", () => PipelineTesterWindow.OpenWithDefender(prefab)) : pipeline != null ? Small("Test Pipeline", () => PipelineTesterWindow.Open(pipeline)) : null));
        }

        protected override void Live(VisualElement root)
        {
            state = CombatEditorStyles.Text(null, "cg-card__summary");
            root.Add(state);
            root.Add(Bar(out fill, out value, "Health", new Color(0.25f, 0.73f, 0.31f)));
            var health = (HealthComponent)target;
            var unit = Owner.GetComponent<Unit>();
            root.Add(Row(
                Small("Kill", () => health.Kill(new EffectContext(null, null, unit))),
                Small("Heal Full", () => health.ApplyHeal(health.Resource != null ? health.Resource.Max : 0f, new EffectContext(null, null, unit))),
                Small("Revive", () => health.Revive())));
            Refresh();
        }

        protected override void Refresh()
        {
            var health = (HealthComponent)target;
            state.text = health.IsDead ? "Dead" : "Alive";
            SetBar(fill, value, health.Resource);
        }
    }

    [CustomEditor(typeof(StatusEffectsComponent)), CanEditMultipleObjects]
    public sealed class StatusEffectsComponentEditor : CombatInspector
    {
        private VisualElement list;
        private string signature;
        private StatusEffectDefinition effect;

        protected override string Style => "cg-kind-effect";
        protected override string About => Description(typeof(StatusEffectsComponent));

        protected override void Live(VisualElement root)
        {
            list = new VisualElement();
            root.Add(list);
            var apply = new VisualElement();
            apply.AddToClassList("cg-field-row");
            var field = new ObjectField("Apply") { objectType = typeof(StatusEffectDefinition), allowSceneObjects = false };
            field.RegisterValueChangedCallback(evt => effect = evt.newValue as StatusEffectDefinition);
            apply.Add(field);
            var effects = (StatusEffectsComponent)target;
            apply.Add(Small("Apply", () =>
            {
                var unit = Owner.GetComponent<Unit>();
                if(effect != null && unit != null) effects.Apply(effect, unit);
            }));
            root.Add(apply);
            Refresh();
        }

        protected override void Refresh()
        {
            var effects = (StatusEffectsComponent)target;
            string current = string.Join("|", effects.Active.Select(e => $"{e.GetHashCode()}:{e.Stacks}:{e.Remaining:0.0}"));
            if(current == signature) return;
            signature = current;
            list.Clear();
            if(effects.Active.Count == 0) list.Add(CombatEditorStyles.Text("No status effects", "cg-empty-note"));
            foreach(var instance in effects.Active)
            {
                var each = instance;
                string time = instance.Definition != null && instance.Definition.IsPermanent ? "permanent" : $"{instance.Remaining:0.0}s left";
                var row = new VisualElement();
                row.AddToClassList("cg-inspector__row");
                row.Add(CombatEditorStyles.Text($"{StudioKinds.NameOf(instance.Definition)} ×{instance.Stacks} · {time} · from {CombatLog.NameOf(instance.SourceUnit)}", "cg-inspector__row-name"));
                row.Add(Small("Remove", () => effects.Remove(new StatusEffectHandle(each))));
                list.Add(row);
            }
        }
    }

    [CustomEditor(typeof(FirableLoadout)), CanEditMultipleObjects]
    public sealed class FirableLoadoutEditor : CombatInspector
    {
        private readonly List<(Label value, Firable firable)> rows = new();

        protected override string Style => "cg-kind-firable";
        protected override string About => Description(typeof(FirableLoadout));

        protected override void Extras(VisualElement root)
        {
            var entries = FirableDamage.Read<FirableLoadout.SlotEntry[]>(target, "startingLoadout") ?? Array.Empty<FirableLoadout.SlotEntry>();
            foreach(var entry in entries.Where(e => e.Firable != null))
            {
                var firable = entry.Firable;
                var line = new VisualElement();
                line.AddToClassList("cg-inspector__row");
                string mode = BlockTypes.DisplayName((firable.Mode ?? new SemiAutoFire()).GetType());
                line.Add(CombatEditorStyles.Text($"{(entry.Slot != null ? StudioKinds.NameOf(entry.Slot) + ": " : string.Empty)}{StudioKinds.NameOf(firable)} · {mode}", "cg-inspector__row-name"));
                line.Add(Small("Studio", () => CombatStudioWindow.Open(firable)));
                line.Add(Small("Lab", () => FirableLabWindow.Open(firable)));
                if(!IsAsset) line.Add(Small("Range", () =>
                {
                    Selection.activeGameObject = Owner;
                    RangeGizmos.Show(null);
                }));
                root.Add(line);
            }
        }

        protected override void Live(VisualElement root)
        {
            foreach(var pair in ((FirableLoadout)target).Equipped)
            {
                var row = new VisualElement();
                row.AddToClassList("cg-inspector__row");
                row.Add(CombatEditorStyles.Text($"{StudioKinds.NameOf(pair.Key)}: {StudioKinds.NameOf(pair.Value.Definition)}", "cg-inspector__row-name"));
                var value = CombatEditorStyles.Text(null, "cg-inspector__row-value");
                row.Add(value);
                root.Add(row);
                rows.Add((value, pair.Value));
            }
            if(rows.Count == 0) root.Add(CombatEditorStyles.Text("Nothing equipped", "cg-empty-note"));
            Refresh();
        }

        protected override void Refresh()
        {
            foreach(var (value, firable) in rows)
            {
                value.text = CombatMonitorWindow.FirableState(firable);
            }
        }
    }

    [CustomEditor(typeof(UnitViewBinder)), CanEditMultipleObjects]
    public sealed class UnitViewBinderEditor : CombatInspector
    {
        protected override string Style => "cg-kind-view";
        protected override string About => Description(typeof(UnitViewBinder));

        protected override void Extras(VisualElement root)
        {
            var views = Owner.GetComponentsInChildren<IUnitView>(true).OfType<Component>().ToList();
            if(views.Count == 0)
            {
                root.Add(CombatEditorStyles.Text("No IUnitView components in this object or its children yet", "cg-empty-note"));
                return;
            }
            root.Add(CombatEditorStyles.Text(views.Count == 1 ? "Binds 1 view" : $"Binds {views.Count} views", "cg-section-title"));
            foreach(var view in views)
            {
                var each = view;
                var row = new VisualElement();
                row.AddToClassList("cg-inspector__row");
                row.Add(CombatEditorStyles.Text(CombatTools.PathOfComponent(view), "cg-inspector__row-name"));
                row.Add(Small("Select", () => Selection.activeObject = each.gameObject));
                root.Add(row);
            }
        }
    }

    [CustomEditor(typeof(Hitbox)), CanEditMultipleObjects]
    public sealed class HitboxEditor : CombatInspector
    {
        protected override string Style => "cg-kind-tag";
        protected override string About => "A part of a unit that takes extra damage. Hitbox Multiplier steps in the damage pipeline read its multiplier, and Hit Zone conditions read its zone.";

        protected override void Extras(VisualElement root)
        {
            var hitbox = (Hitbox)target;
            var owner = hitbox.Owner != null ? hitbox.Owner : hitbox.GetComponentInParent<Unit>();
            root.Add(CombatEditorStyles.Text(owner != null ? $"Belongs to {owner.DisplayName}{(hitbox.Owner == null ? " (found from the parent when it wakes up)" : string.Empty)}" : "No Unit in the parents, so hits here won't find a unit", "cg-note"));
            root.Add(CombatEditorStyles.Text($"Hits here deal ×{hitbox.DamageMultiplier:0.##} with a Hitbox Multiplier step", "cg-note"));
            if(hitbox.TryGetComponent<Collider>(out _)) return;
            var box = new HelpBox("No collider on this object, so queries can't hit it", HelpBoxMessageType.Warning);
            box.Add(Small("Add Sphere Collider", () => Undo.AddComponent<SphereCollider>(hitbox.gameObject)));
            root.Add(box);
        }
    }

    internal static class ProfileSection
    {
        public static void Add(VisualElement root, SerializedObject serializedObject, string field, string title)
        {
            var holder = new VisualElement();
            root.Add(holder);
            var profileProperty = serializedObject.FindProperty("profile");

            void Build(SerializedProperty property)
            {
                holder.Clear();
                var profile = property?.objectReferenceValue as UnitStatProfile;
                if(profile == null) return;
                var users = UnitLibrary.UsersOf(profile);
                var foldout = new Foldout { text = title, value = false, viewDataKey = $"cg-inspector-{field}" };
                foldout.AddToClassList("cg-section");
                var shared = CombatEditorStyles.Chip(users.Count <= 1 ? "Only this unit" : $"Shared by {users.Count} units");
                shared.tooltip = string.Join("\n", users.Select(StudioKinds.NameOf));
                foldout.Add(shared);
                var profileObject = new SerializedObject(profile);
                var list = new PropertyField(profileObject.FindProperty(field));
                list.Bind(profileObject);
                foldout.Add(list);
                holder.Add(foldout);
            }

            if(profileProperty == null) return;
            Build(profileProperty);
            holder.TrackPropertyValue(profileProperty, Build);
        }
    }
}
