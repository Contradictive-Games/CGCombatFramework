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
        private Label title;
        private VisualElement chips;
        private VisualElement problems;
        private VisualElement parts;
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
            card.Add(Row(
                prefab != null ? Small("Unit Creator", () => UnitCreatorWindow.Open(prefab), "Open this unit in the Unit Creator") : null,
                prefab != null ? Small("Test Damage", () => PipelineTesterWindow.OpenWithDefender(prefab), "Hit it in the Pipeline Tester") : null,
                ranges,
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
        }

        protected override void Extras(VisualElement root)
        {
            root.Add(CombatEditorStyles.Text("Parts", "cg-section-title"));
            parts = new VisualElement();
            parts.AddToClassList("cg-chips");
            root.Add(parts);
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

            if(parts == null) return;
            parts.Clear();
            var present = UnitParts.Present(gameObject);
            foreach(var info in UnitParts.All)
            {
                bool has = (present & info.Part) != 0;
                var each = info;
                var chip = new Button(() => AddPart(each)) { text = has ? each.Label : $"+ {each.Label}", tooltip = has ? each.Description : $"Add {each.Label}: {each.Description}" };
                chip.AddToClassList("cg-chip");
                chip.AddToClassList(has ? "cg-inspector__part--on" : "cg-inspector__part");
                chip.SetEnabled(!has);
                parts.Add(chip);
            }
        }

        private void AddPart(PartInfo info)
        {
            var gameObject = Owner;
            if(gameObject == null) return;
            if(EditorUtility.IsPersistent(gameObject)) UnitEdits.AddPart(gameObject.transform.root.gameObject, info);
            else
            {
                foreach(var other in UnitParts.All)
                {
                    if((info.Excludes & other.Part) == 0) continue;
                    var existing = gameObject.GetComponent(other.Type);
                    if(existing != null) Undo.DestroyObjectImmediate(existing);
                }
                Undo.AddComponent(gameObject, info.Type);
            }
            Summarize();
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
            state.text = unit.IsSpawned ? $"{(unit.IsAlive ? "Alive" : "Dead")} · {unit.Id}{(unit.Owner != null ? $" · owned by {unit.Owner.DisplayName}" : string.Empty)}" : "Not spawned";
            if(unit.TryGet<IHealth>(out var health) && health.Resource != null) SetBar(healthFill, healthValue, health.Resource);
            var active = unit.TryGet<IStatusEffects>(out var found) ? found.Active : null;
            effects.text = active == null || active.Count == 0 ? "No status effects" : "Effects: " + string.Join(", ", active.Select(e => $"{StudioKinds.NameOf(e.Definition)} ×{e.Stacks}"));
            var held = unit.Tags.All.Select(t => StudioKinds.NameOf(t)).ToList();
            tags.text = held.Count == 0 ? "No tags" : "Tags: " + string.Join(", ", held);
        }
    }
}
