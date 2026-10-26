using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class UnitOverview : VisualElement
    {
        private const string k_Preview = "Units.Preview";
        private const string k_Problems = "Units.Problems";
        private const string k_Identity = "Units.Identity";
        private const string k_Stats = "Units.Stats";
        private const string k_ProfileFields = "Units.ProfileFields";
        private const string k_Health = "Units.Health";
        private const string k_Effects = "Units.Effects";
        private const string k_Loadout = "Units.Loadout";
        private const string k_Hitboxes = "Units.Hitboxes";
        private const string k_Parts = "Units.Parts";
        private const string k_Others = "Units.Others";
        private const string k_PartDescriptions = "Units.PartDescriptions";
        private const int k_PreviewTries = 40;

        private readonly UnitCreatorWindow window;
        private readonly ScrollView scroll;
        private readonly VisualElement watchers;
        private readonly List<SerializedObject> bound = new();
        private GameObject prefab;
        private string structure;
        private Label title;
        private Label subtitle;
        private VisualElement chips;
        private VisualElement problems;

        public UnitOverview(UnitCreatorWindow window)
        {
            this.window = window;
            AddToClassList("cg-detail");
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cg-detail__scroll");
            scroll.contentContainer.AddToClassList("cg-detail__content");
            Add(scroll);
            watchers = new VisualElement();
            watchers.AddToClassList("cg-watcher");
            Add(watchers);
        }

        public static void Options(GenericMenu menu, string prefix)
        {
            StudioPrefs.Toggle(menu, prefix + "Preview", k_Preview, true);
            StudioPrefs.Toggle(menu, prefix + "Problems", k_Problems, true);
            StudioPrefs.Toggle(menu, prefix + "Identity", k_Identity, true);
            StudioPrefs.Toggle(menu, prefix + "Stats and Resources", k_Stats, true);
            StudioPrefs.Toggle(menu, prefix + "Profile Fields", k_ProfileFields, true);
            StudioPrefs.Toggle(menu, prefix + "Health", k_Health, true);
            StudioPrefs.Toggle(menu, prefix + "Status Effects", k_Effects, true);
            StudioPrefs.Toggle(menu, prefix + "Loadout", k_Loadout, true);
            StudioPrefs.Toggle(menu, prefix + "Hitboxes", k_Hitboxes, true);
            StudioPrefs.Toggle(menu, prefix + "Parts", k_Parts, true);
            StudioPrefs.Toggle(menu, prefix + "Part Descriptions", k_PartDescriptions, true);
            StudioPrefs.Toggle(menu, prefix + "Other Components", k_Others, true);
        }

        public void Show(GameObject target)
        {
            prefab = target;
            foreach(var each in bound)
            {
                each.Dispose();
            }
            bound.Clear();
            watchers.Clear();
            scroll.Clear();
            title = null;
            subtitle = null;
            chips = null;
            problems = null;

            if(prefab == null || !prefab.TryGetComponent<Unit>(out var unit))
            {
                scroll.Add(CombatEditorStyles.Text("Pick a unit on the left, or make one with New Unit", "cg-placeholder"));
                return;
            }

            structure = Structure();
            scroll.Add(HeaderCard());
            problems = new VisualElement();
            problems.AddToClassList("cg-problems");
            scroll.Add(problems);

            if(StudioPrefs.Get(k_Identity, true)) scroll.Add(IdentityCard(unit));
            var stats = prefab.GetComponent<StatsComponent>();
            var resources = prefab.GetComponent<ResourceComponent>();
            if(StudioPrefs.Get(k_Stats, true) && (stats != null || resources != null)) scroll.Add(StatsCard(stats, resources));
            var health = prefab.GetComponent<HealthComponent>();
            if(StudioPrefs.Get(k_Health, true) && health != null) scroll.Add(HealthCard(health));
            var effects = prefab.GetComponent<StatusEffectsComponent>();
            if(StudioPrefs.Get(k_Effects, true) && effects != null) scroll.Add(ComponentCard("Status Effects", "cg-kind-effect", "effects", effects, "innateEffects"));
            var loadout = prefab.GetComponent<FirableLoadout>();
            if(StudioPrefs.Get(k_Loadout, true) && loadout != null) scroll.Add(ComponentCard("Loadout", "cg-kind-firable", "loadout", loadout, "startingLoadout"));
            if(StudioPrefs.Get(k_Hitboxes, true)) scroll.Add(HitboxCard());
            if(StudioPrefs.Get(k_Parts, true)) scroll.Add(PartsCard());
            if(StudioPrefs.Get(k_Others, true)) AddOthers();
            Refresh();
        }

        public void Refresh()
        {
            if(prefab == null) return;
            if(title != null) title.text = StudioKinds.NameOf(prefab);
            if(subtitle != null) subtitle.text = AssetDatabase.GetAssetPath(prefab);
            if(chips != null)
            {
                chips.Clear();
                var faction = UnitLibrary.FactionOf(prefab);
                if(faction != null)
                {
                    var chip = CombatEditorStyles.Chip(StudioKinds.NameOf(faction), "cg-chip--faction");
                    chip.style.borderLeftColor = faction.Color;
                    chips.Add(chip);
                }
                int parts = UnitParts.Count(UnitParts.Present(prefab));
                chips.Add(CombatEditorStyles.Chip(parts == 1 ? "1 part" : $"{parts} parts"));
                int hitboxes = prefab.GetComponentsInChildren<Hitbox>(true).Length;
                if(hitboxes > 0) chips.Add(CombatEditorStyles.Chip(hitboxes == 1 ? "1 hitbox" : $"{hitboxes} hitboxes"));
                var health = UnitLibrary.ResourceOf(UnitLibrary.ResourceProfileOf(prefab), UnitLibrary.HealthOf(prefab));
                if(health != null) chips.Add(CombatEditorStyles.Chip($"{UnitLibrary.MaxOf(UnitLibrary.ProfileOf(prefab), health):0.##} {StudioKinds.NameOf(health.Type)}"));
            }

            if(problems == null) return;
            problems.Clear();
            if(!StudioPrefs.Get(k_Problems, true)) return;
            foreach(var issue in UnitChecks.Of(prefab))
            {
                var box = new HelpBox(issue.Message, issue.Severity);
                box.AddToClassList("cg-problem");
                if(issue.Fix != null)
                {
                    var fix = issue.Fix;
                    var button = new Button(() =>
                    {
                        fix();
                        window.AfterUnitEdit(prefab);
                    }) { text = issue.FixLabel };
                    button.AddToClassList("cg-problem__fix");
                    box.Add(button);
                }
                problems.Add(box);
            }
        }

        private VisualElement HeaderCard()
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-unit-header");
            card.AddToClassList("cg-kind-unit");

            var row = new VisualElement();
            row.AddToClassList("cg-unit-header__row");
            if(StudioPrefs.Get(k_Preview, true))
            {
                var preview = new Image { scaleMode = ScaleMode.ScaleToFit };
                preview.AddToClassList("cg-unit-header__preview");
                LoadPreview(preview, 0);
                row.Add(preview);
            }

            var texts = new VisualElement();
            texts.AddToClassList("cg-unit-header__texts");
            texts.Add(CombatEditorStyles.Text("UNIT", "cg-card__chip"));
            title = CombatEditorStyles.Text(null, "cg-card__title");
            texts.Add(title);
            subtitle = CombatEditorStyles.Text(null, "cg-card__summary");
            texts.Add(subtitle);
            chips = new VisualElement();
            chips.AddToClassList("cg-chips");
            texts.Add(chips);
            row.Add(texts);
            card.Add(row);

            var actions = new VisualElement();
            actions.AddToClassList("cg-unit-header__actions");
            actions.Add(Action("Open Prefab", () => AssetDatabase.OpenAsset(prefab)));
            actions.Add(Action("Ping", () => EditorGUIUtility.PingObject(prefab)));
            actions.Add(Action("Place in Scene", () =>
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Place Unit");
                Selection.activeGameObject = instance;
            }));
            actions.Add(Action("Test Damage", () => PipelineTesterWindow.OpenWithDefender(prefab)));
            actions.Add(Action("Copy to New Unit", () => window.StartFrom(prefab)));
            var delete = Action("Delete", () => window.DeleteUnit(prefab));
            delete.AddToClassList("cg-card__button--danger");
            delete.SetEnabled(AssetDeletion.CanDelete(prefab));
            actions.Add(delete);
            card.Add(actions);
            return card;
        }

        private void LoadPreview(Image image, int tries)
        {
            var texture = AssetPreview.GetAssetPreview(prefab);
            image.image = texture != null ? texture : AssetPreview.GetMiniThumbnail(prefab);
            if(texture == null && tries < k_PreviewTries) image.schedule.Execute(() => LoadPreview(image, tries + 1)).ExecuteLater(150);
        }

        private VisualElement IdentityCard(Unit unit)
        {
            var card = Card("Identity", "cg-kind-unit", "identity", out var content);
            var serialized = Track(unit);
            content.Add(Prop(serialized, "displayName", "Display Name"));
            content.Add(Prop(serialized, "faction"));
            content.Add(Prop(serialized, "startingTags"));
            content.Add(Prop(serialized, "spawnOnStart"));
            return card;
        }

        private VisualElement StatsCard(StatsComponent stats, ResourceComponent resources)
        {
            var card = Card("Stats and Resources", "cg-kind-stat", "stats", out var content);
            var statProfile = UnitLibrary.Read<UnitStatProfile>(stats, "profile");
            var resourceProfile = UnitLibrary.Read<UnitStatProfile>(resources, "profile");
            if(stats != null) content.Add(Prop(Track(stats), "profile", resources != null && statProfile == resourceProfile ? "Profile" : "Stat Profile"));
            if(resources != null && (stats == null || statProfile != resourceProfile)) content.Add(Prop(Track(resources), "profile", "Resource Profile"));

            var profile = statProfile ?? resourceProfile;
            if(profile == null)
            {
                content.Add(CombatEditorStyles.Text("No profile yet. Pick one above, or create one from the Project window", "cg-empty-note"));
                return card;
            }

            var users = UnitLibrary.UsersOf(profile);
            var row = new VisualElement();
            row.AddToClassList("cg-chips");
            var shared = CombatEditorStyles.Chip(users.Count <= 1 ? "Only this unit" : $"Shared by {users.Count} units");
            shared.tooltip = string.Join("\n", users.Select(StudioKinds.NameOf));
            row.Add(shared);
            row.Add(Action("Select Profile", () => Selection.activeObject = profile));
            if(users.Count > 1) row.Add(Action("Make Unique", () =>
            {
                UnitEdits.MakeProfileUnique(prefab);
                window.AfterUnitEdit(prefab);
            }));
            content.Add(row);

            if(!StudioPrefs.Get(k_ProfileFields, true)) return card;
            var profileSerialized = Track(profile);
            content.Add(Prop(profileSerialized, "Stats"));
            if(resourceProfile == null || resourceProfile == profile) content.Add(Prop(profileSerialized, "Resources"));
            else content.Add(Prop(Track(resourceProfile), "Resources"));
            return card;
        }

        private VisualElement HealthCard(HealthComponent health)
        {
            var card = Card("Health", "cg-kind-damage", "health", out var content);
            var serialized = Track(health);
            content.Add(Prop(serialized, "healthType"));
            content.Add(Prop(serialized, "pipeline"));
            content.Add(Prop(serialized, "deadTag"));

            var row = new VisualElement();
            row.AddToClassList("cg-chips");
            var pipeline = UnitLibrary.PipelineOf(prefab);
            if(pipeline != null)
            {
                int steps = pipeline.Steps?.Length ?? 0;
                row.Add(CombatEditorStyles.Chip(steps == 1 ? "1 step" : $"{steps} steps"));
                row.Add(Action("Edit Pipeline", () => CombatStudioWindow.Open(pipeline)));
            }
            row.Add(Action("Test Damage", () => PipelineTesterWindow.OpenWithDefender(prefab)));
            content.Add(row);
            return card;
        }

        private VisualElement ComponentCard(string name, string style, string key, Component component, params string[] fields)
        {
            var card = Card(name, style, key, out var content);
            var serialized = Track(component);
            foreach(var field in fields)
            {
                content.Add(Prop(serialized, field));
            }
            return card;
        }

        private VisualElement HitboxCard()
        {
            var card = Card("Hitboxes", "cg-kind-tag", "hitboxes", out var content);
            var hitboxes = prefab.GetComponentsInChildren<Hitbox>(true);
            if(hitboxes.Length == 0) content.Add(CombatEditorStyles.Text("No hitboxes. Hits land on the unit's colliders with a x1 multiplier", "cg-empty-note"));
            foreach(var hitbox in hitboxes)
            {
                var serialized = Track(hitbox);
                var row = new VisualElement();
                row.AddToClassList("cg-hitbox-row");
                var name = new Button(() => Selection.activeObject = hitbox.gameObject) { text = AnimationUtility.CalculateTransformPath(hitbox.transform, prefab.transform), tooltip = "Select this hitbox" };
                name.AddToClassList("cg-hitbox-row__name");
                row.Add(name);
                row.Add(InlineFields.Field(serialized.FindProperty("DamageMultiplier"), "multiplier"));
                row.Add(InlineFields.Field(serialized.FindProperty("Zone"), "zone"));
                content.Add(row);
            }
            var add = Action("Add Hitbox", () =>
            {
                UnitEdits.AddHitbox(prefab);
                window.AfterUnitEdit(prefab);
            });
            content.Add(add);
            return card;
        }

        private VisualElement PartsCard()
        {
            var card = Card("Parts", "cg-kind-definition", "parts", out var content);
            var present = UnitParts.Present(prefab);
            var grid = new VisualElement();
            grid.AddToClassList("cg-part-grid");
            foreach(var info in UnitParts.All)
            {
                bool has = (present & info.Part) != 0;
                var part = new VisualElement();
                part.AddToClassList("cg-part");
                part.EnableInClassList("cg-part--on", has);
                var head = new VisualElement();
                head.AddToClassList("cg-part__head");
                var icon = new BlockIcon();
                icon.SetStyle(info.Style);
                head.Add(icon);
                head.Add(CombatEditorStyles.Text(info.Label, "cg-part__title"));
                var each = info;
                var toggle = new Button(() =>
                {
                    if(has)
                    {
                        if(!EditorUtility.DisplayDialog("Remove Part", $"Remove {each.Label} from {prefab.name}? This saves the prefab and can't be undone.", "Remove", "Cancel")) return;
                        UnitEdits.RemovePart(prefab, each);
                    }
                    else UnitEdits.AddPart(prefab, each);
                    window.AfterUnitEdit(prefab);
                }) { text = has ? "Remove" : "Add" };
                toggle.AddToClassList("cg-part__button");
                head.Add(toggle);
                part.Add(head);
                if(StudioPrefs.Get(k_PartDescriptions, true)) part.Add(CombatEditorStyles.Text(info.Description, "cg-part__text"));
                grid.Add(part);
            }
            content.Add(grid);
            return card;
        }

        private void AddOthers()
        {
            var known = new HashSet<Type>(UnitParts.All.Select(p => p.Type)) { typeof(Unit), typeof(Transform), typeof(Rigidbody) };
            var others = prefab.GetComponents<Component>().Where(c => c != null && !known.Contains(c.GetType()) && !(c is Collider) && !(c is Renderer) && !(c is MeshFilter)).ToList();
            if(others.Count == 0) return;
            var card = Card("Other Components", "cg-kind-definition", "others", out var content);
            foreach(var component in others)
            {
                var line = new VisualElement();
                line.AddToClassList("cg-other-row");
                line.Add(new Image { image = AssetPreview.GetMiniThumbnail(component) });
                line.Add(CombatEditorStyles.Text(ObjectNames.NicifyVariableName(component.GetType().Name), "cg-other-row__name"));
                if(component is IUnitComponent) line.Add(CombatEditorStyles.Chip("unit component"));
                content.Add(line);
            }
            scroll.Add(card);
        }

        private VisualElement Card(string name, string style, string key, out VisualElement content)
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-unit-card");
            card.AddToClassList(style);
            var foldout = new Foldout { text = name, value = true, viewDataKey = "cg-unit-card-" + key };
            foldout.AddToClassList("cg-unit-card__foldout");
            var icon = new BlockIcon();
            icon.SetStyle(style);
            icon.AddToClassList("cg-unit-card__icon");
            var input = foldout.Q(className: Toggle.inputUssClassName);
            if(input != null) input.Insert(Mathf.Min(1, input.childCount), icon);
            card.Add(foldout);
            content = foldout.contentContainer;
            return card;
        }

        private SerializedObject Track(UnityEngine.Object target)
        {
            var serialized = new SerializedObject(target);
            bound.Add(serialized);
            var watcher = new VisualElement();
            watchers.Add(watcher);
            watcher.TrackSerializedObjectValue(serialized, _ =>
            {
                if(Structure() != structure) schedule.Execute(() => Show(prefab));
                else Refresh();
                window.RefreshList();
            });
            return serialized;
        }

        private string Structure()
        {
            if(prefab == null) return string.Empty;
            return string.Join("|", Id(UnitLibrary.Read<UnitStatProfile>(prefab.GetComponent<StatsComponent>(), "profile")),
                Id(UnitLibrary.Read<UnitStatProfile>(prefab.GetComponent<ResourceComponent>(), "profile")),
                Id(UnitLibrary.PipelineOf(prefab)), (int)UnitParts.Present(prefab), prefab.GetComponentsInChildren<Hitbox>(true).Length);
        }

        private static string Id(UnityEngine.Object target) => target != null ? AssetDatabase.GetAssetPath(target) + target.name : "-";

        private static PropertyField Prop(SerializedObject serialized, string path, string label = null)
        {
            var property = serialized.FindProperty(path);
            var field = label == null ? new PropertyField(property) : new PropertyField(property, label);
            field.Bind(serialized);
            return field;
        }

        private static Button Action(string text, System.Action run)
        {
            var button = new Button(run) { text = text };
            button.AddToClassList("cg-card__button");
            return button;
        }
    }
}
