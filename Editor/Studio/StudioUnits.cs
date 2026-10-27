using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    [Serializable]
    internal sealed class UnitSession
    {
        public GameObject Selected;
        public bool Creating;
        public int Step;
        public UnitBlueprint Blueprint = new();
    }

    internal sealed class StudioUnits
    {
        private enum Subtitle { Path, Faction, Parts, Health }

        private const string k_Compact = "Units.Compact";
        private const string k_Previews = "Units.Previews";
        private const string k_Subtitle = "Units.Subtitle";
        private const string k_Badges = "Units.Badges";
        private const float k_CozyHeight = 38f;
        private const float k_CompactHeight = 20f;

        private readonly CombatStudioWindow window;
        private readonly string blueprintPath;
        private readonly List<GameObject> units = new();
        private SerializedObject serializedWindow;
        private ListView list;
        private VisualElement listEmpty;
        private UnitOverview overview;
        private UnitWizard wizard;
        private Faction factionFilter;
        private UnitPart partFilter;
        private bool problemsOnly;
        private bool refreshQueued;

        public StudioUnits(CombatStudioWindow window, string sessionPath)
        {
            this.window = window;
            blueprintPath = $"{sessionPath}.{nameof(UnitSession.Blueprint)}";
            serializedWindow = new SerializedObject(window);
        }

        public UnitBlueprint Blueprint => Session.Blueprint;

        public int Step
        {
            get => Session.Step;
            set => Session.Step = value;
        }

        public bool Filtered => factionFilter != null || partFilter != UnitPart.None || problemsOnly;

        private UnitSession Session => window.UnitSession;

        private static bool Compact => StudioPrefs.Get(k_Compact, false);

        public void Dispose()
        {
            serializedWindow?.Dispose();
            serializedWindow = null;
        }

        public VisualElement BuildList()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            var header = CombatEditorStyles.Header("Units");
            header.Add(CombatEditorStyles.DropButton("View", ShowViewMenu, "List and overview options"));
            pane.Add(header);

            list = new ListView
            {
                fixedItemHeight = k_CozyHeight,
                selectionType = SelectionType.Single,
                itemsSource = units,
                makeItem = MakeRow,
                bindItem = BindRow
            };
            list.AddToClassList("cg-asset-list");
            list.RegisterCallback<KeyDownEvent>(evt =>
            {
                bool delete = evt.keyCode == KeyCode.Delete || (evt.keyCode == KeyCode.Backspace && evt.actionKey);
                if(!delete || !(list.selectedItem is GameObject unit)) return;
                evt.StopPropagation();
                DeleteUnit(unit);
            });
            list.selectionChanged += selection =>
            {
                var unit = selection.FirstOrDefault() as GameObject;
                if(unit == null || (unit == Session.Selected && !Session.Creating)) return;
                Select(unit);
                ShowContent();
            };
            pane.Add(list);

            listEmpty = new VisualElement();
            listEmpty.AddToClassList("cg-outline-empty");
            listEmpty.Add(CombatEditorStyles.Text("No units found", "cg-outline-empty__title"));
            listEmpty.Add(CombatEditorStyles.Text("Want to make one?", "cg-outline-empty__text"));
            var make = new Button(() => StartWizard(null)) { text = "Create Unit" };
            make.AddToClassList("cg-outline-empty__create");
            listEmpty.Add(make);
            pane.Add(listEmpty);
            return pane;
        }

        public VisualElement BuildContent()
        {
            var content = new VisualElement();
            content.AddToClassList("cg-pane");
            overview = new UnitOverview(this);
            wizard = new UnitWizard(this, serializedWindow, blueprintPath);
            content.Add(overview);
            content.Add(wizard);
            return content;
        }

        public void Show()
        {
            if(list == null) return;
            ApplyListView();
            RefreshList();
            ShowContent();
        }

        public void Select(GameObject unit)
        {
            Session.Selected = unit;
            Session.Creating = false;
        }

        public void RecordUndo(string name) => Undo.RecordObject(window, name);

        public void StartWizard(UnitArchetype archetype)
        {
            RecordUndo("New Unit");
            var session = Session;
            session.Creating = true;
            session.Step = 0;
            session.Blueprint = Fresh(session.Blueprint);
            if(archetype != null)
            {
                archetype.Apply(session.Blueprint);
                session.Step = 1;
            }
            serializedWindow?.Update();
            ShowContent();
        }

        public void StartFrom(GameObject prefab)
        {
            RecordUndo("Copy Unit");
            var session = Session;
            session.Creating = true;
            session.Blueprint = Fresh(session.Blueprint);
            UnitArchetypes.CopyFrom(prefab, session.Blueprint);
            session.Step = 1;
            serializedWindow?.Update();
            ShowContent();
        }

        public void CancelWizard()
        {
            Session.Creating = false;
            ShowContent();
        }

        public void FinishWizard(GameObject prefab)
        {
            UnitLibrary.Clear();
            Select(prefab);
            RefreshList();
            ShowContent();
            EditorGUIUtility.PingObject(prefab);
        }

        public void DeleteUnit(GameObject unit)
        {
            if(unit == null || !AssetDeletion.Confirm(unit)) return;
            RefreshList();
            ShowContent();
        }

        public void AfterUnitEdit(GameObject prefab)
        {
            UnitLibrary.Clear();
            Session.Selected = prefab;
            RefreshList();
            if(!Session.Creating) overview?.Show(prefab);
        }

        public void RefreshList()
        {
            if(list == null) return;
            var terms = StudioIndex.Terms(window.Query);
            var before = units.ToList();
            units.Clear();
            foreach(var unit in UnitLibrary.All)
            {
                if(unit == null) continue;
                if(factionFilter != null && UnitLibrary.FactionOf(unit) != factionFilter) continue;
                if(partFilter != UnitPart.None && (UnitParts.Present(unit) & partFilter) == 0) continue;
                if(problemsOnly && UnitChecks.Problems(unit) == 0) continue;
                if(terms.Length > 0 && !StudioIndex.Matches(SearchText(unit), terms)) continue;
                units.Add(unit);
            }
            units.Sort((a, b) => string.Compare(StudioPrefs.LabelOf(a), StudioPrefs.LabelOf(b), StringComparison.OrdinalIgnoreCase));
            if(before.SequenceEqual(units)) list.RefreshItems();
            else list.Rebuild();
            var selected = Session.Selected;
            int index = selected != null ? units.IndexOf(selected) : -1;
            if(index >= 0) list.SetSelectionWithoutNotify(new[] { index });
            else list.ClearSelection();

            window.UpdateFilterButton();
            CombatEditorStyles.Show(listEmpty, units.Count == 0);
            CombatEditorStyles.Show(list, units.Count > 0);
        }

        public void Ping()
        {
            if(Session.Selected != null) EditorGUIUtility.PingObject(Session.Selected);
        }

        public void Rescan()
        {
            UnitLibrary.Clear();
            RefreshList();
            ShowContent();
        }

        public void ShowCreateMenu(Rect rect)
        {
            var selected = Session.Selected;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Wizard..."), false, () => StartWizard(null));
            menu.AddSeparator(string.Empty);
            menu.AddDisabledItem(new GUIContent("Presets"));
            foreach(var archetype in UnitArchetypes.All)
            {
                var each = archetype;
                menu.AddItem(new GUIContent(each.Name), false, () => StartWizard(each));
            }
            menu.AddSeparator(string.Empty);
            if(selected != null) menu.AddItem(new GUIContent("Copy Selected Unit"), false, () => StartFrom(selected));
            else menu.AddDisabledItem(new GUIContent("Copy Selected Unit"));
            menu.DropDown(rect);
        }

        public void ShowFilterMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Problems Only"), problemsOnly, () =>
            {
                problemsOnly = !problemsOnly;
                RefreshList();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Faction/Any"), factionFilter == null, () => SetFaction(null));
            menu.AddSeparator("Faction/");
            foreach(var faction in UnitLibrary.All.Select(UnitLibrary.FactionOf).Where(f => f != null).Distinct().OrderBy(StudioKinds.NameOf))
            {
                var each = faction;
                menu.AddItem(new GUIContent("Faction/" + StudioKinds.NameOf(each)), factionFilter == each, () => SetFaction(each));
            }
            menu.AddItem(new GUIContent("Has Part/Any"), partFilter == UnitPart.None, () => SetPart(UnitPart.None));
            menu.AddSeparator("Has Part/");
            foreach(var info in UnitParts.All)
            {
                var each = info;
                menu.AddItem(new GUIContent("Has Part/" + each.Label), partFilter == each.Part, () => SetPart(each.Part));
            }
            if(Filtered)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Clear Filters"), false, () =>
                {
                    problemsOnly = false;
                    factionFilter = null;
                    partFilter = UnitPart.None;
                    RefreshList();
                });
            }
            menu.DropDown(rect);
        }

        public void OnProjectChanged()
        {
            UnitLibrary.Clear();
            if(list == null) return;
            RefreshList();
            if(!Session.Creating) overview.Show(Session.Selected);
        }

        public void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(list == null || refreshQueued || !AssetChanges.AnyCombat(guids)) return;
            refreshQueued = true;
            list.schedule.Execute(() =>
            {
                refreshQueued = false;
                list.RefreshItems();
                if(!Session.Creating) overview.Refresh();
            }).ExecuteLater(120);
        }

        public void OnUndoRedo()
        {
            serializedWindow?.Update();
            if(Session.Creating) wizard?.Rebuild();
            else overview?.Refresh();
        }

        private void ShowContent()
        {
            if(overview == null) return;
            bool creating = Session.Creating;
            CombatEditorStyles.Show(overview, !creating);
            CombatEditorStyles.Show(wizard, creating);
            if(creating) wizard.Rebuild();
            else overview.Show(Session.Selected);
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("cg-asset-row");
            var preview = new Image { scaleMode = ScaleMode.ScaleToFit };
            preview.AddToClassList("cg-asset-row__sprite");
            row.Add(preview);
            var icon = new BlockIcon();
            icon.SetStyle("cg-kind-unit");
            row.Add(icon);
            var texts = new VisualElement();
            texts.AddToClassList("cg-asset-row__texts");
            texts.Add(CombatEditorStyles.Text(null, "cg-asset-row__name"));
            texts.Add(CombatEditorStyles.Text(null, "cg-asset-row__path"));
            row.Add(texts);
            row.Add(CombatEditorStyles.Text("!", "cg-asset-row__problem"));
            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if(!(row.userData is GameObject unit)) return;
                evt.menu.AppendAction("Open Prefab", _ => AssetDatabase.OpenAsset(unit));
                evt.menu.AppendAction("Delete...", _ => DeleteUnit(unit), AssetDeletion.CanDelete(unit) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Ping", _ => EditorGUIUtility.PingObject(unit));
                evt.menu.AppendAction("Copy to New Unit", _ => StartFrom(unit));
                evt.menu.AppendAction("Test Damage", _ => PipelineTesterWindow.OpenWithDefender(unit));
                StudioWindows.AppendAssetActions(evt.menu, unit);
            }));
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            var unit = units[index];
            row.userData = unit;
            bool previews = StudioPrefs.Get(k_Previews, true);
            var preview = (Image)row[0];
            var texture = previews ? AssetPreview.GetAssetPreview(unit) : null;
            preview.image = texture;
            CombatEditorStyles.Show(preview, texture != null);
            CombatEditorStyles.Show(row[1], previews && texture == null);
            if(previews && texture == null && AssetPreview.IsLoadingAssetPreviews()) row.schedule.Execute(() => list.RefreshItem(index)).ExecuteLater(200);

            ((Label)row[2][0]).text = StudioPrefs.LabelOf(unit);
            CombatEditorStyles.SetText(row[2][1], Compact ? null : SubtitleOf(unit));
            int problems = StudioPrefs.Get(k_Badges, true) ? UnitChecks.Problems(unit) : 0;
            CombatEditorStyles.Show(row[3], problems > 0);
            row[3].tooltip = problems == 1 ? "1 problem" : $"{problems} problems";
        }

        private static string SubtitleOf(GameObject unit)
        {
            switch((Subtitle)StudioPrefs.GetInt(k_Subtitle, 0))
            {
                case Subtitle.Faction:
                    var faction = UnitLibrary.FactionOf(unit);
                    return faction != null ? StudioKinds.NameOf(faction) : "No faction";
                case Subtitle.Parts:
                    return string.Join(", ", UnitParts.All.Where(p => unit.GetComponent(p.Type) != null).Select(p => p.Label));
                case Subtitle.Health:
                    var entry = UnitLibrary.ResourceOf(UnitLibrary.ResourceProfileOf(unit), UnitLibrary.HealthOf(unit));
                    return entry != null ? $"{UnitLibrary.MaxOf(UnitLibrary.ProfileOf(unit), entry):0.##} {StudioKinds.NameOf(entry.Type)}" : "No health";
                default:
                    return AssetDatabase.GetAssetPath(unit);
            }
        }

        private static string SearchText(GameObject unit)
        {
            var parts = new List<string> { unit.name, StudioKinds.NameOf(unit) };
            var faction = UnitLibrary.FactionOf(unit);
            if(faction != null) parts.Add(StudioKinds.NameOf(faction));
            parts.AddRange(UnitLibrary.TagsOf(unit).Select(StudioKinds.NameOf));
            var profile = UnitLibrary.ProfileOf(unit);
            if(profile != null) parts.Add(StudioKinds.NameOf(profile));
            parts.AddRange(UnitParts.All.Where(p => unit.GetComponent(p.Type) != null).Select(p => p.Label));
            return string.Join(" ", parts).ToLowerInvariant();
        }

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Cozy"), !Compact, () => StudioPrefs.Set(k_Compact, false));
            menu.AddItem(new GUIContent("Compact"), Compact, () => StudioPrefs.Set(k_Compact, true));
            menu.AddSeparator(string.Empty);
            StudioPrefs.Labels(menu);
            menu.AddSeparator(string.Empty);
            menu.AddDisabledItem(new GUIContent("Second Line"));
            foreach(Subtitle each in Enum.GetValues(typeof(Subtitle)))
            {
                StudioPrefs.Choice(menu, ObjectNames.NicifyVariableName(each.ToString()), k_Subtitle, (int)each, 0);
            }
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Previews", k_Previews, true);
            StudioPrefs.Toggle(menu, "Problem Badges", k_Badges, true);
            menu.AddSeparator(string.Empty);
            UnitOverview.Options(menu, "Overview/");
            UnitWizard.Options(menu, "Wizard/");
            StudioPrefs.Behavior(menu);
            menu.DropDown(rect);
        }

        private void SetFaction(Faction faction)
        {
            factionFilter = faction;
            RefreshList();
        }

        private void SetPart(UnitPart part)
        {
            partFilter = part;
            RefreshList();
        }

        private void ApplyListView()
        {
            if(list == null) return;
            list.fixedItemHeight = Compact ? k_CompactHeight : k_CozyHeight;
            list.EnableInClassList("cg-asset-list--compact", Compact);
            list.Rebuild();
        }

        private static UnitBlueprint Fresh(UnitBlueprint previous)
        {
            return new UnitBlueprint { Folder = previous.Folder, TypesFolder = DefaultTypesFolder(previous), OwnFolder = previous.OwnFolder, PlaceInScene = previous.PlaceInScene };
        }

        private static string DefaultTypesFolder(UnitBlueprint previous)
        {
            string existing = DefinitionAssets.FolderOf<StatType>();
            if(!string.IsNullOrEmpty(existing)) return existing;
            return string.IsNullOrWhiteSpace(previous.TypesFolder) ? "Assets/Units/Types" : previous.TypesFolder;
        }
    }
}
