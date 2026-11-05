using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    [CustomEditor(typeof(Unit)), CanEditMultipleObjects]
    public sealed class UnitEditor : CombatInspector
    {
        private const long k_RecheckMs = 150;

        private Debouncer recheck;
        private Label title;
        private VisualElement chips;
        private VisualElement problems;
        private VisualElement sequences;
        private VisualElement parts;
        private VisualElement customParts;
        private Label state;
        private VisualElement healthFill;
        private Label healthValue;
        private Label effects;
        private Label tags;

        protected override string Style => "cg-kind-unit";

        protected override void Header(VisualElement root)
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-kind-unit");
            card.Add(CombatEditorStyles.Text(IsAsset ? "UNIT PREFAB" : "UNIT", "cg-card__chip"));
            title = CombatEditorStyles.Text(null, "cg-card__title");
            card.Add(title);
            chips = new VisualElement();
            chips.AddToClassList("cg-chips");
            card.Add(chips);

            var prefab = PrefabOf(Owner);
            var ranges = !IsAsset ? Small(null, ToggleRanges) : null;
            Button partsMenu = null;
            partsMenu = Small("Parts ▾", () => ShowPartsMenu(partsMenu), "Go to or add this unit's parts");
            card.Add(Row(
                prefab != null ? Small("Combat Studio", () => CombatStudioWindow.OpenUnit(prefab), "Open this unit in the Combat Studio") : null,
                prefab != null ? Small("Test Damage", () => PipelineTesterWindow.OpenWithDefender(prefab), "Hit it in the Pipeline Tester") : null,
                ranges,
                partsMenu,
                Application.isPlaying ? Small("Combat Monitor", CombatMonitorWindow.Open) : null));
            root.Add(card);
            if(ranges != null) FollowRanges(ranges, () =>
            {
                ranges.text = RangeGizmos.On ? "Hide Ranges" : "Show Ranges";
                ranges.tooltip = RangeGizmos.On ? "Stop drawing ranges in the Scene view" : "Draw its loadout's ranges in the Scene view";
            });

            problems = new VisualElement();
            problems.AddToClassList("cg-problems");
            root.Add(problems);

            var watcher = new VisualElement();
            watcher.AddToClassList("cg-watcher");
            root.Add(watcher);
            watcher.TrackSerializedObjectValue(serializedObject, _ => Summarize());

            recheck = new Debouncer(root, k_RecheckMs, Summarize);
            root.RegisterCallback<AttachToPanelEvent>(_ => Watch(true));
            root.RegisterCallback<DetachFromPanelEvent>(_ => Watch(false));
        }

        private void Watch(bool on)
        {
            AssetChanges.ObjectsChanged -= Recheck;
            AssetChanges.Changed -= OnAssetsChanged;
            AssetChanges.Imported -= OnImported;
            if(!on) return;
            AssetChanges.ObjectsChanged += Recheck;
            AssetChanges.Changed += OnAssetsChanged;
            AssetChanges.Imported += OnImported;
        }

        private void Recheck() => recheck?.Poke();

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(AssetChanges.AnyCombat(guids)) Recheck();
        }

        private void OnImported(IReadOnlyCollection<string> paths)
        {
            if(AssetChanges.AnyCombatPath(paths)) Recheck();
        }

        protected override void Extras(VisualElement root)
        {
            root.Add(CombatEditorStyles.Text("Sequences", "cg-section-title"));
            sequences = new VisualElement();
            sequences.AddToClassList("cg-chips");
            root.Add(sequences);
            root.Add(CombatEditorStyles.Text("Parts", "cg-section-title"));
            parts = new VisualElement();
            parts.AddToClassList("cg-chips");
            root.Add(parts);
            if(UnitParts.Custom.Count > 0)
            {
                root.Add(CombatEditorStyles.Text("Custom Parts", "cg-section-title"));
                customParts = new VisualElement();
                customParts.AddToClassList("cg-chips");
                root.Add(customParts);
            }
            Summarize();
        }

        private void ToggleRanges()
        {
            if(!RangeGizmos.On) SelectOwner();
            RangeGizmos.SetOn(!RangeGizmos.On);
        }

        private void Summarize()
        {
            var gameObject = Owner;
            if(gameObject == null || title == null) return;
            var unit = (Unit)target;
            title.text = unit.DisplayName;

            chips.Clear();
            var faction = UnitLibrary.FactionOf(gameObject);
            if(faction != null)
            {
                var chip = CombatEditorStyles.Chip(StudioKinds.NameOf(faction), "cg-chip--faction");
                chip.style.borderLeftColor = faction.Color;
                chips.Add(chip);
            }
            var health = UnitLibrary.ResourceOf(UnitLibrary.ResourceProfileOf(gameObject), UnitLibrary.HealthOf(gameObject));
            if(health != null) chips.Add(CombatEditorStyles.Chip($"{UnitLibrary.MaxOf(UnitLibrary.ProfileOf(gameObject), health):0.##} {StudioKinds.NameOf(health.Type)}"));
            int hitboxes = gameObject.GetComponentsInChildren<Hitbox>(true).Length;
            if(hitboxes > 0) chips.Add(CombatEditorStyles.Chip(hitboxes == 1 ? "1 hitbox" : $"{hitboxes} hitboxes"));

            problems.Clear();
            foreach(var issue in UnitChecks.Of(gameObject))
            {
                var box = new HelpBox(issue.Message, issue.Severity);
                box.AddToClassList("cg-problem");
                if(issue.Fix != null && IsAsset)
                {
                    var fix = issue.Fix;
                    var button = new Button(() =>
                    {
                        fix();
                        Summarize();
                    }) { text = issue.FixLabel };
                    button.AddToClassList("cg-problem__fix");
                    box.Add(button);
                }
                problems.Add(box);
            }

            if(sequences != null)
            {
                sequences.Clear();
                var found = gameObject.GetComponentsInChildren<IArrivalSequence>(true).OfType<Component>().Select(c => (component: c, kind: "Arrival"))
                    .Concat(gameObject.GetComponentsInChildren<IDepartureSequence>(true).OfType<Component>().Select(c => (component: c, kind: "Departure")));
                foreach(var (component, kind) in found)
                {
                    var each = component;
                    string where = each.gameObject == gameObject ? string.Empty : $" on {each.gameObject.name}";
                    var chip = new Button(() => EditorGUIUtility.PingObject(each)) { text = $"{kind}: {ObjectNames.NicifyVariableName(each.GetType().Name)}{where}", tooltip = "Show this component" };
                    chip.AddToClassList("cg-chip");
                    sequences.Add(chip);
                }
                if(sequences.childCount == 0) sequences.Add(CombatEditorStyles.Text("None, so the unit arrives and departs at once", "cg-empty-note"));
            }

            if(parts == null) return;
            Chips(parts, gameObject, UnitParts.All);
            if(customParts != null) Chips(customParts, gameObject, UnitParts.Custom);
        }

        private void Chips(VisualElement into, GameObject gameObject, IEnumerable<PartInfo> infos)
        {
            into.Clear();
            foreach(var info in infos)
            {
                bool has = UnitParts.Has(gameObject, info);
                var each = info;
                string about = string.IsNullOrEmpty(each.Description) ? null : each.Description;
                if(has)
                {
                    var chip = CombatEditorStyles.Chip(each.Label, "cg-inspector__part--on");
                    chip.tooltip = about == null ? "Double click to go to it" : $"{about}\n\nDouble click to go to it";
                    Looks.AccentText(chip, each.Look.Color);
                    chip.RegisterCallback<ClickEvent>(evt =>
                    {
                        if(evt.clickCount == 2) GoTo(chip, each);
                    });
                    into.Add(chip);
                    continue;
                }
                var add = new Button(() => AddPart(each))
                {
                    text = $"+ {each.Label}",
                    tooltip = about == null ? $"Add {each.Label}" : $"Add {each.Label}: {about}"
                };
                add.AddToClassList("cg-chip");
                add.AddToClassList("cg-inspector__part");
                into.Add(add);
            }
        }

        private void GoTo(VisualElement from, PartInfo info)
        {
            var gameObject = Owner;
            if(gameObject == null) return;
            var found = UnitParts.Find(gameObject, info);
            if(found.Count > 0) Flash.RevealInInspector(from, found[0]);
        }

        private void ShowPartsMenu(VisualElement anchor)
        {
            var gameObject = Owner;
            if(gameObject == null) return;
            var menu = new GenericMenu();
            var present = UnitParts.PresentAll(gameObject);
            foreach(var info in present)
            {
                var each = info;
                menu.AddItem(new GUIContent("Go To/" + MenuPath(each)), false, () => GoTo(anchor, each));
            }
            if(present.Count == 0) menu.AddDisabledItem(new GUIContent("Go To"));
            bool added = false;
            foreach(var info in UnitParts.Every)
            {
                if(present.Contains(info)) continue;
                var each = info;
                menu.AddItem(new GUIContent("Add/" + MenuPath(each)), false, () => AddPart(each));
                added = true;
            }
            if(!added) menu.AddDisabledItem(new GUIContent("Add"));
            menu.DropDown(anchor.worldBound);
        }

        private static string MenuPath(PartInfo info)
        {
            if(!info.IsCustom) return info.Label;
            return string.IsNullOrEmpty(info.Group) ? $"Custom/{info.Label}" : $"Custom/{info.Group}/{info.Label}";
        }

        private void AddPart(PartInfo info)
        {
            var gameObject = Owner;
            if(gameObject == null) return;
            if(EditorUtility.IsPersistent(gameObject)) UnitEdits.AddPart(gameObject.transform.root.gameObject, info);
            else AddWithUndo(gameObject, info, new HashSet<PartInfo>());
            Summarize();
        }

        private static void AddWithUndo(GameObject gameObject, PartInfo info, HashSet<PartInfo> visiting)
        {
            if(!visiting.Add(info) || UnitParts.Has(gameObject, info)) return;
            foreach(var required in UnitParts.Requirements(info))
            {
                AddWithUndo(gameObject, required, visiting);
            }
            foreach(var other in UnitParts.All)
            {
                if((info.Excludes & other.Part) == 0) continue;
                var existing = gameObject.GetComponent(other.Type);
                if(existing != null) Undo.DestroyObjectImmediate(existing);
            }
            Undo.AddComponent(gameObject, info.Type);
        }

        protected override void Live(VisualElement root)
        {
            state = CombatEditorStyles.Text(null, "cg-card__summary");
            root.Add(state);
            root.Add(Bar(out healthFill, out healthValue, "Health", new Color(0.25f, 0.73f, 0.31f)));
            effects = CombatEditorStyles.Text(null, "cg-card__summary");
            root.Add(effects);
            tags = CombatEditorStyles.Text(null, "cg-card__summary");
            root.Add(tags);
            Refresh();
        }

        protected override void Refresh()
        {
            var unit = (Unit)target;
            state.text = unit.IsSpawned ? $"{(unit.IsAlive ? "Alive" : "Dead")}{(unit.IsArriving ? " · arriving" : string.Empty)} · {unit.Id}{(unit.Owner != null ? $" · owned by {unit.Owner.DisplayName}" : string.Empty)}" : unit.IsDeparting ? "Departing" : "Not spawned";
            if(unit.TryGet<IHealth>(out var health) && health.Resource != null) SetBar(healthFill, healthValue, health.Resource);
            var active = unit.TryGet<IStatusEffects>(out var found) ? found.Active : null;
            effects.text = active == null || active.Count == 0 ? "No status effects" : "Effects: " + string.Join(", ", active.Select(e => $"{StudioKinds.NameOf(e.Definition)} ×{e.Stacks}"));
            var held = unit.Tags.All.Select(t => StudioKinds.NameOf(t)).ToList();
            tags.text = held.Count == 0 ? "No tags" : "Tags: " + string.Join(", ", held);
        }
    }
}
