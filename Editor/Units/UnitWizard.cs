using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class UnitWizard : VisualElement
    {
        private const string k_Blueprint = "blueprint";
        private const string k_Hints = "Units.WizardHints";
        private static readonly string[] k_Steps = { "Archetype", "Identity", "Stats", "Combat", "Parts", "Review" };

        private readonly UnitCreatorWindow window;
        private readonly SerializedObject serialized;
        private readonly VisualElement chips;
        private readonly ScrollView body;
        private readonly Button back;
        private readonly Button next;
        private readonly Button create;
        private readonly VisualElement watcher;

        public UnitWizard(UnitCreatorWindow window, SerializedObject serialized)
        {
            this.window = window;
            this.serialized = serialized;
            AddToClassList("cg-detail");
            AddToClassList("cg-wizard");

            chips = new VisualElement();
            chips.AddToClassList("cg-wizard__steps");
            Add(chips);

            body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList("cg-detail__scroll");
            body.contentContainer.AddToClassList("cg-detail__content");
            Add(body);

            var footer = new VisualElement();
            footer.AddToClassList("cg-wizard__footer");
            footer.Add(new Button(window.CancelWizard) { text = "Cancel" });
            footer.Add(new VisualElement { style = { flexGrow = 1 } });
            back = new Button(() => Go(window.Step - 1)) { text = "Back" };
            next = new Button(() => Go(window.Step + 1)) { text = "Next" };
            next.AddToClassList("cg-wizard__primary");
            create = new Button(Create) { text = "Create Unit" };
            create.AddToClassList("cg-wizard__primary");
            footer.Add(back);
            footer.Add(next);
            footer.Add(create);
            Add(footer);

            watcher = new VisualElement();
            watcher.AddToClassList("cg-watcher");
            Add(watcher);
            watcher.TrackSerializedObjectValue(serialized, _ => UpdateFooter());
        }

        private UnitBlueprint Blueprint => window.Blueprint;

        public static void Options(GenericMenu menu, string prefix)
        {
            StudioPrefs.Toggle(menu, prefix + "Hints", k_Hints, true);
        }

        public void Go(int step)
        {
            serialized.Update();
            window.Step = Mathf.Clamp(step, 0, k_Steps.Length - 1);
            Rebuild();
        }

        public void Rebuild()
        {
            serialized.Update();
            chips.Clear();
            for(int i = 0; i < k_Steps.Length; i++)
            {
                int index = i;
                var chip = new Button(() => Go(index)) { text = $"{i + 1}  {k_Steps[i]}" };
                chip.AddToClassList("cg-wizard__step");
                chip.EnableInClassList("cg-wizard__step--current", i == window.Step);
                chip.EnableInClassList("cg-wizard__step--done", i < window.Step);
                chips.Add(chip);
            }

            body.Clear();
            switch(window.Step)
            {
                case 0: ArchetypeStep(); break;
                case 1: IdentityStep(); break;
                case 2: StatsStep(); break;
                case 3: CombatStep(); break;
                case 4: PartsStep(); break;
                default: ReviewStep(); break;
            }
            body.contentContainer.Bind(serialized);
            UpdateFooter();
        }

        private void UpdateFooter()
        {
            bool last = window.Step >= k_Steps.Length - 1;
            back.SetEnabled(window.Step > 0);
            CombatEditorStyles.Show(next, !last);
            CombatEditorStyles.Show(create, last);
            create.SetEnabled(UnitBuilder.Errors(Blueprint).Count == 0);
        }

        private void ArchetypeStep()
        {
            Title("Start from", "Pick what kind of unit this is. You can change everything in the next steps.");
            var grid = new VisualElement();
            grid.AddToClassList("cg-archetypes");
            var cards = new List<VisualElement>();
            foreach(var archetype in UnitArchetypes.All)
            {
                var each = archetype;
                var card = new VisualElement();
                card.AddToClassList("cg-archetype");
                card.AddToClassList(each.Style);
                card.EnableInClassList("cg-archetype--selected", Blueprint.Archetype == each.Name);
                var head = new VisualElement();
                head.AddToClassList("cg-archetype__head");
                var icon = new BlockIcon();
                icon.SetStyle(each.Style);
                head.Add(icon);
                head.Add(CombatEditorStyles.Text(each.Name, "cg-archetype__title"));
                card.Add(head);
                card.Add(CombatEditorStyles.Text(each.Description, "cg-archetype__text"));
                if(StudioPrefs.Get(k_Hints, true))
                {
                    foreach(var line in UnitArchetypes.Contents(each))
                    {
                        card.Add(CombatEditorStyles.Text("· " + line, "cg-archetype__line"));
                    }
                }
                card.RegisterCallback<ClickEvent>(evt =>
                {
                    if(Blueprint.Archetype != each.Name)
                    {
                        Undo.RecordObject(window, "Pick Archetype");
                        each.Apply(Blueprint);
                        serialized.Update();
                    }
                    foreach(var other in cards)
                    {
                        other.EnableInClassList("cg-archetype--selected", other == card);
                    }
                    UpdateFooter();
                    if(evt.clickCount >= 2) Go(1);
                });
                cards.Add(card);
                grid.Add(card);
            }
            body.Add(grid);

            body.Add(CombatEditorStyles.Text("Or copy an existing unit", "cg-section-title"));
            var copy = new ObjectField("Unit Prefab") { objectType = typeof(GameObject), allowSceneObjects = false };
            copy.RegisterValueChangedCallback(evt =>
            {
                if(!(evt.newValue is GameObject prefab) || !UnitLibrary.IsUnit(prefab)) return;
                window.StartFrom(prefab);
            });
            body.Add(copy);
            Hint("Copies its parts, profile, pipeline, faction, tags, loadout and hitboxes. The new unit reuses the same profile and pipeline assets.");
        }

        private void IdentityStep()
        {
            Title("Identity", "What it's called and what it looks like.");
            body.Add(Field("FileName", "File Name"));
            body.Add(Field("DisplayName", "Display Name"));
            Hint("Leave Display Name empty to use the file name.");

            var faction = new VisualElement();
            faction.AddToClassList("cg-field-row");
            faction.Add(Field("Faction"));
            faction.Add(Small("New", () => CreateDefinition<Faction>("Faction", made =>
            {
                Blueprint.Faction = made;
                serialized.Update();
            })));
            body.Add(faction);
            body.Add(Field("Tags", "Starting Tags"));
            body.Add(Field("SpawnOnStart", "Spawn On Start"));
            Hint("Turn Spawn On Start off for units that are spawned by code or payloads, like summons.");

            body.Add(CombatEditorStyles.Text("Body", "cg-section-title"));
            var shape = Field("Body");
            body.Add(shape);
            var model = Field("Model");
            body.Add(model);
            body.Add(Field("Collider"));
            void Refresh(SerializedProperty property) => CombatEditorStyles.Show(model, property.enumValueIndex == (int)BodyShape.Model);
            var bodyProperty = serialized.FindProperty($"{k_Blueprint}.Body");
            Refresh(bodyProperty);
            model.TrackPropertyValue(bodyProperty, Refresh);
        }

        private void StatsStep()
        {
            Title("Stats and Resources", "Base values and pools like health or shields.");
            if(!Blueprint.Has(UnitPart.Stats) && !Blueprint.Has(UnitPart.Resources))
            {
                Note("This unit has no Stats or Resources part. Turn them on in Parts to give it stats.");
            }
            body.Add(Field("Profile", "Profile"));
            var existing = new VisualElement();
            existing.Add(Field("ExistingProfile", "Existing Profile"));
            var preview = CombatEditorStyles.Text(null, "cg-note");
            existing.Add(preview);
            body.Add(existing);

            var fresh = new VisualElement();
            fresh.Add(Field("Stats"));
            Hint("Pick an existing stat type, or leave it empty and type a name to make a new one.", fresh);
            fresh.Add(Field("Resources"));
            var created = CombatEditorStyles.Text(null, "cg-note");
            fresh.Add(created);
            body.Add(fresh);

            void Refresh(SerializedObject _)
            {
                serialized.Update();
                bool useExisting = Blueprint.Profile == ProfileMode.ExistingProfile;
                CombatEditorStyles.Show(existing, useExisting);
                CombatEditorStyles.Show(fresh, !useExisting);
                var profile = Blueprint.ExistingProfile;
                CombatEditorStyles.SetText(preview, profile == null ? null : $"{profile.Stats?.Length ?? 0} stats, {profile.Resources?.Length ?? 0} resources. Shared with {UnitLibrary.UsersOf(profile).Count} unit(s)");
                var plan = UnitBuilder.Plan(Blueprint).Where(f => f.Created && (f.Kind == "Stat Type" || f.Kind == "Resource Type")).Select(f => f.Label).ToList();
                CombatEditorStyles.SetText(created, plan.Count == 0 ? null : $"Creates {plan.Count} new type(s): {string.Join(", ", plan)}");
            }

            Refresh(null);
            existing.TrackSerializedObjectValue(serialized, Refresh);
        }

        private void CombatStep()
        {
            Title("Combat", "How it takes damage, what it starts with and what it carries.");
            if(Blueprint.Has(UnitPart.Health))
            {
                var labels = Blueprint.ResourceLabels();
                if(labels.Count == 0) Note("Add a resource on the Stats step for Health to use.");
                else
                {
                    if(!labels.Contains(Blueprint.HealthResource))
                    {
                        Blueprint.HealthResource = labels[0];
                        serialized.Update();
                    }
                    var health = new DropdownField("Health Pool", labels, Mathf.Max(0, labels.IndexOf(Blueprint.HealthResource)));
                    health.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(window, "Health Pool");
                        Blueprint.HealthResource = evt.newValue;
                        serialized.Update();
                    });
                    body.Add(health);
                }

                body.Add(Field("Pipeline", "Damage Pipeline"));
                var existing = Field("ExistingPipeline", "Pipeline");
                body.Add(existing);
                var names = StudioTemplates.Pipelines.Select(t => t.Name).ToList();
                var template = new DropdownField("Template", names, Mathf.Clamp(Blueprint.PipelineTemplate, 0, names.Count - 1));
                template.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(window, "Pipeline Template");
                    Blueprint.PipelineTemplate = names.IndexOf(evt.newValue);
                    serialized.Update();
                });
                body.Add(template);
                var wire = Field("WirePipeline", "Wire Stats");
                body.Add(wire);
                body.Add(Field("DeadTag", "Dead Tag"));

                void Refresh(SerializedProperty property)
                {
                    CombatEditorStyles.Show(existing, property.enumValueIndex == (int)PipelineMode.Existing);
                    CombatEditorStyles.Show(template, property.enumValueIndex == (int)PipelineMode.NewFromTemplate);
                    CombatEditorStyles.Show(wire, property.enumValueIndex == (int)PipelineMode.NewFromTemplate);
                }

                var pipelineProperty = serialized.FindProperty($"{k_Blueprint}.Pipeline");
                Refresh(pipelineProperty);
                existing.TrackPropertyValue(pipelineProperty, Refresh);
            }
            else Note("This unit has no Health part, so it can't take damage.");

            if(Blueprint.Has(UnitPart.StatusEffects))
            {
                body.Add(CombatEditorStyles.Text("Status Effects", "cg-section-title"));
                body.Add(Field("InnateEffects", "Innate Effects"));
                Hint("Applied on every spawn with the unit as the source. Good for passives.");
            }

            if(Blueprint.Has(UnitPart.Loadout))
            {
                body.Add(CombatEditorStyles.Text("Loadout", "cg-section-title"));
                body.Add(Field("Loadout"));
                Hint("Pick a slot, or type a name to make a new one. Double click a firable to open it in the Combat Studio.");
            }
        }

        private void PartsStep()
        {
            Title("Parts", "The components this unit is made of. Some parts need others and are switched on for you.");
            var grid = new VisualElement();
            grid.AddToClassList("cg-part-grid");
            foreach(var info in UnitParts.All)
            {
                var each = info;
                bool on = Blueprint.Has(each.Part);
                var part = new VisualElement();
                part.AddToClassList("cg-part");
                part.AddToClassList("cg-part--pickable");
                part.EnableInClassList("cg-part--on", on);
                var head = new VisualElement();
                head.AddToClassList("cg-part__head");
                var toggle = new Toggle { value = on };
                toggle.AddToClassList("cg-part__toggle");
                head.Add(toggle);
                var icon = new BlockIcon();
                icon.SetStyle(each.Style);
                head.Add(icon);
                head.Add(CombatEditorStyles.Text(each.Label, "cg-part__title"));
                part.Add(head);
                part.Add(CombatEditorStyles.Text(each.Description, "cg-part__text"));
                if(each.Requires != UnitPart.None) part.Add(CombatEditorStyles.Text("Needs " + UnitParts.Of(each.Requires)?.Label, "cg-part__needs"));
                part.RegisterCallback<ClickEvent>(_ =>
                {
                    Undo.RecordObject(window, "Toggle Part");
                    Blueprint.Parts = UnitParts.Toggle(Blueprint.Parts, each.Part, !Blueprint.Has(each.Part));
                    serialized.Update();
                    Rebuild();
                });
                grid.Add(part);
            }
            body.Add(grid);

            body.Add(CombatEditorStyles.Text("Hitboxes", "cg-section-title"));
            body.Add(Field("Hitboxes"));
            Hint("Each hitbox is a child with a sphere collider. Height is measured from the unit's feet.");
        }

        private void ReviewStep()
        {
            Title("Review", "Where everything goes. Nothing is made until you press Create Unit.");
            body.Add(FolderRow("Folder", "Folder"));
            body.Add(Field("OwnFolder", "Own Folder"));
            body.Add(FolderRow("TypesFolder", "Types Folder"));
            body.Add(Field("PlaceInScene", "Place in Scene"));

            var summary = new VisualElement();
            body.Add(summary);

            void Refresh(SerializedObject _)
            {
                summary.Clear();
                var b = Blueprint;
                foreach(var error in UnitBuilder.Errors(b))
                {
                    summary.Add(new HelpBox(error, HelpBoxMessageType.Error));
                }
                foreach(var warning in UnitBuilder.Warnings(b))
                {
                    summary.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
                }

                summary.Add(CombatEditorStyles.Text("Files", "cg-section-title"));
                foreach(var file in UnitBuilder.Plan(b))
                {
                    summary.Add(PlanRow(file));
                }

                summary.Add(CombatEditorStyles.Text("Components", "cg-section-title"));
                var parts = new VisualElement();
                parts.AddToClassList("cg-chips");
                parts.Add(CombatEditorStyles.Chip("Unit"));
                foreach(var info in UnitParts.All.Where(p => b.Has(p.Part)))
                {
                    parts.Add(CombatEditorStyles.Chip(info.Label));
                }
                if(b.Hitboxes.Count > 0) parts.Add(CombatEditorStyles.Chip(b.Hitboxes.Count == 1 ? "1 hitbox" : $"{b.Hitboxes.Count} hitboxes"));
                summary.Add(parts);
            }

            Refresh(null);
            summary.TrackSerializedObjectValue(serialized, Refresh);
        }

        private static VisualElement PlanRow(PlannedFile file)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-plan-row");
            var icon = new BlockIcon();
            icon.SetStyle(file.Style);
            row.Add(icon);
            row.Add(CombatEditorStyles.Text(file.Kind, "cg-plan-row__kind"));
            row.Add(CombatEditorStyles.Text(file.Label, "cg-plan-row__label"));
            row.Add(CombatEditorStyles.Chip(file.Created ? "new" : "existing", file.Created ? "cg-chip--new" : null));
            var path = CombatEditorStyles.Text(file.Path, "cg-plan-row__path");
            path.tooltip = file.Path;
            row.Add(path);
            return row;
        }

        private VisualElement FolderRow(string field, string label)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-field-row");
            row.Add(Field(field, label));
            row.Add(Small("...", () =>
            {
                var property = serialized.FindProperty($"{k_Blueprint}.{field}");
                string picked = EditorUtility.OpenFolderPanel(label, property.stringValue, string.Empty);
                string relative = DefinitionAssets.ProjectRelative(picked);
                if(relative == null) return;
                property.stringValue = relative;
                serialized.ApplyModifiedProperties();
            }));
            return row;
        }

        private void Create()
        {
            serialized.ApplyModifiedProperties();
            if(UnitBuilder.Errors(Blueprint).Count > 0)
            {
                UpdateFooter();
                return;
            }
            var prefab = UnitBuilder.Build(Blueprint);
            if(prefab != null) window.FinishWizard(prefab);
        }

        private void CreateDefinition<T>(string kind, Action<T> made) where T : ScriptableObject
        {
            string folder = UnitBuilder.TypesFolderFor(Blueprint);
            DefinitionAssets.EnsureFolder(folder);
            string path = EditorUtility.SaveFilePanelInProject($"New {kind}", kind, "asset", $"Choose where to save the new {kind.ToLowerInvariant()}", folder);
            if(string.IsNullOrEmpty(path)) return;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            DefinitionAssets.ResetId(asset, path);
            AssetDatabase.SaveAssets();
            Undo.RecordObject(window, $"New {kind}");
            made(asset);
        }

        private PropertyField Field(string name, string label = null)
        {
            var property = serialized.FindProperty($"{k_Blueprint}.{name}");
            return label == null ? new PropertyField(property) : new PropertyField(property, label);
        }

        private void Title(string text, string description)
        {
            body.Add(CombatEditorStyles.Text(text, "cg-wizard__title"));
            body.Add(CombatEditorStyles.Text(description, "cg-wizard__text"));
        }

        private void Hint(string text, VisualElement into = null)
        {
            if(StudioPrefs.Get(k_Hints, true)) (into ?? body).Add(CombatEditorStyles.Text(text, "cg-note"));
        }

        private void Note(string text) => body.Add(new HelpBox(text, HelpBoxMessageType.Info));

        private static Button Small(string text, Action run)
        {
            var button = new Button(run) { text = text };
            button.AddToClassList("cg-field-row__button");
            return button;
        }
    }
}
