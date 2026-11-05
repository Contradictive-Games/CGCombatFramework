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
        private const string k_CustomParts = "Units.CustomParts";
        private const string k_PartFields = "Units.PartFields";
        private const string k_Others = "Units.Others";
        private const string k_PartDescriptions = "Units.PartDescriptions";
        private const string k_ClosedPrefix = "Units.Closed.";
        private const UnitPart k_OwnCards = UnitPart.Stats | UnitPart.Resources | UnitPart.Health | UnitPart.StatusEffects | UnitPart.Loadout;
        private const string k_PartsKey = "parts";
        private const string k_CustomPartsKey = "custom-parts";
        private const string k_FieldKey = "part:";

        private readonly StudioUnits page;
        private readonly ScrollView scroll;
        private readonly ScrollKeeper keeper;
        private readonly VisualElement watchers;
        private readonly List<Section> sections = new();
        private readonly List<SerializedObject> loose = new();
        private readonly HashSet<string> closed = new();
        private string closedKey;
        private Image preview;
        private GameObject prefab;
        private string structure;
        private Section building;
        private string anchorKey;
        private Label title;
        private Label subtitle;
        private VisualElement chips;
        private VisualElement problems;

        private sealed class Section
        {
            public readonly string Key;
            public readonly string Signature;
            public readonly List<SerializedObject> Bound = new();
            public readonly List<VisualElement> Watchers = new();
            public VisualElement Element;

            public Section(string key, string signature)
            {
                Key = key;
                Signature = signature;
            }
        }

        private readonly struct Planned
        {
            public readonly string Key;
            public readonly string Signature;
            public readonly Func<VisualElement> Build;

            public Planned(string key, string signature, Func<VisualElement> build)
            {
                Key = key;
                Signature = signature ?? string.Empty;
                Build = build;
            }
        }

        public UnitOverview(StudioUnits page)
        {
            this.page = page;
            AddToClassList("cg-detail");
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cg-detail__scroll");
            scroll.contentContainer.AddToClassList("cg-detail__content");
            Add(scroll);
            keeper = new ScrollKeeper(scroll);
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
            StudioPrefs.Toggle(menu, prefix + "Part Fields", k_PartFields, true);
            StudioPrefs.Toggle(menu, prefix + "Hitboxes", k_Hitboxes, true);
            StudioPrefs.Toggle(menu, prefix + "Parts", k_Parts, true);
            StudioPrefs.Toggle(menu, prefix + "Custom Parts", k_CustomParts, true);
            StudioPrefs.Toggle(menu, prefix + "Part Descriptions", k_PartDescriptions, true);
            StudioPrefs.Toggle(menu, prefix + "Other Components", k_Others, true);
        }

        public void Show(GameObject target)
        {
            bool same = target != null && target == prefab && sections.Count > 0;
            var hold = same ? Anchor(anchorKey) : null;
            anchorKey = null;
            prefab = target;
            if(!same)
            {
                Reset();
                LoadClosed();
            }

            if(prefab == null || !prefab.TryGetComponent<Unit>(out var unit))
            {
                Reset();
                scroll.Add(CombatEditorStyles.Text("Pick a unit on the left, or make one with New", "cg-placeholder"));
                return;
            }

            structure = Structure();
            Apply(Plan(unit));
            RefreshContent();
            if(same) ShowPreview();
            keeper.Hold(hold);
        }

        public void Refresh()
        {
            if(prefab == null) return;
            var hold = Anchor(null);
            RefreshContent();
            keeper.Hold(hold);
        }

        private void RefreshContent()
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
                int parts = UnitParts.CountAll(prefab);
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
                        page.AfterUnitEdit(prefab);
                    }) { text = issue.FixLabel };
                    button.AddToClassList("cg-problem__fix");
                    box.Add(button);
                }
                problems.Add(box);
            }
        }

        private List<Planned> Plan(Unit unit)
        {
            var plan = new List<Planned>
            {
                new("header", StudioPrefs.Get(k_Preview, true).ToString(), HeaderCard),
                new("problems", null, ProblemsBox)
            };
            if(StudioPrefs.Get(k_Identity, true)) plan.Add(new("identity", null, () => IdentityCard(unit)));

            var stats = prefab.GetComponent<StatsComponent>();
            var resources = prefab.GetComponent<ResourceComponent>();
            if(StudioPrefs.Get(k_Stats, true) && (stats != null || resources != null)) plan.Add(new("stats", StatsSignature(stats, resources), () => StatsCard(stats, resources)));
            var health = prefab.GetComponent<HealthComponent>();
            if(StudioPrefs.Get(k_Health, true) && health != null)
            {
                var pipeline = UnitLibrary.PipelineOf(prefab);
                plan.Add(new("health", $"{Id(pipeline)}|{pipeline?.Steps?.Length ?? 0}", () => HealthCard(health)));
            }
            var effects = prefab.GetComponent<StatusEffectsComponent>();
            if(StudioPrefs.Get(k_Effects, true) && effects != null) plan.Add(new("effects", null, () => ComponentCard("Status Effects", BuiltInStyles.Of(typeof(StatusEffectsComponent)), "effects", UnitParts.Of(UnitPart.StatusEffects), effects, "innateEffects")));
            var loadout = prefab.GetComponent<FirableLoadout>();
            if(StudioPrefs.Get(k_Loadout, true) && loadout != null) plan.Add(new("loadout", null, () => ComponentCard("Loadout", BuiltInStyles.Of(typeof(FirableLoadout)), "loadout", UnitParts.Of(UnitPart.Loadout), loadout, "startingLoadout")));

            if(StudioPrefs.Get(k_PartFields, true))
            {
                foreach(var (info, component) in FieldTargets())
                {
                    plan.Add(new(FieldKey(info, component), null, () => FieldCard(info, component)));
                }
            }

            if(StudioPrefs.Get(k_Hitboxes, true))
            {
                string hitboxes = string.Join(",", prefab.GetComponentsInChildren<Hitbox>(true).Select(h => AnimationUtility.CalculateTransformPath(h.transform, prefab.transform)));
                plan.Add(new("hitboxes", hitboxes, HitboxCard));
            }
            bool descriptions = StudioPrefs.Get(k_PartDescriptions, true);
            if(StudioPrefs.Get(k_Parts, true)) plan.Add(new(k_PartsKey, $"{(int)UnitParts.Present(prefab)}|{descriptions}", PartsCard));
            if(StudioPrefs.Get(k_CustomParts, true) && UnitParts.Custom.Count > 0)
            {
                string present = string.Join(",", UnitParts.PresentCustom(prefab).Select(p => p.Type.FullName));
                plan.Add(new(k_CustomPartsKey, $"{present}|{descriptions}", CustomPartsCard));
            }
            if(StudioPrefs.Get(k_Others, true)) plan.Add(new("others", string.Join(",", Others().Select(c => c.GetType().FullName)), OthersCard));
            return plan;
        }

        private void Apply(List<Planned> plan)
        {
            var old = new Dictionary<string, Section>();
            foreach(var section in sections)
            {
                old[section.Key] = section;
            }

            var next = new List<Section>();
            foreach(var each in plan)
            {
                if(old.TryGetValue(each.Key, out var existing))
                {
                    old.Remove(each.Key);
                    if(existing.Signature == each.Signature && existing.Bound.All(b => b.targetObject != null))
                    {
                        next.Add(existing);
                        continue;
                    }
                }
                var section = new Section(each.Key, each.Signature);
                building = section;
                try
                {
                    section.Element = each.Build();
                }
                finally
                {
                    building = null;
                }
                if(existing != null) Dispose(existing);
                next.Add(section);
            }
            foreach(var leftover in old.Values)
            {
                Dispose(leftover);
            }

            sections.Clear();
            sections.AddRange(next);
            int index = 0;
            foreach(var section in sections)
            {
                if(section.Element == null) continue;
                if(index >= scroll.childCount || scroll[index] != section.Element) scroll.Insert(index, section.Element);
                index++;
            }
            while(scroll.childCount > index)
            {
                scroll.RemoveAt(scroll.childCount - 1);
            }
        }

        private void Reset()
        {
            keeper.Release();
            foreach(var section in sections)
            {
                Dispose(section);
            }
            sections.Clear();
            foreach(var each in loose)
            {
                each.Dispose();
            }
            loose.Clear();
            watchers.Clear();
            scroll.Clear();
            title = null;
            subtitle = null;
            chips = null;
            problems = null;
            preview = null;
        }

        private static void Dispose(Section section)
        {
            foreach(var watcher in section.Watchers)
            {
                watcher.RemoveFromHierarchy();
            }
            foreach(var each in section.Bound)
            {
                each.Dispose();
            }
            section.Element?.RemoveFromHierarchy();
        }

        private void LoadClosed()
        {
            closed.Clear();
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            closedKey = string.IsNullOrEmpty(path) ? null : k_ClosedPrefix + AssetDatabase.AssetPathToGUID(path);
            if(closedKey == null) return;
            foreach(var key in StudioPrefs.GetString(closedKey).Split('\n'))
            {
                if(key.Length > 0) closed.Add(key);
            }
        }

        private void SetOpen(string key, bool open)
        {
            bool changed = open ? closed.Remove(key) : closed.Add(key);
            if(!changed || closedKey == null) return;
            if(closed.Count == 0) StudioPrefs.Remove(closedKey);
            else StudioPrefs.Store(closedKey, string.Join("\n", closed));
        }

        private void SetAllOpen(bool open, string anchor)
        {
            var hold = Anchor(anchor);
            foreach(var section in sections)
            {
                var foldout = CardFoldout(section.Element);
                if(foldout != null) foldout.value = open;
            }
            keeper.Hold(hold);
        }

        private static Foldout CardFoldout(VisualElement card) => card?.Q<Foldout>(className: "cg-unit-card__foldout");

        private void ShowPreview()
        {
            if(preview == null) return;
            var image = preview;
            var asset = prefab;
            AssetPreviews.Show(image, asset, texture =>
            {
                if(texture == null) image.image = AssetPreview.GetMiniThumbnail(asset);
            });
        }

        private Func<float> Anchor(string preferredKey)
        {
            Section anchor = null;
            if(preferredKey != null) anchor = sections.FirstOrDefault(s => s.Key == preferredKey && Laid(s.Element));
            if(anchor == null && keeper.Offset > 0f) anchor = sections.FirstOrDefault(s => Laid(s.Element) && s.Element.layout.yMax > keeper.Offset);
            if(anchor == null) return null;
            string key = anchor.Key;
            float viewportY = keeper.ViewportY(anchor.Element);
            return () =>
            {
                var section = sections.FirstOrDefault(s => s.Key == key);
                return section != null && Laid(section.Element) ? section.Element.layout.y - viewportY : float.NaN;
            };
        }

        private static bool Laid(VisualElement element) => element != null && element.panel != null && !float.IsNaN(element.layout.y);

        private VisualElement ProblemsBox()
        {
            problems = new VisualElement();
            problems.AddToClassList("cg-problems");
            return problems;
        }

        private VisualElement HeaderCard()
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-unit-header");
            card.AddToClassList(BuiltInStyles.Of(typeof(Unit)));

            var row = new VisualElement();
            row.AddToClassList("cg-unit-header__row");
            if(StudioPrefs.Get(k_Preview, true))
            {
                preview = new Image { scaleMode = ScaleMode.ScaleToFit };
                preview.AddToClassList("cg-unit-header__preview");
                row.Add(preview);
                ShowPreview();
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
            actions.Add(Action("Copy to New Unit", () => page.StartFrom(prefab)));
            Button parts = null;
            parts = Action("Parts ▾", () => ShowPartsMenu(parts.worldBound));
            parts.tooltip = "Go to, add or remove this unit's parts";
            actions.Add(parts);
            var delete = Action("Delete", () => page.DeleteUnit(prefab));
            delete.AddToClassList("cg-card__button--danger");
            delete.SetEnabled(AssetDeletion.CanDelete(prefab));
            actions.Add(delete);
            card.Add(actions);
            card.AddManipulator(new ContextualMenuManipulator(evt => CardMenu(evt, "header", null)));
            return card;
        }

        private void ShowPartsMenu(Rect rect)
        {
            if(prefab == null) return;
            var menu = new GenericMenu();
            foreach(var (path, run) in PartsMenu())
            {
                if(run == null) menu.AddDisabledItem(new GUIContent(path));
                else menu.AddItem(new GUIContent(path), false, () => run());
            }
            menu.DropDown(rect);
        }

        private List<(string path, Action run)> PartsMenu()
        {
            var items = new List<(string path, Action run)>();
            var present = UnitParts.PresentAll(prefab);
            foreach(var info in present)
            {
                items.Add(("Go To/" + MenuPath(info), () => RevealPart(info)));
            }
            if(present.Count == 0) items.Add(("Go To", null));
            int before = items.Count;
            foreach(var info in UnitParts.Every)
            {
                if(!present.Contains(info)) items.Add(("Add/" + MenuPath(info), () => TogglePart(info, false, null)));
            }
            if(items.Count == before) items.Add(("Add", null));
            foreach(var info in present)
            {
                items.Add(("Remove/" + MenuPath(info), () => TogglePart(info, true, null)));
            }
            if(present.Count == 0) items.Add(("Remove", null));
            return items;
        }

        private void CardMenu(ContextualMenuPopulateEvent evt, string key, IReadOnlyList<PartInfo> parts)
        {
            if(prefab == null) return;
            var menu = evt.menu;
            if(menu.MenuItems().Count > 0) menu.AppendSeparator();

            PartInfo tile = null;
            for(var each = evt.target as VisualElement; each != null && tile == null; each = each.parent)
            {
                if(each.userData is PartInfo info && each.ClassListContains("cg-part")) tile = info;
            }
            bool removable = false;
            if(tile != null)
            {
                bool has = UnitParts.Has(prefab, tile);
                if(has) menu.AppendAction($"Go To {tile.Label}", _ => RevealPart(tile));
                menu.AppendAction(has ? $"Remove {tile.Label}..." : $"Add {tile.Label}", _ => TogglePart(tile, has, has ? null : key));
                removable = true;
            }
            else if(parts != null)
            {
                foreach(var info in parts)
                {
                    if(!UnitParts.Has(prefab, info)) continue;
                    menu.AppendAction($"Remove {info.Label}...", _ => TogglePart(info, true, null));
                    removable = true;
                }
            }
            if(removable) menu.AppendSeparator();
            menu.AppendAction("Collapse All", _ => SetAllOpen(false, key));
            menu.AppendAction("Expand All", _ => SetAllOpen(true, key));
            menu.AppendSeparator();
            foreach(var (path, run) in PartsMenu())
            {
                if(run == null) menu.AppendAction("Parts/" + path, null, DropdownMenuAction.Status.Disabled);
                else menu.AppendAction("Parts/" + path, _ => run());
            }
        }

        private static string MenuPath(PartInfo info)
        {
            if(!info.IsCustom) return info.Label;
            return string.IsNullOrEmpty(info.Group) ? $"Custom/{info.Label}" : $"Custom/{info.Group}/{info.Label}";
        }

        private void TogglePart(PartInfo info, bool has, string anchor)
        {
            if(has)
            {
                if(!EditorUtility.DisplayDialog("Remove Part", RemoveMessage(info), "Remove", "Cancel")) return;
                anchorKey = anchor;
                UnitEdits.RemovePart(prefab, info);
            }
            else
            {
                anchorKey = anchor;
                UnitEdits.AddPart(prefab, info);
            }
            page.AfterUnitEdit(prefab);
        }

        private void RevealPart(PartInfo info)
        {
            var target = CardFor(info);
            if(target == null) return;
            var foldout = CardFoldout(target) ?? target.GetFirstAncestorOfType<Foldout>();
            if(foldout != null) foldout.value = true;
            keeper.Reveal(target);
            Flash.Play(target);
        }

        private VisualElement CardFor(PartInfo info)
        {
            string key = null;
            if((info.Part & (UnitPart.Stats | UnitPart.Resources)) != 0) key = "stats";
            else if(info.Part == UnitPart.Health) key = "health";
            else if(info.Part == UnitPart.StatusEffects) key = "effects";
            else if(info.Part == UnitPart.Loadout) key = "loadout";

            string prefix = k_FieldKey + info.Type.FullName + ":";
            var section = sections.FirstOrDefault(s => s.Element != null && (key != null ? s.Key == key : s.Key.StartsWith(prefix, StringComparison.Ordinal)));
            if(section != null) return section.Element;

            var parts = sections.FirstOrDefault(s => s.Key == (info.IsCustom ? k_CustomPartsKey : k_PartsKey))?.Element;
            return parts?.Query<VisualElement>(className: "cg-part").Where(e => e.userData == info).First();
        }

        private VisualElement IdentityCard(Unit unit)
        {
            var card = Card("Identity", BuiltInStyles.Of(typeof(Unit)), "identity", out var content);
            var serialized = Track(unit);
            content.Add(Prop(serialized, "displayName", "Display Name"));
            content.Add(Prop(serialized, "faction"));
            content.Add(Prop(serialized, "startingTags"));
            content.Add(Prop(serialized, "spawnOnStart"));
            return card;
        }

        private string StatsSignature(StatsComponent stats, ResourceComponent resources)
        {
            var statProfile = UnitLibrary.Read<UnitStatProfile>(stats, "profile");
            var resourceProfile = UnitLibrary.Read<UnitStatProfile>(resources, "profile");
            var profile = statProfile ?? resourceProfile;
            int users = profile != null ? UnitLibrary.UsersOf(profile).Count : 0;
            return $"{stats != null}|{resources != null}|{Id(statProfile)}|{Id(resourceProfile)}|{users}|{StudioPrefs.Get(k_ProfileFields, true)}";
        }

        private VisualElement StatsCard(StatsComponent stats, ResourceComponent resources)
        {
            var card = Card("Stats and Resources", BuiltInStyles.Of(typeof(StatsComponent)), "stats", out var content, default, new[] { UnitParts.Of(UnitPart.Stats), UnitParts.Of(UnitPart.Resources) });
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
                page.AfterUnitEdit(prefab);
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
            var card = Card("Health", BuiltInStyles.Of(typeof(HealthComponent)), "health", out var content, default, new[] { UnitParts.Of(UnitPart.Health) });
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

        private VisualElement ComponentCard(string name, string style, string key, PartInfo part, Component component, params string[] fields)
        {
            var card = Card(name, style, key, out var content, default, new[] { part });
            var serialized = Track(component);
            foreach(var field in fields)
            {
                content.Add(Prop(serialized, field));
            }
            return card;
        }

        private IEnumerable<(PartInfo info, Component component)> FieldTargets()
        {
            foreach(var info in UnitParts.All)
            {
                if((k_OwnCards & info.Part) != 0) continue;
                foreach(var component in UnitParts.Find(prefab, info))
                {
                    yield return (info, component);
                }
            }
            foreach(var info in UnitParts.PresentCustom(prefab))
            {
                foreach(var component in UnitParts.Find(prefab, info))
                {
                    yield return (info, component);
                }
            }
        }

        private string Where(Component component) => component.transform == prefab.transform ? null : AnimationUtility.CalculateTransformPath(component.transform, prefab.transform);

        private string FieldKey(PartInfo info, Component component) => $"{k_FieldKey}{info.Type.FullName}:{Where(component)}:{component.GetInstanceID()}";

        private VisualElement FieldCard(PartInfo info, Component component)
        {
            var serialized = new SerializedObject(component);
            var fields = BlockTypes.Children(serialized);
            if(fields.Count == 0)
            {
                serialized.Dispose();
                return null;
            }
            Watch(serialized);
            string where = Where(component);
            string heading = where == null ? info.Label : $"{info.Label} · {where}";
            var card = Card(heading, info.Style, $"part-{info.Type.FullName}-{where}", out var content, info.Look, new[] { info });
            foreach(var field in fields)
            {
                var property = new PropertyField(field);
                property.Bind(serialized);
                content.Add(property);
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
                anchorKey = "hitboxes";
                UnitEdits.AddHitbox(prefab);
                page.AfterUnitEdit(prefab);
            });
            content.Add(add);
            return card;
        }

        private VisualElement PartsCard()
        {
            var card = Card("Parts", "cg-kind-definition", k_PartsKey, out var content);
            var grid = new VisualElement();
            grid.AddToClassList("cg-part-grid");
            foreach(var info in UnitParts.All)
            {
                grid.Add(PartCard(info, false));
            }
            content.Add(grid);
            return card;
        }

        private VisualElement CustomPartsCard()
        {
            var card = Card("Custom Parts", PartInfo.k_CustomStyle, k_CustomPartsKey, out var content);
            foreach(var group in UnitParts.Groups(UnitParts.Custom))
            {
                if(group.Key != null) content.Add(CombatEditorStyles.Text(group.Key, "cg-part-group"));
                var grid = new VisualElement();
                grid.AddToClassList("cg-part-grid");
                foreach(var info in group)
                {
                    grid.Add(PartCard(info, true));
                }
                content.Add(grid);
            }
            return card;
        }

        private VisualElement PartCard(PartInfo info, bool needs)
        {
            bool has = UnitParts.Has(prefab, info);
            var look = info.Look;
            var part = new VisualElement { userData = info };
            part.AddToClassList("cg-part");
            if(info.IsCustom) part.AddToClassList(info.Style);
            part.EnableInClassList("cg-part--on", has);
            if(has)
            {
                Looks.Accent(part, look.Color);
                part.tooltip = "Double click to go to its card";
                part.RegisterCallback<ClickEvent>(evt =>
                {
                    if(evt.clickCount == 2 && !(evt.target is Button)) RevealPart(info);
                });
            }
            var head = new VisualElement();
            head.AddToClassList("cg-part__head");
            var icon = new BlockIcon();
            icon.SetStyle(info.Style);
            icon.SetLook(look);
            head.Add(icon);
            head.Add(CombatEditorStyles.Text(info.Label, "cg-part__title"));
            var toggle = new Button(() => TogglePart(info, has, info.IsCustom ? k_CustomPartsKey : k_PartsKey)) { text = has ? "Remove" : "Add" };
            toggle.AddToClassList("cg-part__button");
            head.Add(toggle);
            part.Add(head);
            if(!string.IsNullOrEmpty(info.Description) && StudioPrefs.Get(k_PartDescriptions, true)) part.Add(CombatEditorStyles.Text(info.Description, "cg-part__text"));
            string needed = needs ? UnitParts.NeedsLabel(info) : null;
            if(needed != null) part.Add(CombatEditorStyles.Text(needed, "cg-part__needs"));
            return part;
        }

        private string RemoveMessage(PartInfo info)
        {
            var message = new System.Text.StringBuilder($"Remove {info.Label} from {prefab.name}?");
            if(info.InChildren)
            {
                var locations = UnitParts.Locations(prefab, info);
                if(locations.Count > 1) message.Append($" This removes all {locations.Count} copies, on {Join(locations)}.");
                else if(locations.Count == 1 && locations[0] != prefab.name) message.Append($" It's on {locations[0]}.");
            }
            var dependents = UnitParts.Dependents(prefab, info);
            if(dependents.Count > 0) message.Append($" This also removes {Join(dependents.Select(d => d.Label).ToList())}, which need it.");
            message.Append(" This saves the prefab and can't be undone.");
            return message.ToString();
        }

        private static string Join(List<string> items)
        {
            if(items.Count <= 1) return items.Count == 1 ? items[0] : string.Empty;
            return string.Join(", ", items.Take(items.Count - 1)) + " and " + items[items.Count - 1];
        }

        private List<Component> Others()
        {
            var known = new HashSet<Type>(UnitParts.All.Select(p => p.Type)) { typeof(Unit), typeof(Transform), typeof(Rigidbody) };
            known.UnionWith(UnitParts.Custom.Select(p => p.Type));
            return prefab.GetComponents<Component>().Where(c => c != null && !known.Contains(c.GetType()) && !(c is Collider) && !(c is Renderer) && !(c is MeshFilter)).ToList();
        }

        private VisualElement OthersCard()
        {
            var others = Others();
            if(others.Count == 0) return null;
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
            return card;
        }

        private VisualElement Card(string name, string style, string key, out VisualElement content, Look look = default, IReadOnlyList<PartInfo> parts = null)
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-unit-card");
            card.AddToClassList(style);
            Looks.Accent(card, look.Color);
            var foldout = new Foldout { text = name, value = !closed.Contains(key) };
            foldout.AddToClassList("cg-unit-card__foldout");
            foldout.RegisterValueChangedCallback(evt =>
            {
                if(evt.target == foldout) SetOpen(key, evt.newValue);
            });
            card.AddManipulator(new ContextualMenuManipulator(evt => CardMenu(evt, key, parts)));
            var icon = new BlockIcon();
            icon.SetStyle(style);
            icon.SetLook(look);
            icon.AddToClassList("cg-unit-card__icon");
            var input = foldout.Q(className: Toggle.inputUssClassName);
            if(input != null) input.Insert(Mathf.Min(1, input.childCount), icon);
            card.Add(foldout);
            content = foldout.contentContainer;
            return card;
        }

        private SerializedObject Track(UnityEngine.Object target) => Watch(new SerializedObject(target));

        private SerializedObject Watch(SerializedObject serialized)
        {
            var watcher = new VisualElement();
            watchers.Add(watcher);
            if(building != null)
            {
                building.Bound.Add(serialized);
                building.Watchers.Add(watcher);
            }
            else loose.Add(serialized);
            watcher.TrackSerializedObjectValue(serialized, _ =>
            {
                if(Structure() != structure) schedule.Execute(() => Show(prefab));
                else Refresh();
                UnitChecks.Forget(prefab);
                page.RefreshList();
            });
            return serialized;
        }

        private string Structure()
        {
            if(prefab == null) return string.Empty;
            return string.Join("|", Id(UnitLibrary.Read<UnitStatProfile>(prefab.GetComponent<StatsComponent>(), "profile")),
                Id(UnitLibrary.Read<UnitStatProfile>(prefab.GetComponent<ResourceComponent>(), "profile")),
                Id(UnitLibrary.PipelineOf(prefab)), (int)UnitParts.Present(prefab), prefab.GetComponentsInChildren<Hitbox>(true).Length,
                string.Join(",", UnitParts.PresentCustom(prefab).Select(p => p.Type.Name)));
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
