using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public sealed class CombatDatabaseWindow : EditorWindow
    {
        private enum ViewMode { Grid, List }
        private enum SortMode { Name, Id, FileName, Type, Folder }

        private const string k_Title = "Combat Database";
        private const string k_View = "Db.View";
        private const string k_Sort = "Db.Sort";
        private const string k_Descending = "Db.Descending";
        private const string k_Size = "Db.Size";
        private const string k_Inspector = "Db.Inspector";
        private const string k_Info = "Db.Info";
        private const string k_TypeBadge = "Db.TypeBadge";
        private const string k_Counts = "Db.Counts";
        private const string k_Empty = "Db.EmptyCategories";
        private const string k_Swatches = "Db.Swatches";
        private const string k_Group = "Db.Group";
        private const float k_MinSize = 64f;
        private const float k_MaxSize = 160f;
        private const int k_MaxTiles = 3000;

        [SerializeField] private string categoryKey = DatabaseCatalog.k_All;
        [SerializeField] private Object selected;

        private readonly List<Object> items = new();
        private readonly Dictionary<string, int> idCounts = new();
        private List<DatabaseCategory> categories = new();
        private ToolbarSearchField search;
        private ToolbarButton filterButton;
        private ToggleButtonGroup labels;
        private ToggleButtonGroup views;
        private Slider size;
        private ListView categoryList;
        private ScrollView grid;
        private MultiColumnListView table;
        private VisualElement inspector;
        private VisualElement inspectorPane;
        private VisualElement detailsCard;
        private SerializedObject watched;
        private Label status;
        private bool duplicatesOnly;
        private bool missingIconsOnly;
        private bool rebuildQueued;
        private bool rescanQueued;

        private DatabaseCategory Current => categories.FirstOrDefault(c => c.Key == categoryKey) ?? categories.FirstOrDefault();
        private static ViewMode View => (ViewMode)StudioPrefs.GetInt(k_View, 0);
        private static float TileSize => Mathf.Clamp(StudioPrefs.GetFloat(k_Size, 96f), k_MinSize, k_MaxSize);

        [MenuItem(StudioWindows.k_Menu + "Database", false, 3)]
        public static void Open()
        {
            var window = GetWindow<CombatDatabaseWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += QueueRescan;
            StudioPrefs.Changed += OnPrefsChanged;
            AssetChanges.Changed += OnAssetsChanged;
            AssetChanges.CreatedOrDeleted += OnAssetsCreatedOrDeleted;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= QueueRescan;
            StudioPrefs.Changed -= OnPrefsChanged;
            AssetChanges.Changed -= OnAssetsChanged;
            AssetChanges.CreatedOrDeleted -= OnAssetsCreatedOrDeleted;
            watched?.Dispose();
            watched = null;
        }

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(grid == null) return;
            var changed = new HashSet<string>(guids);
            foreach(var tile in grid.Query<VisualElement>(className: "cg-tile").ToList())
            {
                if(!(tile.userData is Object asset) || !changed.Contains(AssetChanges.GuidOf(asset))) continue;
                var replacement = Tile(asset, DatabaseCatalog.CategoryOf(categories, asset));
                tile.parent.Insert(tile.parent.IndexOf(tile), replacement);
                tile.RemoveFromHierarchy();
            }
            if(View == ViewMode.List) table.RefreshItems();
            categoryList.RefreshItems();
            if(selected == null || detailsCard?.parent == null || !changed.Contains(AssetChanges.GuidOf(selected))) return;
            var card = DetailsCard();
            detailsCard.parent.Insert(detailsCard.parent.IndexOf(detailsCard), card);
            detailsCard.RemoveFromHierarchy();
            detailsCard = card;
        }

        private void OnAssetsCreatedOrDeleted(IReadOnlyCollection<string> guids) => QueueRescan();

        private void QueueRescan()
        {
            if(grid == null || rescanQueued) return;
            rescanQueued = true;
            grid.schedule.Execute(() =>
            {
                rescanQueued = false;
                Rescan();
            }).ExecuteLater(250);
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-studio");
            root.AddToClassList("cg-database");
            root.Add(BuildToolbar());

            var split = new TwoPaneSplitView(0, 180f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-db-split" };
            split.AddToClassList("cg-studio__split");
            split.Add(BuildCategories());
            var inner = new TwoPaneSplitView(1, 320f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-db-inner" };
            inner.Add(BuildCenter());
            inspectorPane = BuildInspector();
            inner.Add(inspectorPane);
            split.Add(inner);
            root.Add(split);

            root.RegisterCallback<KeyDownEvent>(OnKey);
            Rescan();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("cg-toolbar");
            toolbar.Add(CombatEditorStyles.Text(k_Title, "cg-toolbar__title"));

            labels = new ToggleButtonGroup { isMultipleSelection = false, allowEmptySelection = false, tooltip = "What each entry is called" };
            labels.AddToClassList("cg-toolbar__group");
            labels.Add(new Button { text = "Name" });
            labels.Add(new Button { text = "ID" });
            labels.Add(new Button { text = "File" });
            labels.SetValueWithoutNotify(State(3, (int)StudioPrefs.Label));
            labels.RegisterValueChangedCallback(evt => StudioPrefs.Label = (AssetLabel)First(evt.newValue));
            toolbar.Add(labels);

            var create = new ToolbarButton(CreateInCategory) { text = "New", tooltip = "Make a new entry in this category" };
            create.AddToClassList("cg-toolbar__new");
            toolbar.Add(create);
            toolbar.Add(new ToolbarSpacer { flex = true });

            size = new Slider(k_MinSize, k_MaxSize) { value = TileSize, tooltip = "Tile size" };
            size.AddToClassList("cg-toolbar__size");
            size.RegisterValueChangedCallback(evt =>
            {
                StudioPrefs.Store(k_Size, evt.newValue);
                ApplyTileSize();
            });
            toolbar.Add(size);

            views = new ToggleButtonGroup { isMultipleSelection = false, allowEmptySelection = false };
            views.AddToClassList("cg-toolbar__group");
            views.Add(new Button { text = "Grid" });
            views.Add(new Button { text = "List" });
            views.SetValueWithoutNotify(State(2, (int)View));
            views.RegisterValueChangedCallback(evt => StudioPrefs.SetInt(k_View, First(evt.newValue)));
            toolbar.Add(views);

            ToolbarButton sort = null;
            sort = new ToolbarButton(() => ShowSortMenu(sort.worldBound)) { text = "Sort" };
            toolbar.Add(sort);
            filterButton = new ToolbarButton(() => ShowFilterMenu(filterButton.worldBound)) { text = "Filter" };
            filterButton.AddToClassList("cg-toolbar__filter");
            toolbar.Add(filterButton);

            search = new ToolbarSearchField { tooltip = "Search names, IDs, files and details" };
            search.AddToClassList("cg-toolbar__search");
            search.RegisterValueChangedCallback(_ => RefreshItems());
            toolbar.Add(search);

            ToolbarButton view = null;
            view = new ToolbarButton(() => ShowViewMenu(view.worldBound)) { text = "View" };
            toolbar.Add(view);
            toolbar.Add(StudioWindows.Menu());
            return toolbar;
        }

        private VisualElement BuildCategories()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            pane.Add(CombatEditorStyles.Header("Categories"));
            categoryList = new ListView
            {
                fixedItemHeight = 24f,
                selectionType = SelectionType.Single,
                makeItem = () =>
                {
                    var row = new VisualElement();
                    row.AddToClassList("cg-category-row");
                    row.Add(new BlockIcon());
                    row.Add(CombatEditorStyles.Text(null, "cg-category-row__title"));
                    row.Add(CombatEditorStyles.Text(null, "cg-category-row__count"));
                    return row;
                },
                bindItem = (row, index) =>
                {
                    var category = (DatabaseCategory)categoryList.itemsSource[index];
                    ((BlockIcon)row[0]).SetStyle(category.Style);
                    ((Label)row[1]).text = category.Title;
                    CombatEditorStyles.SetText(row[2], StudioPrefs.Get(k_Counts, true) ? category.Items.Count.ToString() : null);
                    row.EnableInClassList("cg-category-row--empty", category.Items.Count == 0);
                }
            };
            categoryList.AddToClassList("cg-asset-list");
            categoryList.selectionChanged += selection =>
            {
                if(!(selection.FirstOrDefault() is DatabaseCategory category) || category.Key == categoryKey) return;
                categoryKey = category.Key;
                RefreshItems();
            };
            pane.Add(categoryList);
            return pane;
        }

        private VisualElement BuildCenter()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            pane.AddToClassList("cg-db-center");

            grid = new ScrollView(ScrollViewMode.Vertical);
            grid.AddToClassList("cg-db-grid");
            grid.contentContainer.AddToClassList("cg-db-grid__content");
            pane.Add(grid);

            var columns = new Columns
            {
                MakeColumn("icon", string.Empty, 28f, false, MakeIconCell, BindIconCell),
                MakeColumn("name", "Name", 180f, true, () => CombatEditorStyles.Text(null, "cg-cell"), (e, i) => ((Label)e).text = StudioKinds.NameOf(items[i])),
                MakeColumn("id", "ID", 180f, true, () => CombatEditorStyles.Text(null, "cg-cell"), (e, i) => ((Label)e).text = DatabaseCatalog.IdOf(items[i]) ?? string.Empty),
                MakeColumn("file", "File", 140f, true, () => CombatEditorStyles.Text(null, "cg-cell"), (e, i) => ((Label)e).text = items[i].name),
                MakeColumn("type", "Type", 120f, true, () => CombatEditorStyles.Text(null, "cg-cell"), (e, i) => ((Label)e).text = DatabaseCatalog.CategoryOf(categories, items[i])?.Title),
                MakeColumn("info", "Details", 220f, true, () => CombatEditorStyles.Text(null, "cg-cell"), (e, i) => ((Label)e).text = DatabaseCatalog.InfoOf(items[i])),
                MakeColumn("folder", "Folder", 200f, true, () => CombatEditorStyles.Text(null, "cg-cell"), (e, i) => ((Label)e).text = FolderOf(items[i]))
            };
            columns["icon"].sortable = false;
            columns["icon"].optional = false;
            table = new MultiColumnListView(columns)
            {
                itemsSource = items,
                fixedItemHeight = 22f,
                selectionType = SelectionType.Single,
                sortingMode = ColumnSortingMode.Custom,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            table.AddToClassList("cg-db-table");
            table.columnSortingChanged += SortFromTable;
            table.selectionChanged += selection => Select(selection.FirstOrDefault() as Object, false);
            table.itemsChosen += chosen => OpenAsset(chosen.FirstOrDefault() as Object);
            table.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if(selected != null) AppendActions(evt.menu, selected);
            }));
            pane.Add(table);

            status = CombatEditorStyles.Text(null, "cg-db-status");
            pane.Add(status);
            return pane;
        }

        private static Column MakeColumn(string name, string title, float width, bool sortable, Func<VisualElement> make, Action<VisualElement, int> bind)
        {
            return new Column { name = name, title = title, width = width, stretchable = name == "info", sortable = sortable, optional = true, makeCell = make, bindCell = bind };
        }

        private VisualElement MakeIconCell()
        {
            var cell = new VisualElement();
            cell.AddToClassList("cg-cell-icon");
            cell.Add(new Image { scaleMode = ScaleMode.ScaleToFit });
            cell.Add(new BlockIcon());
            return cell;
        }

        private void BindIconCell(VisualElement cell, int index)
        {
            var asset = items[index];
            var image = (Image)cell[0];
            var icon = (BlockIcon)cell[1];
            var texture = Thumbnail(asset);
            image.image = texture;
            CombatEditorStyles.Show(image, texture != null);
            CombatEditorStyles.Show(icon, texture == null);
            icon.SetStyle(DatabaseCatalog.CategoryOf(categories, asset)?.Style);
            icon.SetTint(StudioPrefs.Get(k_Swatches, true) ? DatabaseCatalog.ColorOf(asset) : null);
        }

        private VisualElement BuildInspector()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            pane.AddToClassList("cg-db-inspector");
            pane.Add(CombatEditorStyles.Header("Details"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cg-detail__scroll");
            scroll.contentContainer.AddToClassList("cg-detail__content");
            inspector = scroll.contentContainer;
            pane.Add(scroll);
            return pane;
        }

        private void Rescan()
        {
            if(categoryList == null) return;
            UnitLibrary.Clear();
            categories = DatabaseCatalog.Build();
            idCounts.Clear();
            foreach(var asset in categories[0].Items)
            {
                string id = DatabaseCatalog.IdOf(asset);
                if(id == null) continue;
                idCounts.TryGetValue(id, out int count);
                idCounts[id] = count + 1;
            }
            RefreshCategories();
            RefreshItems();
        }

        private void RefreshCategories()
        {
            bool showEmpty = StudioPrefs.Get(k_Empty, true);
            var visible = categories.Where(c => showEmpty || c.Items.Count > 0 || c.IsAll || c.Key == categoryKey).ToList();
            categoryList.itemsSource = visible;
            categoryList.Rebuild();
            int index = visible.FindIndex(c => c.Key == categoryKey);
            if(index < 0)
            {
                categoryKey = DatabaseCatalog.k_All;
                index = 0;
            }
            categoryList.SetSelectionWithoutNotify(new[] { index });
        }

        private void RefreshItems()
        {
            if(grid == null || categories.Count == 0) return;
            var category = Current;
            var terms = StudioIndex.Terms(search != null ? search.value : null);
            items.Clear();
            foreach(var asset in category.Items)
            {
                if(asset == null) continue;
                if(duplicatesOnly && !IsDuplicate(asset)) continue;
                if(missingIconsOnly && (!DatabaseCatalog.HasSpriteField(asset) || DatabaseCatalog.SpriteOf(asset) != null)) continue;
                if(terms.Length > 0 && !StudioIndex.Matches(SearchText(asset), terms)) continue;
                items.Add(asset);
            }
            Sort();

            bool filtered = duplicatesOnly || missingIconsOnly;
            filterButton.text = filtered ? "Filter (on)" : "Filter";
            filterButton.EnableInClassList("cg-toolbar__filter--active", filtered);
            int duplicates = idCounts.Count(pair => pair.Value > 1);
            status.text = $"{items.Count} of {category.Items.Count} in {category.Title}" + (duplicates > 0 ? $" · {duplicates} duplicate ID(s)" : string.Empty);
            status.EnableInClassList("cg-db-status--warning", duplicates > 0);

            ApplyView();
            if(selected != null && !category.Items.Contains(selected)) Select(null, false);
            else ShowInspector();
        }

        private void ApplyView()
        {
            bool gridMode = View == ViewMode.Grid;
            CombatEditorStyles.Show(grid, gridMode);
            CombatEditorStyles.Show(table, !gridMode);
            CombatEditorStyles.Show(size, gridMode);
            CombatEditorStyles.Show(inspectorPane, StudioPrefs.Get(k_Inspector, true));
            views.SetValueWithoutNotify(State(2, (int)View));
            labels.SetValueWithoutNotify(State(3, (int)StudioPrefs.Label));
            if(gridMode) RebuildGrid();
            else
            {
                table.Rebuild();
                int index = selected != null ? items.IndexOf(selected) : -1;
                if(index >= 0) table.SetSelectionWithoutNotify(new[] { index });
                else table.ClearSelection();
            }
        }

        private void RebuildGrid()
        {
            grid.Clear();
            if(items.Count == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList("cg-outline-empty");
                empty.Add(CombatEditorStyles.Text("Nothing here", "cg-outline-empty__title"));
                var category = Current;
                if(category != null && (category.CanCreate || category.IsUnits))
                {
                    empty.Add(CombatEditorStyles.Text("Want to make a new one?", "cg-outline-empty__text"));
                    var create = new Button(CreateInCategory) { text = $"Create {Singular(category)}" };
                    create.AddToClassList("cg-outline-empty__create");
                    empty.Add(create);
                }
                grid.Add(empty);
                return;
            }

            bool group = StudioPrefs.Get(k_Group, true) && Current.IsAll && StudioPrefs.GetInt(k_Sort, 0) == (int)SortMode.Type;
            VisualElement container = null;
            DatabaseCategory last = null;
            int shown = 0;
            foreach(var asset in items)
            {
                if(shown++ >= k_MaxTiles) break;
                var category = DatabaseCatalog.CategoryOf(categories, asset);
                if(container == null || (group && category != last))
                {
                    if(group) grid.Add(CombatEditorStyles.Text(category?.Title, "cg-db-grid__group"));
                    container = new VisualElement();
                    container.AddToClassList("cg-db-grid__tiles");
                    grid.Add(container);
                    last = category;
                }
                container.Add(Tile(asset, category));
            }
            if(items.Count > k_MaxTiles) grid.Add(CombatEditorStyles.Text($"Showing the first {k_MaxTiles}. Search or pick a category to narrow it down.", "cg-note"));
        }

        private VisualElement Tile(Object asset, DatabaseCategory category)
        {
            float tile = TileSize;
            var element = new VisualElement { userData = asset };
            element.AddToClassList("cg-tile");
            if(category != null) element.AddToClassList(category.Style);
            element.EnableInClassList("cg-tile--selected", asset == selected);
            element.style.width = tile;

            var thumb = new VisualElement();
            thumb.AddToClassList("cg-tile__thumb");
            thumb.style.height = tile - 18f;
            var texture = Thumbnail(asset);
            if(texture != null)
            {
                var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("cg-tile__image");
                thumb.Add(image);
            }
            else
            {
                var icon = new BlockIcon();
                icon.AddToClassList("cg-tile__glyph");
                icon.SetStyle(category?.Style);
                var color = StudioPrefs.Get(k_Swatches, true) ? DatabaseCatalog.ColorOf(asset) : null;
                icon.SetTint(color);
                float glyph = Mathf.Round(tile * 0.32f);
                icon.style.width = glyph;
                icon.style.height = glyph;
                thumb.Add(icon);
            }
            var swatch = StudioPrefs.Get(k_Swatches, true) ? DatabaseCatalog.ColorOf(asset) : null;
            if(swatch.HasValue) thumb.style.borderBottomColor = swatch.Value;
            thumb.EnableInClassList("cg-tile__thumb--swatch", swatch.HasValue);
            if(IsDuplicate(asset))
            {
                var badge = CombatEditorStyles.Text("!", "cg-asset-row__problem");
                badge.AddToClassList("cg-tile__badge");
                badge.tooltip = $"Another definition uses the ID {DatabaseCatalog.IdOf(asset)}";
                thumb.Add(badge);
            }
            if(StudioPrefs.Get(k_TypeBadge, false) && Current.IsAll && category != null) thumb.Add(CombatEditorStyles.Text(category.Title, "cg-tile__type"));
            element.Add(thumb);

            var name = CombatEditorStyles.Text(StudioPrefs.LabelOf(asset), "cg-tile__name");
            element.Add(name);
            if(StudioPrefs.Get(k_Info, true)) element.Add(CombatEditorStyles.Text(DatabaseCatalog.InfoOf(asset), "cg-tile__info"));
            element.tooltip = $"{StudioKinds.NameOf(asset)}\n{DatabaseCatalog.IdOf(asset) ?? asset.name}\n{AssetDatabase.GetAssetPath(asset)}";

            element.RegisterCallback<ClickEvent>(evt =>
            {
                if(evt.clickCount >= 2) OpenAsset(asset);
                else Select(asset, true);
            });
            element.AddManipulator(new ContextualMenuManipulator(evt => AppendActions(evt.menu, asset)));
            return element;
        }

        private Texture Thumbnail(Object asset)
        {
            var sprite = DatabaseCatalog.SpriteOf(asset);
            if(sprite != null) return AssetPreview.GetAssetPreview(sprite) ?? sprite.texture;
            if(asset is GameObject unit)
            {
                var preview = AssetPreview.GetAssetPreview(unit);
                if(preview == null && AssetPreview.IsLoadingAssetPreviews()) grid.schedule.Execute(RebuildGridLater).ExecuteLater(250);
                return preview;
            }
            return null;
        }

        private void RebuildGridLater()
        {
            if(rebuildQueued) return;
            rebuildQueued = true;
            grid.schedule.Execute(() =>
            {
                rebuildQueued = false;
                if(View == ViewMode.Grid) RebuildGrid();
                else table.RefreshItems();
            }).ExecuteLater(100);
        }

        private void ApplyTileSize()
        {
            if(View == ViewMode.Grid) RebuildGrid();
        }

        private void Select(Object asset, bool rebuildTiles)
        {
            RecentAssets.Remember(asset);
            selected = asset;
            if(rebuildTiles)
            {
                foreach(var tile in grid.Query<VisualElement>(className: "cg-tile").ToList())
                {
                    tile.EnableInClassList("cg-tile--selected", tile.userData as Object == asset);
                }
            }
            ShowInspector();
        }

        private void ShowInspector()
        {
            if(inspector == null) return;
            inspector.Clear();
            if(!StudioPrefs.Get(k_Inspector, true)) return;
            if(selected == null)
            {
                inspector.Add(CombatEditorStyles.Text("Pick something to see its details", "cg-placeholder"));
                return;
            }

            detailsCard = DetailsCard();
            inspector.Add(detailsCard);

            Object shownObject = selected is GameObject prefab && prefab.TryGetComponent<Unit>(out var unit) ? unit : selected;
            inspector.Add(new InspectorElement(shownObject));
            watched?.Dispose();
            watched = new SerializedObject(shownObject);
            var watcher = new VisualElement();
            watcher.AddToClassList("cg-watcher");
            inspector.Add(watcher);
            string guid = AssetChanges.GuidOf(selected);
            watcher.TrackSerializedObjectValue(watched, _ =>
            {
                if(guid != null) OnAssetsChanged(new[] { guid });
            });
        }

        private VisualElement DetailsCard()
        {
            var category = DatabaseCatalog.CategoryOf(categories, selected);
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            if(category != null) card.AddToClassList(category.Style);
            card.Add(CombatEditorStyles.Text(category?.Title.ToUpperInvariant(), "cg-card__chip"));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("cg-card__title-row");
            var icon = new BlockIcon();
            icon.AddToClassList("cg-card__icon");
            icon.SetStyle(category?.Style);
            icon.SetTint(DatabaseCatalog.ColorOf(selected));
            titleRow.Add(icon);
            titleRow.Add(CombatEditorStyles.Text(StudioKinds.NameOf(selected), "cg-card__title"));
            card.Add(titleRow);

            string id = DatabaseCatalog.IdOf(selected);
            if(id != null)
            {
                var idRow = new VisualElement();
                idRow.AddToClassList("cg-id-row");
                idRow.Add(CombatEditorStyles.Text(id, "cg-id-row__id"));
                var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = id) { text = "Copy", tooltip = "Copy the ID" };
                copy.AddToClassList("cg-card__button");
                idRow.Add(copy);
                card.Add(idRow);
                if(IsDuplicate(selected)) card.Add(CombatEditorStyles.Text("Another definition has this ID", "cg-card__warning"));
            }
            card.Add(CombatEditorStyles.Text(AssetDatabase.GetAssetPath(selected), "cg-card__summary"));
            card.Add(CombatEditorStyles.Text(DatabaseCatalog.InfoOf(selected), "cg-card__summary"));

            var actions = new VisualElement();
            actions.AddToClassList("cg-chips");
            actions.Add(Action("Open", () => OpenAsset(selected)));
            actions.Add(Action("Ping", () => EditorGUIUtility.PingObject(selected)));
            actions.Add(Action("Duplicate", () => Duplicate(selected)));
            var delete = Action("Delete", () => Delete(selected));
            delete.AddToClassList("cg-card__button--danger");
            delete.SetEnabled(AssetDeletion.CanDelete(selected));
            actions.Add(delete);
            card.Add(actions);

            if(!(selected is GameObject))
            {
                var users = UnitLibrary.UsersOf(selected);
                if(users.Count > 0)
                {
                    var used = CombatEditorStyles.Text(users.Count == 1 ? $"Used by {StudioKinds.NameOf(users[0])}" : $"Used by {users.Count} units", "cg-note");
                    used.tooltip = string.Join("\n", users.Select(StudioKinds.NameOf));
                    card.Add(used);
                }
            }
            return card;
        }

        private void OpenAsset(Object asset)
        {
            if(asset == null) return;
            if(asset is GameObject unit && UnitLibrary.IsUnit(unit))
            {
                CombatStudioWindow.OpenUnit(unit);
                return;
            }
            if(asset is ScriptableObject scriptable && StudioKinds.IndexOf(scriptable.GetType()) >= 0)
            {
                CombatStudioWindow.Open(scriptable);
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void AppendActions(DropdownMenu menu, Object asset)
        {
            menu.AppendAction("Open", _ => OpenAsset(asset));
            menu.AppendAction("Ping", _ => EditorGUIUtility.PingObject(asset));
            menu.AppendAction("Select in Project", _ => Selection.activeObject = asset);
            menu.AppendSeparator();
            string id = DatabaseCatalog.IdOf(asset);
            menu.AppendAction("Copy ID", _ => EditorGUIUtility.systemCopyBuffer = id, id != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Copy Name", _ => EditorGUIUtility.systemCopyBuffer = StudioKinds.NameOf(asset));
            menu.AppendAction("Copy Path", _ => EditorGUIUtility.systemCopyBuffer = AssetDatabase.GetAssetPath(asset));
            menu.AppendSeparator();
            menu.AppendAction("Duplicate", _ => Duplicate(asset));
            menu.AppendAction("Delete...", _ => Delete(asset), AssetDeletion.CanDelete(asset) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            if(asset is DamagePipeline pipeline) menu.AppendAction("Test Pipeline", _ => PipelineTesterWindow.Open(pipeline));
            if(asset is GameObject unit) menu.AppendAction("Test Damage", _ => PipelineTesterWindow.OpenWithDefender(unit));
            StudioWindows.AppendAssetActions(menu, asset);
        }

        private void Delete(Object asset)
        {
            if(!AssetDeletion.Confirm(asset)) return;
            if(selected == null || selected == asset) selected = null;
            Rescan();
        }

        private void Duplicate(Object asset)
        {
            string from = AssetDatabase.GetAssetPath(asset);
            if(string.IsNullOrEmpty(from)) return;
            string to = AssetDatabase.GenerateUniqueAssetPath(from);
            if(!AssetDatabase.CopyAsset(from, to)) return;
            var copy = AssetDatabase.LoadAssetAtPath<Object>(to);
            if(copy is ScriptableObject scriptable) DefinitionAssets.ResetId(scriptable, to);
            AssetDatabase.SaveAssets();
            selected = copy;
            Rescan();
        }

        private void CreateInCategory()
        {
            var category = Current;
            if(category == null) return;
            if(category.IsUnits)
            {
                CombatStudioWindow.NewUnit();
                return;
            }
            if(!category.CanCreate) return;
            string folder = selected != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(selected))?.Replace('\\', '/') : null;
            if(string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets")) folder = category.Items.Count > 0 ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(category.Items[0]))?.Replace('\\', '/') : "Assets";
            string singular = Singular(category);
            string path = EditorUtility.SaveFilePanelInProject($"New {singular}", singular, "asset", $"Choose where to save the new {singular.ToLowerInvariant()}", folder);
            if(string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance(category.Type);
            AssetDatabase.CreateAsset(asset, path);
            DefinitionAssets.ResetId(asset, path);
            AssetDatabase.SaveAssets();
            selected = asset;
            Rescan();
        }

        private static string Singular(DatabaseCategory category)
        {
            if(category.Type == null) return "Entry";
            if(category.IsUnits) return "Unit";
            string name = ObjectNames.NicifyVariableName(category.Type.Name);
            return name.EndsWith(" Definition", StringComparison.Ordinal) ? name.Substring(0, name.Length - 11) : name;
        }

        private void Sort()
        {
            var mode = (SortMode)StudioPrefs.GetInt(k_Sort, 0);
            bool descending = StudioPrefs.Get(k_Descending, false);
            Comparison<Object> compare = mode switch
            {
                SortMode.Id => (a, b) => Compare(DatabaseCatalog.IdOf(a) ?? a.name, DatabaseCatalog.IdOf(b) ?? b.name),
                SortMode.FileName => (a, b) => Compare(a.name, b.name),
                SortMode.Type => (a, b) =>
                {
                    int byType = categories.IndexOf(DatabaseCatalog.CategoryOf(categories, a)).CompareTo(categories.IndexOf(DatabaseCatalog.CategoryOf(categories, b)));
                    return byType != 0 ? byType : Compare(StudioPrefs.LabelOf(a), StudioPrefs.LabelOf(b));
                },
                SortMode.Folder => (a, b) => Compare(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)),
                _ => (a, b) => Compare(StudioPrefs.LabelOf(a), StudioPrefs.LabelOf(b))
            };
            items.Sort(compare);
            if(descending) items.Reverse();
        }

        private void SortFromTable()
        {
            var column = table.sortedColumns.FirstOrDefault();
            if(column == null) return;
            var mode = column.columnName switch
            {
                "id" => SortMode.Id,
                "file" => SortMode.FileName,
                "type" => SortMode.Type,
                "folder" => SortMode.Folder,
                _ => SortMode.Name
            };
            StudioPrefs.Store(k_Sort, (int)mode);
            StudioPrefs.Store(k_Descending, column.direction == SortDirection.Descending);
            Sort();
            table.RefreshItems();
        }

        private static int Compare(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

        private bool IsDuplicate(Object asset)
        {
            string id = DatabaseCatalog.IdOf(asset);
            return id != null && idCounts.TryGetValue(id, out int count) && count > 1;
        }

        private string SearchText(Object asset)
        {
            return string.Join(" ", StudioKinds.NameOf(asset), DatabaseCatalog.IdOf(asset), asset.name, DatabaseCatalog.CategoryOf(categories, asset)?.Title, DatabaseCatalog.InfoOf(asset)).ToLowerInvariant();
        }

        private static string FolderOf(Object asset) => Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset))?.Replace('\\', '/');

        private void ShowSortMenu(Rect rect)
        {
            var menu = new GenericMenu();
            foreach(SortMode mode in Enum.GetValues(typeof(SortMode)))
            {
                StudioPrefs.Choice(menu, ObjectNames.NicifyVariableName(mode.ToString()), k_Sort, (int)mode, 0);
            }
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Descending", k_Descending, false);
            menu.DropDown(rect);
        }

        private void ShowFilterMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate IDs Only"), duplicatesOnly, () =>
            {
                duplicatesOnly = !duplicatesOnly;
                RefreshItems();
            });
            menu.AddItem(new GUIContent("Missing Icons Only"), missingIconsOnly, () =>
            {
                missingIconsOnly = !missingIconsOnly;
                RefreshItems();
            });
            if(duplicatesOnly || missingIconsOnly)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Clear Filters"), false, () =>
                {
                    duplicatesOnly = false;
                    missingIconsOnly = false;
                    RefreshItems();
                });
            }
            menu.DropDown(rect);
        }

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Tiles"));
            StudioPrefs.Toggle(menu, "Details Line", k_Info, true);
            StudioPrefs.Toggle(menu, "Color Swatches", k_Swatches, true);
            StudioPrefs.Toggle(menu, "Type Badges", k_TypeBadge, false);
            StudioPrefs.Toggle(menu, "Group by Type", k_Group, true);
            menu.AddSeparator(string.Empty);
            menu.AddDisabledItem(new GUIContent("Panels"));
            StudioPrefs.Toggle(menu, "Details Panel", k_Inspector, true);
            StudioPrefs.Toggle(menu, "Category Counts", k_Counts, true);
            StudioPrefs.Toggle(menu, "Empty Categories", k_Empty, true);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Labels(menu);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Rescan Project"), false, Rescan);
            menu.DropDown(rect);
        }

        private void OnKey(KeyDownEvent evt)
        {
            if(selected == null || !(evt.target is VisualElement element) || inspectorPane.Contains(element) || search.Contains(element)) return;
            if(evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                OpenAsset(selected);
                evt.StopPropagation();
            }
            else if(evt.keyCode == KeyCode.Delete || (evt.keyCode == KeyCode.Backspace && evt.actionKey))
            {
                Delete(selected);
                evt.StopPropagation();
            }
        }

        private void OnPrefsChanged()
        {
            if(categoryList == null) return;
            RefreshCategories();
            RefreshItems();
        }

        private static ToggleButtonGroupState State(int length, int active)
        {
            var options = new List<bool>();
            for(int i = 0; i < length; i++)
            {
                options.Add(i == active);
            }
            return ToggleButtonGroupState.CreateFromOptions(options);
        }

        private static int First(ToggleButtonGroupState state)
        {
            for(int i = 0; i < state.length; i++)
            {
                if(state[i]) return i;
            }
            return 0;
        }

        private static Button Action(string text, Action run)
        {
            var button = new Button(run) { text = text };
            button.AddToClassList("cg-card__button");
            return button;
        }
    }
}
