using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class UnitCreatorWindow : EditorWindow
    {
        private enum Subtitle { Path, Faction, Parts, Health }

        private const string k_Title = "Unit Creator";
        private const string k_Compact = "Units.Compact";
        private const string k_Previews = "Units.Previews";
        private const string k_Subtitle = "Units.Subtitle";
        private const string k_Badges = "Units.Badges";
        private const float k_CozyHeight = 38f;
        private const float k_CompactHeight = 20f;

        [SerializeField] private GameObject selected;
        [SerializeField] private bool creating;
        [SerializeField] private int step;
        [SerializeField] private UnitBlueprint blueprint = new();

        private readonly List<GameObject> units = new();
        private SerializedObject serializedWindow;
        private ToolbarSearchField search;
        private ToolbarButton filterButton;
        private ListView list;
        private VisualElement listEmpty;
        private VisualElement content;
        private UnitOverview overview;
        private UnitWizard wizard;
        private Faction factionFilter;
        private UnitPart partFilter;
        private bool problemsOnly;

        internal UnitBlueprint Blueprint => blueprint;

        internal int Step
        {
            get => step;
            set => step = value;
        }

        private static bool Compact => StudioPrefs.Get(k_Compact, false);

        [MenuItem(StudioWindows.k_Menu + k_Title)]
        public static void Open() => Open(null);

        public static void Open(GameObject unit)
        {
            var window = GetWindow<UnitCreatorWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            if(unit == null) return;
            window.SelectUnit(unit);
            window.RefreshList();
        }

        [MenuItem("Assets/Create/CG Combat Framework/Unit (Wizard)", priority = 0)]
        private static void CreateFromMenu()
        {
            var window = GetWindow<UnitCreatorWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            window.StartWizard(null);
        }

        [MenuItem("CONTEXT/Unit/Open in Unit Creator")]
        private static void OpenFromContext(MenuCommand command)
        {
            if(command.context is Component component) Open(PrefabUtility.GetCorrespondingObjectFromSource(component.gameObject) ?? component.gameObject);
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
            StudioPrefs.Changed += OnPrefsChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            StudioPrefs.Changed -= OnPrefsChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            serializedWindow?.Dispose();
            serializedWindow = null;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-studio");
            root.AddToClassList("cg-units");
            serializedWindow = new SerializedObject(this);

            root.Add(BuildToolbar());
            var split = new TwoPaneSplitView(0, 240f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-units-split" };
            split.AddToClassList("cg-studio__split");
            split.Add(BuildListPane());
            content = new VisualElement();
            content.AddToClassList("cg-pane");
            overview = new UnitOverview(this);
            wizard = new UnitWizard(this, serializedWindow);
            content.Add(overview);
            content.Add(wizard);
            split.Add(content);
            root.Add(split);

            ApplyListView();
            RefreshList();
            ShowContent();
        }

        internal void StartWizard(UnitArchetype archetype)
        {
            Undo.RecordObject(this, "New Unit");
            creating = true;
            step = 0;
            blueprint = new UnitBlueprint { Folder = blueprint.Folder, TypesFolder = DefaultTypesFolder(blueprint), OwnFolder = blueprint.OwnFolder, PlaceInScene = blueprint.PlaceInScene };
            if(archetype != null)
            {
                archetype.Apply(blueprint);
                step = 1;
            }
            serializedWindow?.Update();
            ShowContent();
        }

        internal void StartFrom(GameObject prefab)
        {
            Undo.RecordObject(this, "Copy Unit");
            creating = true;
            blueprint = new UnitBlueprint { Folder = blueprint.Folder, TypesFolder = DefaultTypesFolder(blueprint), OwnFolder = blueprint.OwnFolder, PlaceInScene = blueprint.PlaceInScene };
            UnitArchetypes.CopyFrom(prefab, blueprint);
            step = 1;
            serializedWindow?.Update();
            ShowContent();
        }

        internal void CancelWizard()
        {
            creating = false;
            ShowContent();
        }

        internal void FinishWizard(GameObject prefab)
        {
            creating = false;
            UnitLibrary.Clear();
            selected = prefab;
            RefreshList();
            ShowContent();
            EditorGUIUtility.PingObject(prefab);
        }

        internal void AfterUnitEdit(GameObject prefab)
        {
            UnitLibrary.Clear();
            selected = prefab;
            RefreshList();
            if(!creating) overview?.Show(selected);
        }

        internal void RefreshList()
        {
            if(list == null) return;
            var terms = StudioIndex.Terms(search != null ? search.value : null);
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
            int index = selected != null ? units.IndexOf(selected) : -1;
            if(index >= 0) list.SetSelectionWithoutNotify(new[] { index });
            else list.ClearSelection();

            bool filtered = factionFilter != null || partFilter != UnitPart.None || problemsOnly;
            filterButton.text = filtered ? "Filter (on)" : "Filter";
            filterButton.EnableInClassList("cg-toolbar__filter--active", filtered);
            CombatEditorStyles.Show(listEmpty, units.Count == 0);
            CombatEditorStyles.Show(list, units.Count > 0);
        }

        private void SelectUnit(GameObject unit)
        {
            selected = unit;
            creating = false;
            ShowContent();
        }

        private void ShowContent()
        {
            if(overview == null) return;
            CombatEditorStyles.Show(overview, !creating);
            CombatEditorStyles.Show(wizard, creating);
            if(creating) wizard.Rebuild();
            else overview.Show(selected);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("cg-toolbar");
            toolbar.Add(CombatEditorStyles.Text(k_Title, "cg-toolbar__title"));
            var create = new ToolbarMenu { text = "New Unit" };
            create.AddToClassList("cg-toolbar__new");
            create.menu.AppendAction("Wizard...", _ => StartWizard(null));
            create.menu.AppendSeparator();
            create.menu.AppendAction("Presets", _ => { }, DropdownMenuAction.Status.Disabled);
            foreach(var archetype in UnitArchetypes.All)
            {
                var each = archetype;
                create.menu.AppendAction(each.Name, _ => StartWizard(each));
            }
            create.menu.AppendSeparator();
            create.menu.AppendAction("Copy Selected Unit", _ => StartFrom(selected), _ => selected != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            toolbar.Add(create);
            toolbar.Add(new ToolbarButton(() =>
            {
                if(selected != null) EditorGUIUtility.PingObject(selected);
            }) { text = "Ping" });
            toolbar.Add(new ToolbarButton(() =>
            {
                UnitLibrary.Clear();
                RefreshList();
                ShowContent();
            }) { text = "Refresh", tooltip = "Scan the project for unit prefabs again" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            filterButton = new ToolbarButton(() => ShowFilterMenu(filterButton.worldBound)) { text = "Filter" };
            filterButton.AddToClassList("cg-toolbar__filter");
            toolbar.Add(filterButton);
            search = new ToolbarSearchField { tooltip = "Search names, factions, tags, profiles and parts" };
            search.AddToClassList("cg-toolbar__search");
            search.RegisterValueChangedCallback(_ => RefreshList());
            toolbar.Add(search);
            toolbar.Add(StudioWindows.Menu());
            return toolbar;
        }

        private VisualElement BuildListPane()
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
            list.selectionChanged += selection =>
            {
                var unit = selection.FirstOrDefault() as GameObject;
                if(unit == null || (unit == selected && !creating)) return;
                SelectUnit(unit);
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
                evt.menu.AppendAction("Ping", _ => EditorGUIUtility.PingObject(unit));
                evt.menu.AppendAction("Copy to New Unit", _ => StartFrom(unit));
                evt.menu.AppendAction("Test Damage", _ => PipelineTesterWindow.OpenWithDefender(unit));
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

        private void ShowFilterMenu(Rect rect)
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
            if(problemsOnly || factionFilter != null || partFilter != UnitPart.None)
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

        private void OnPrefsChanged()
        {
            if(list == null) return;
            ApplyListView();
            RefreshList();
            ShowContent();
        }

        private void OnProjectChanged()
        {
            UnitLibrary.Clear();
            if(list == null) return;
            RefreshList();
            if(!creating) overview.Show(selected);
        }

        private void OnUndoRedo()
        {
            serializedWindow?.Update();
            if(creating) wizard?.Rebuild();
            else overview?.Refresh();
        }

        private static string DefaultTypesFolder(UnitBlueprint previous)
        {
            string existing = DefinitionAssets.FolderOf<StatType>();
            if(!string.IsNullOrEmpty(existing)) return existing;
            return string.IsNullOrWhiteSpace(previous.TypesFolder) ? "Assets/Units/Types" : previous.TypesFolder;
        }
    }
}
