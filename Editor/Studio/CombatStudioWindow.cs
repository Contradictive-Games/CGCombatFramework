using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class CombatStudioWindow : EditorWindow
    {
        private const string k_Title = "Combat Studio";
        private const string k_Open = "Open in Combat Studio";
        private const string k_Menu = StudioWindows.k_Menu + k_Title;
        private const string k_Compact = "Compact";
        private const string k_Icons = "Icons";
        private const string k_Paths = "Paths";
        private const string k_Badges = "Badges";
        private const string k_Guides = "Guides";
        private const string k_TreeSummaries = "TreeSummaries";
        private const string k_TreeLabels = "TreeLabels";
        private const string k_TreeIcons = "TreeIcons";
        private const string k_TreeCompact = "TreeCompact";
        private const float k_CozyHeight = 34f;
        private const float k_CompactHeight = 20f;
        private const float k_TreeHeight = 22f;
        private const float k_TreeCompactHeight = 18f;

        [SerializeField] private int kindIndex;
        [SerializeField] private ScriptableObject asset;
        [SerializeField] private string selectedPath = string.Empty;

        private readonly StudioOutline outline = new();
        private readonly OutlineGuides guides = new();
        private readonly List<ScriptableObject> assets = new();
        private readonly List<ToolbarToggle> tabs = new();
        private readonly HashSet<int> trail = new();
        private readonly HashSet<Type> usedTypes = new();
        private SerializedObject serializedObject;
        private ToolbarSearchField search;
        private ToolbarSearchField outlineSearch;
        private ToolbarButton filterButton;
        private ListView assetList;
        private TreeView tree;
        private VisualElement outlineEmpty;
        private Label outlineEmptyTitle;
        private Button outlineEmptyCreate;
        private StudioDetail detail;
        private VisualElement banner;
        private VisualElement watcher;
        private ScriptableObject expandedFor;
        private List<int> savedExpansion;
        private string signature;
        private bool selecting;
        private bool problemsOnly;
        private Type usesFilter;

        private StudioKind Kind => StudioKinds.All[Mathf.Clamp(kindIndex, 0, StudioKinds.All.Length - 1)];
        private string OutlineFilter => outlineSearch != null ? outlineSearch.value ?? string.Empty : string.Empty;
        private static bool Compact => StudioPrefs.Get(k_Compact, false);
        private static bool ShowIcons => StudioPrefs.Get(k_Icons, true);

        [MenuItem(k_Menu, false, 0)]
        public static void Open() => Open(null);

        public static void Open(ScriptableObject target) => Open(target, null);

        public static void Open(ScriptableObject target, string path)
        {
            var window = GetWindow<CombatStudioWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            if(target != null) window.Reveal(target, path);
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
#pragma warning disable CS0618
            var target = EditorUtility.InstanceIDToObject(instanceId) as ScriptableObject;
#pragma warning restore CS0618
            if(target == null || StudioKinds.IndexOf(target.GetType()) < 0) return false;
            Open(target);
            return true;
        }

        [MenuItem("CONTEXT/" + nameof(FirableDefinition) + "/" + k_Open)]
        [MenuItem("CONTEXT/" + nameof(StatusEffectDefinition) + "/" + k_Open)]
        [MenuItem("CONTEXT/" + nameof(DamagePipeline) + "/" + k_Open)]
        private static void OpenFromContext(MenuCommand command) => Open(command.context as ScriptableObject);

        public void Reveal(ScriptableObject target, string path = null)
        {
            RecentAssets.Remember(target);
            asset = target;
            selectedPath = path ?? string.Empty;
            if(tree != null) Load(target, path);
        }

        public void Select(string path)
        {
            if(tree == null) return;
            if(serializedObject == null)
            {
                selectedPath = string.Empty;
                trail.Clear();
                detail.Show(null, outline, null);
                return;
            }

            var node = outline.Nearest(path);
            if(node == null) return;
            if(!outline.IsVisible(node.Id)) node = outline.Root;
            selectedPath = node.Path;
            for(var parent = outline.Parent(node); parent != null; parent = outline.Parent(parent))
            {
                tree.ExpandItem(parent.Id);
            }
            selecting = true;
            tree.SetSelectionById(node.Id);
            selecting = false;
            tree.ScrollToItemById(node.Id);
            UpdateTrail(node);
            detail.Show(serializedObject, outline, node);
        }

        public void AfterEdit(string path)
        {
            if(serializedObject == null) return;
            serializedObject.Update();
            RebuildOutline();
            Select(path ?? selectedPath);
            RefreshAssets();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
            StudioPrefs.Changed += OnPrefsChanged;
            AssetChanges.Changed += OnOtherAssetsChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            StudioPrefs.Changed -= OnPrefsChanged;
            AssetChanges.Changed -= OnOtherAssetsChanged;
            serializedObject?.Dispose();
            serializedObject = null;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-studio");

            root.Add(BuildToolbar());

            banner = new VisualElement();
            banner.AddToClassList("cg-banner");
            root.Add(banner);

            var split = new TwoPaneSplitView(0, 220f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-studio-split" };
            split.AddToClassList("cg-studio__split");
            var inner = new TwoPaneSplitView(0, 300f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-studio-inner" };
            split.Add(BuildAssetPane());
            split.Add(inner);
            inner.Add(BuildOutlinePane());
            detail = new StudioDetail(this);
            inner.Add(detail);
            root.Add(split);

            ApplyListView();
            ApplyTreeView();
            SetKind(kindIndex, false);
            Load(asset, selectedPath);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("cg-toolbar");
            for(int i = 0; i < StudioKinds.All.Length; i++)
            {
                int index = i;
                var tab = new ToolbarToggle { text = StudioKinds.All[i].Title };
                tab.AddToClassList("cg-tab");
                tab.RegisterValueChangedCallback(evt =>
                {
                    if(evt.newValue) SetKind(index, true);
                    else if(index == kindIndex) tab.SetValueWithoutNotify(true);
                });
                tabs.Add(tab);
                toolbar.Add(tab);
            }

            toolbar.Add(new ToolbarSpacer { flex = true });

            ToolbarButton create = null;
            create = new ToolbarButton(() => ShowCreateMenu(create.worldBound)) { text = "New" };
            create.AddToClassList("cg-toolbar__new");
            toolbar.Add(create);
            toolbar.Add(new ToolbarButton(PingAsset) { text = "Ping" });

            filterButton = new ToolbarButton(() => ShowFilterMenu(filterButton.worldBound)) { text = "Filter" };
            filterButton.AddToClassList("cg-toolbar__filter");
            toolbar.Add(filterButton);

            search = new ToolbarSearchField { tooltip = "Search names, IDs and the building blocks they use, e.g. raycast damage" };
            search.AddToClassList("cg-toolbar__search");
            search.RegisterValueChangedCallback(_ => RefreshAssets());
            toolbar.Add(search);
            toolbar.Add(StudioWindows.Menu());
            return toolbar;
        }

        private VisualElement BuildAssetPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");

            var header = CombatEditorStyles.Header("Assets");
            header.Add(CombatEditorStyles.DropButton("View", ShowViewMenu, "List options"));
            pane.Add(header);

            assetList = new ListView
            {
                fixedItemHeight = k_CozyHeight,
                selectionType = SelectionType.Single,
                itemsSource = assets,
                makeItem = MakeAssetRow,
                bindItem = BindAssetRow
            };
            assetList.AddToClassList("cg-asset-list");
            assetList.selectionChanged += OnAssetSelected;
            assetList.RegisterCallback<KeyDownEvent>(evt =>
            {
                bool delete = evt.keyCode == KeyCode.Delete || (evt.keyCode == KeyCode.Backspace && evt.actionKey);
                if(!delete || !(assetList.selectedItem is ScriptableObject target)) return;
                evt.StopPropagation();
                DeleteAsset(target);
            });
            pane.Add(assetList);
            return pane;
        }

        private VisualElement BuildOutlinePane()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");

            var header = CombatEditorStyles.Header("Outline");
            outlineSearch = new ToolbarSearchField { tooltip = "Filter this outline by block, field or value" };
            outlineSearch.AddToClassList("cg-outline-search");
            outlineSearch.RegisterValueChangedCallback(_ =>
            {
                RebuildOutline();
                Select(selectedPath);
            });
            header.Add(outlineSearch);
            header.Add(CombatEditorStyles.DropButton("View", ShowOutlineMenu, "Outline and detail options"));
            pane.Add(header);

            tree = new TreeView
            {
                fixedItemHeight = k_TreeHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeTreeRow,
                bindItem = BindTreeRow,
                unbindItem = (row, _) => row.userData = null
            };
            tree.AddToClassList("cg-outline");
            tree.selectionChanged += OnTreeSelected;
            tree.RegisterCallback<KeyDownEvent>(OnTreeKey, TrickleDown.TrickleDown);
            tree.RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
            tree.RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
            pane.Add(tree);

            outlineEmpty = new VisualElement();
            outlineEmpty.AddToClassList("cg-outline-empty");
            outlineEmptyTitle = CombatEditorStyles.Text(null, "cg-outline-empty__title");
            outlineEmpty.Add(outlineEmptyTitle);
            outlineEmpty.Add(CombatEditorStyles.Text("Want to make a new one?", "cg-outline-empty__text"));
            var buttons = new VisualElement();
            buttons.AddToClassList("cg-outline-empty__buttons");
            outlineEmptyCreate = new Button(() => CreateAsset(null));
            outlineEmptyCreate.AddToClassList("cg-outline-empty__create");
            buttons.Add(outlineEmptyCreate);
            Button presets = null;
            presets = new Button(() => ShowCreateMenu(presets.worldBound)) { text = "From Preset" };
            buttons.Add(presets);
            outlineEmpty.Add(buttons);
            pane.Add(outlineEmpty);
            return pane;
        }

        private VisualElement MakeAssetRow()
        {
            var row = new VisualElement();
            row.AddToClassList("cg-asset-row");
            var sprite = new Image { scaleMode = ScaleMode.ScaleToFit };
            sprite.AddToClassList("cg-asset-row__sprite");
            row.Add(sprite);
            row.Add(new BlockIcon());
            var texts = new VisualElement();
            texts.AddToClassList("cg-asset-row__texts");
            texts.Add(CombatEditorStyles.Text(null, "cg-asset-row__name"));
            texts.Add(CombatEditorStyles.Text(null, "cg-asset-row__path"));
            row.Add(texts);
            row.Add(CombatEditorStyles.Text("!", "cg-asset-row__problem"));
            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if(!(row.userData is ScriptableObject target)) return;
                evt.menu.AppendAction("Ping", _ => EditorGUIUtility.PingObject(target));
                evt.menu.AppendAction("Duplicate", _ => DuplicateAsset(target));
                evt.menu.AppendAction("Delete...", _ => DeleteAsset(target), AssetDeletion.CanDelete(target) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                StudioWindows.AppendAssetActions(evt.menu, target);
            }));
            return row;
        }

        private void BindAssetRow(VisualElement row, int index)
        {
            var target = assets[index];
            row.userData = target;
            var sprite = StudioKinds.IconOf(target);
            var image = (Image)row[0];
            var icon = (BlockIcon)row[1];
            image.sprite = sprite;
            image.style.display = ShowIcons && sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            icon.SetCategory(Kind.Category);
            icon.style.display = ShowIcons && sprite == null ? DisplayStyle.Flex : DisplayStyle.None;
            ((Label)row[2][0]).text = LabelOf(target);
            CombatEditorStyles.SetText(row[2][1], StudioPrefs.Get(k_Paths, true) ? AssetDatabase.GetAssetPath(target) : null);
            var entry = StudioIndex.Of(target);
            CombatEditorStyles.Show(row[3], entry.HasProblems && StudioPrefs.Get(k_Badges, true));
            row[3].tooltip = entry.MissingTypes ? "Uses building blocks that no longer exist" : $"{entry.EmptySlots} empty slot(s)";
        }

        private static string LabelOf(ScriptableObject target) => StudioPrefs.LabelOf(target);

        private VisualElement MakeTreeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("cg-tree-row");
            row.Add(new BlockIcon());
            row.Add(CombatEditorStyles.Text(null, "cg-tree-row__label"));
            row.Add(CombatEditorStyles.Text(null, "cg-tree-row__title"));
            row.Add(CombatEditorStyles.Text(null, "cg-tree-row__summary"));
            row.AddManipulator(new ContextualMenuManipulator(evt => PopulateTreeMenu(evt, row)));
            row.AddManipulator(new SlotDragger(PathOf, TreeRows, (path, to) => AfterEdit(BlockEdits.MoveTo(serializedObject, path, to))));
            return row;
        }

        private void BindTreeRow(VisualElement row, int index)
        {
            var node = tree.GetItemDataForIndex<OutlineNode>(index);
            row.userData = node;
            if(node == null || serializedObject == null) return;

            var category = node.Kind == OutlineKind.Root ? Kind.Category : StudioOutline.CategoryOf(node, serializedObject);
            row.EnableInClassList("cg-tree-row--root", node.Kind == OutlineKind.Root);
            row.EnableInClassList("cg-tree-row--section", node.Kind == OutlineKind.Section);
            row.EnableInClassList("cg-tree-row--empty", StudioOutline.IsEmpty(node, serializedObject));
            row.EnableInClassList("cg-tree-row--trail", trail.Contains(node.Id));
            BlockCategories.Apply(row, category);
            ((BlockIcon)row[0]).SetCategory(category);
            CombatEditorStyles.Show(row[0], StudioPrefs.Get(k_TreeIcons, true));
            CombatEditorStyles.SetText(row[1], node.Kind == OutlineKind.Block && StudioPrefs.Get(k_TreeLabels, true) ? node.Label : null);
            CombatEditorStyles.SetText(row[2], StudioOutline.TitleOf(node, serializedObject));
            CombatEditorStyles.SetText(row[3], StudioPrefs.Get(k_TreeSummaries, true) ? StudioOutline.SummaryOf(node, serializedObject, true) : null);
            guides.Bind(row, outline.GuidesOf(node), !outline.IsBranch(node));
            row.tooltip = node.Kind == OutlineKind.Block ? BlockTypes.TooltipOf(BlockTypes.ValueType(serializedObject.FindProperty(node.Path))) : null;
        }

        private static string PathOf(VisualElement row) => (row.userData as OutlineNode)?.Path;

        private IEnumerable<VisualElement> TreeRows() => tree.Query<VisualElement>(className: "cg-tree-row").Where(row => row.userData != null).ToList();

        private void PopulateTreeMenu(ContextualMenuPopulateEvent evt, VisualElement row)
        {
            if(!(row.userData is OutlineNode node) || serializedObject == null) return;
            switch(node.Kind)
            {
                case OutlineKind.Root:
                    evt.menu.AppendAction("Expand All", _ => tree.ExpandAll());
                    evt.menu.AppendAction("Collapse All", _ => tree.CollapseAll());
                    evt.menu.AppendAction("Ping", _ => PingAsset());
                    evt.menu.AppendAction("Delete...", _ => DeleteAsset(asset), AssetDeletion.CanDelete(asset) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    StudioWindows.AppendAssetActions(evt.menu, asset);
                    break;
                case OutlineKind.Section:
                    evt.menu.AppendAction("Add...", _ => BlockTypePicker.Show(row.worldBound, BlockTypes.SlotType(serializedObject.FindProperty(node.Path)), false,
                        type => AfterEdit(BlockEdits.Add(serializedObject, node.Path, type))));
                    break;
                default:
                    evt.menu.AppendAction("Change Type...", _ => PickType(row, node.Path));
                    evt.menu.AppendSeparator();
                    BlockMenu.Append(evt.menu, BlockMenu.For(serializedObject, node.Path, AfterEdit));
                    break;
            }
        }

        private void PickType(VisualElement anchor, string path)
        {
            var property = serializedObject.FindProperty(path);
            if(property == null) return;
            var current = BlockTypes.ValueType(property);
            BlockTypePicker.Show(anchor.worldBound, BlockTypes.SlotType(property), true, type =>
            {
                if(type != current) AfterEdit(BlockEdits.SetType(serializedObject, path, type));
            });
        }

        private OutlineNode SelectedBlock()
        {
            if(serializedObject == null || tree.selectedIndex < 0) return null;
            var node = tree.GetItemDataForIndex<OutlineNode>(tree.selectedIndex);
            return node != null && node.Kind == OutlineKind.Block ? node : null;
        }

        private void OnTreeKey(KeyDownEvent evt)
        {
            if(!evt.altKey || (evt.keyCode != KeyCode.UpArrow && evt.keyCode != KeyCode.DownArrow)) return;
            var node = SelectedBlock();
            if(node == null) return;
            evt.StopPropagation();
            AfterEdit(BlockEdits.Move(serializedObject, node.Path, evt.keyCode == KeyCode.UpArrow ? -1 : 1));
        }

        private void OnValidateCommand(ValidateCommandEvent evt)
        {
            if(CanRun(evt.commandName)) evt.StopPropagation();
        }

        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            if(!CanRun(evt.commandName)) return;
            var node = SelectedBlock();
            evt.StopPropagation();
            switch(evt.commandName)
            {
                case "Copy":
                    BlockClipboard.Copy(serializedObject.FindProperty(node.Path).managedReferenceValue);
                    break;
                case "Paste":
                    AfterEdit(BlockEdits.Paste(serializedObject, node.Path));
                    break;
                case "Duplicate":
                    AfterEdit(BlockEdits.Duplicate(serializedObject, node.Path));
                    break;
                default:
                    AfterEdit(BlockEdits.Remove(serializedObject, node.Path));
                    break;
            }
        }

        private bool CanRun(string command)
        {
            var node = SelectedBlock();
            if(node == null) return false;
            var property = serializedObject.FindProperty(node.Path);
            if(property == null) return false;
            switch(command)
            {
                case "Copy": return property.managedReferenceValue != null;
                case "Paste": return BlockClipboard.CanPasteInto(BlockTypes.SlotType(property));
                case "Duplicate": return BlockEdits.TrySplit(node.Path, out _, out _);
                case "Delete":
                case "SoftDelete": return true;
                default: return false;
            }
        }

        private void SetKind(int index, bool reset)
        {
            kindIndex = Mathf.Clamp(index, 0, StudioKinds.All.Length - 1);
            for(int i = 0; i < tabs.Count; i++)
            {
                tabs[i].SetValueWithoutNotify(i == kindIndex);
            }
            usesFilter = null;
            if(outlineEmptyCreate != null)
            {
                outlineEmptyTitle.text = $"No {Kind.Singular.ToLowerInvariant()} open";
                outlineEmptyCreate.text = $"Create {Kind.Singular}";
            }
            if(reset && asset != null && !Kind.AssetType.IsInstanceOfType(asset)) Load(null, null);
            RefreshAssets();
        }

        private void RefreshAssets()
        {
            if(assetList == null) return;
            var terms = StudioIndex.Terms(search != null ? search.value : null);
            assets.Clear();
            usedTypes.Clear();
            foreach(var guid in AssetDatabase.FindAssets($"t:{Kind.AssetType.Name}"))
            {
                var found = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if(found == null || !Kind.AssetType.IsInstanceOfType(found)) continue;
                var entry = StudioIndex.Of(found);
                usedTypes.UnionWith(entry.Types);
                if(!StudioIndex.Matches(entry.Text, terms)) continue;
                if(problemsOnly && !entry.HasProblems) continue;
                if(usesFilter != null && !entry.Types.Contains(usesFilter)) continue;
                assets.Add(found);
            }
            assets.Sort((a, b) => string.Compare(LabelOf(a), LabelOf(b), StringComparison.OrdinalIgnoreCase));
            assetList.Rebuild();

            int selected = asset != null ? assets.IndexOf(asset) : -1;
            if(selected >= 0) assetList.SetSelectionWithoutNotify(new[] { selected });
            else assetList.ClearSelection();

            bool filtered = problemsOnly || usesFilter != null;
            filterButton.text = filtered ? "Filter (on)" : "Filter";
            filterButton.EnableInClassList("cg-toolbar__filter--active", filtered);
        }

        private void Load(ScriptableObject target, string path)
        {
            asset = target;
            serializedObject?.Dispose();
            serializedObject = target != null ? new SerializedObject(target) : null;

            watcher?.RemoveFromHierarchy();
            watcher = null;
            if(serializedObject != null)
            {
                int kind = StudioKinds.IndexOf(target.GetType());
                if(kind >= 0 && kind != kindIndex) SetKind(kind, false);
                else
                {
                    int selected = assets.IndexOf(target);
                    if(selected >= 0) assetList.SetSelectionWithoutNotify(new[] { selected });
                }
                watcher = new VisualElement();
                watcher.AddToClassList("cg-watcher");
                rootVisualElement.Add(watcher);
                watcher.TrackSerializedObjectValue(serializedObject, OnAssetChanged);
            }

            bool empty = serializedObject == null;
            tree.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            outlineEmpty.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            outlineSearch.SetEnabled(!empty);

            UpdateBanner();
            RebuildOutline();
            Select(path);
        }

        private void RebuildOutline()
        {
            if(tree == null) return;
            if(serializedObject == null)
            {
                tree.SetRootItems(new List<TreeViewItemData<OutlineNode>>());
                tree.Rebuild();
                signature = null;
                return;
            }

            bool filtering = StudioIndex.Terms(OutlineFilter).Length > 0;
            List<int> restore = null;
            if(filtering) savedExpansion ??= ExpandedIds();
            else
            {
                restore = savedExpansion ?? ExpandedIds();
                savedExpansion = null;
            }

            serializedObject.Update();
            tree.SetRootItems(outline.Build(serializedObject, OutlineFilter));
            tree.Rebuild();
            signature = outline.Signature;

            if(filtering || expandedFor != asset)
            {
                tree.ExpandAll();
                if(!filtering) expandedFor = asset;
                return;
            }
            foreach(int id in restore)
            {
                if(outline.IsVisible(id)) tree.ExpandItem(id);
            }
        }

        private List<int> ExpandedIds()
        {
            var ids = new List<int>();
            foreach(var node in outline.Ordered)
            {
                if(tree.IsExpanded(node.Id)) ids.Add(node.Id);
            }
            return ids;
        }

        private void UpdateTrail(OutlineNode node)
        {
            trail.Clear();
            for(var each = node; each != null; each = outline.Parent(each))
            {
                trail.Add(each.Id);
            }
            tree.RefreshItems();
        }

        private void OnAssetChanged(SerializedObject _)
        {
            if(serializedObject == null || tree == null) return;
            serializedObject.Update();
            if(StudioOutline.SignatureOf(serializedObject) != signature)
            {
                RebuildOutline();
                Select(selectedPath);
                return;
            }
            tree.RefreshItems();
            assetList.RefreshItems();
            detail.RefreshHeader();
        }

        private void OnOtherAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(tree == null || !AssetChanges.AnyCombat(guids)) return;
            assetList.RefreshItems();
            if(serializedObject == null) return;
            tree.RefreshItems();
            detail.RefreshSummaries();
        }

        private void OnUndoRedo()
        {
            if(serializedObject == null) return;
            UpdateBanner();
            OnAssetChanged(serializedObject);
        }

        private void OnProjectChanged()
        {
            StudioIndex.Clear();
            RefreshAssets();
        }

        private void OnAssetSelected(IEnumerable<object> selection)
        {
            var target = selection.FirstOrDefault() as ScriptableObject;
            if(target == null || target == asset) return;
            RecentAssets.Remember(target);
            Load(target, null);
        }

        private void OnTreeSelected(IEnumerable<object> _)
        {
            if(selecting || serializedObject == null) return;
            var node = tree.selectedIndex >= 0 ? tree.GetItemDataForIndex<OutlineNode>(tree.selectedIndex) : null;
            if(node == null) return;
            selectedPath = node.Path;
            UpdateTrail(node);
            detail.Show(serializedObject, outline, node);
        }

        private void UpdateBanner()
        {
            if(banner == null) return;
            banner.Clear();
            bool missing = asset != null && SerializationUtility.HasManagedReferencesWithMissingTypes(asset);
            banner.style.display = missing ? DisplayStyle.Flex : DisplayStyle.None;
            if(!missing) return;

            var names = SerializationUtility.GetManagedReferencesWithMissingTypes(asset).Select(m => m.className).Distinct();
            banner.Add(CombatEditorStyles.Text($"This asset uses building blocks that no longer exist: {string.Join(", ", names)}", "cg-banner__text"));
            var clear = new Button(ClearMissing) { text = "Remove Them" };
            clear.AddToClassList("cg-banner__button");
            banner.Add(clear);
        }

        private void ClearMissing()
        {
            if(asset == null) return;
            Undo.RegisterCompleteObjectUndo(asset, "Remove Missing Blocks");
            SerializationUtility.ClearAllManagedReferencesWithMissingTypes(asset);
            EditorUtility.SetDirty(asset);
            Load(asset, selectedPath);
        }

        private void ShowCreateMenu(Rect rect)
        {
            var kind = Kind;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"Empty {kind.Singular}"), false, () => CreateAsset(null));
            if(kind.Templates.Length > 0)
            {
                menu.AddSeparator(string.Empty);
                menu.AddDisabledItem(new GUIContent("Presets"));
            }
            foreach(var template in kind.Templates)
            {
                var each = template;
                menu.AddItem(new GUIContent(each.Name), false, () => CreateAsset(each));
            }
            menu.DropDown(rect);
        }

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Cozy"), !Compact, () => StudioPrefs.Set(k_Compact, false));
            menu.AddItem(new GUIContent("Compact"), Compact, () => StudioPrefs.Set(k_Compact, true));
            menu.AddSeparator(string.Empty);
            StudioPrefs.Labels(menu);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Icons", k_Icons, true);
            StudioPrefs.Toggle(menu, "Paths", k_Paths, true);
            StudioPrefs.Toggle(menu, "Problem Badges", k_Badges, true);
            menu.DropDown(rect);
        }

        private void ShowOutlineMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Outline"));
            StudioPrefs.Toggle(menu, "Branch Guides", k_Guides, true);
            StudioPrefs.Toggle(menu, "Icons", k_TreeIcons, true);
            StudioPrefs.Toggle(menu, "Field Labels", k_TreeLabels, true);
            StudioPrefs.Toggle(menu, "Summaries", k_TreeSummaries, true);
            StudioPrefs.Toggle(menu, "Compact Rows", k_TreeCompact, false);
            BlockSummary.Options(menu);
            menu.AddSeparator(string.Empty);
            menu.AddDisabledItem(new GUIContent("Details"));
            StudioDetail.Options(menu);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Behavior(menu);
            menu.AddSeparator(string.Empty);
            if(serializedObject != null)
            {
                menu.AddItem(new GUIContent("Expand All"), false, () => tree.ExpandAll());
                menu.AddItem(new GUIContent("Collapse All"), false, () => tree.CollapseAll());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Expand All"));
                menu.AddDisabledItem(new GUIContent("Collapse All"));
            }
            menu.DropDown(rect);
        }

        private void ShowFilterMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Problems Only"), problemsOnly, () =>
            {
                problemsOnly = !problemsOnly;
                RefreshAssets();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Uses/Anything"), usesFilter == null, () => SetUses(null));
            menu.AddSeparator("Uses/");
            foreach(var type in usedTypes.OrderBy(t => BlockCategories.Of(t)).ThenBy(BlockTypes.DisplayName))
            {
                var each = type;
                string label = $"Uses/{BlockCategories.Label(BlockCategories.Of(each))}/{BlockTypes.DisplayName(each)}";
                menu.AddItem(new GUIContent(label), usesFilter == each, () => SetUses(each));
            }
            if(problemsOnly || usesFilter != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Clear Filters"), false, () =>
                {
                    problemsOnly = false;
                    SetUses(null);
                });
            }
            menu.DropDown(rect);
        }

        private void SetUses(Type type)
        {
            usesFilter = type;
            RefreshAssets();
        }

        private void OnPrefsChanged()
        {
            if(tree == null) return;
            ApplyListView();
            ApplyTreeView();
            RefreshAssets();
            if(serializedObject != null) Select(selectedPath);
        }

        private void ApplyListView()
        {
            if(assetList == null) return;
            bool cozy = !Compact && StudioPrefs.Get(k_Paths, true);
            assetList.fixedItemHeight = cozy ? k_CozyHeight : k_CompactHeight;
            assetList.EnableInClassList("cg-asset-list--compact", Compact);
            assetList.Rebuild();
        }

        private void ApplyTreeView()
        {
            if(tree == null) return;
            bool compact = StudioPrefs.Get(k_TreeCompact, false);
            guides.Enabled = StudioPrefs.Get(k_Guides, true);
            tree.fixedItemHeight = compact ? k_TreeCompactHeight : k_TreeHeight;
            tree.EnableInClassList("cg-outline--compact", compact);
            tree.RefreshItems();
            guides.Refresh();
        }

        private void CreateAsset(StudioTemplate template)
        {
            var kind = Kind;
            string folder = asset != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset))?.Replace('\\', '/') : "Assets";
            string name = template != null ? template.Name : kind.Singular;
            string path = EditorUtility.SaveFilePanelInProject($"New {kind.Singular}", name, "asset", $"Choose where to save the new {kind.Singular.ToLowerInvariant()}", folder);
            if(string.IsNullOrEmpty(path)) return;

            var created = CreateInstance(kind.AssetType);
            template?.Build(created);
            AssetDatabase.CreateAsset(created, path);
            DefinitionAssets.ResetId(created, path);
            AssetDatabase.SaveAssets();
            RefreshAssets();
            RecentAssets.Remember(created);
            Load(created, null);
        }

        internal void DeleteAsset(ScriptableObject target)
        {
            if(target == null || !AssetDeletion.Confirm(target)) return;
            if(asset == null) Load(null, null);
            RefreshAssets();
        }

        private void DuplicateAsset(ScriptableObject target)
        {
            string from = AssetDatabase.GetAssetPath(target);
            string to = AssetDatabase.GenerateUniqueAssetPath(from);
            if(!AssetDatabase.CopyAsset(from, to)) return;
            var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(to);
            DefinitionAssets.ResetId(copy, to);
            AssetDatabase.SaveAssets();
            RefreshAssets();
            RecentAssets.Remember(copy);
            Load(copy, null);
        }

        private void PingAsset()
        {
            if(asset != null) EditorGUIUtility.PingObject(asset);
        }
    }
}
