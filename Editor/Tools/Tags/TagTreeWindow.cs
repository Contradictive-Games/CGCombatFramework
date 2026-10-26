using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public sealed class TagTreeWindow : EditorWindow
    {
        public const string k_Title = "Tag Tree";
        private const string k_Ids = "Tags.Ids";
        private const string k_Counts = "Tags.Counts";
        private const string k_Drop = "cg-tag-row--drop";
        private const string k_Dragging = "cg-tag-row--dragging";

        [SerializeField] private CombatTag selected;

        private readonly List<CombatTag> tags = new();
        private readonly Dictionary<CombatTag, int> ids = new();
        private readonly Dictionary<CombatTag, int> usage = new();
        private TreeView tree;
        private ToolbarSearchField search;
        private VisualElement details;
        private VisualElement rootDrop;
        private Label status;
        private bool counted;

        [MenuItem(CombatTools.k_Menu + k_Title, false, 26)]
        public static void Open()
        {
            var window = GetWindow<TagTreeWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
        }

        public static void Open(CombatTag tag)
        {
            var window = GetWindow<TagTreeWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            window.selected = tag;
            window.Rebuild();
        }

        private void OnEnable()
        {
            StudioPrefs.Changed += Rebuild;
            AssetChanges.Changed += OnAssetsChanged;
            AssetChanges.CreatedOrDeleted += OnAssetsChanged;
            Undo.undoRedoPerformed += Rebuild;
        }

        private void OnDisable()
        {
            StudioPrefs.Changed -= Rebuild;
            AssetChanges.Changed -= OnAssetsChanged;
            AssetChanges.CreatedOrDeleted -= OnAssetsChanged;
            Undo.undoRedoPerformed -= Rebuild;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatTools.Apply(root, "cg-tags");

            var toolbar = CombatTools.Toolbar(k_Title);
            toolbar.Add(new ToolbarButton(() => CreateTag(null)) { text = "New Tag" });
            toolbar.Add(new ToolbarButton(() => CreateTag(selected)) { text = "New Child", tooltip = "Make a tag under the selected one" });
            toolbar.Add(new ToolbarButton(CountUsages) { text = "Count Usages", tooltip = "Count how many assets point at each tag" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            search = new ToolbarSearchField();
            search.AddToClassList("cg-toolbar__search");
            search.RegisterValueChangedCallback(_ => Rebuild());
            toolbar.Add(search);
            CombatTools.Finish(toolbar, ShowViewMenu);
            root.Add(toolbar);

            var split = new TwoPaneSplitView(0, 300f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-tags-split" };
            split.AddToClassList("cg-studio__split");
            var left = new VisualElement();
            left.AddToClassList("cg-pane");
            rootDrop = CombatEditorStyles.Text("Drop here to make a top level tag", "cg-tags__root-drop");
            left.Add(rootDrop);
            tree = new TreeView
            {
                fixedItemHeight = 22f,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                unbindItem = (row, _) => row.userData = null
            };
            tree.AddToClassList("cg-outline");
            tree.selectionChanged += selection =>
            {
                var tag = selection.FirstOrDefault() as CombatTag;
                if(tag == null || tag == selected) return;
                selected = tag;
                ShowDetails();
            };
            left.Add(tree);
            split.Add(left);

            var right = new ScrollView(ScrollViewMode.Vertical);
            right.AddToClassList("cg-detail__scroll");
            right.contentContainer.AddToClassList("cg-detail__content");
            details = right.contentContainer;
            split.Add(right);
            root.Add(split);
            status = CombatEditorStyles.Text(null, "cg-db-status");
            root.Add(status);
            Rebuild();
        }

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(tree == null) return;
            RebuildTree();
        }

        private void Rebuild()
        {
            if(tree == null) return;
            RebuildTree();
            ShowDetails();
        }

        private void RebuildTree()
        {
            tags.Clear();
            foreach(var guid in AssetDatabase.FindAssets("t:CombatTag"))
            {
                var tag = AssetDatabase.LoadAssetAtPath<CombatTag>(AssetDatabase.GUIDToAssetPath(guid));
                if(tag != null) tags.Add(tag);
            }

            ids.Clear();
            int next = 1;
            foreach(var tag in tags)
            {
                ids[tag] = next++;
            }

            var terms = StudioIndex.Terms(search != null ? search.value : null);
            var keep = new HashSet<CombatTag>();
            foreach(var tag in tags)
            {
                if(terms.Length > 0 && !StudioIndex.Matches(SearchText(tag), terms)) continue;
                foreach(var each in Chain(tag))
                {
                    keep.Add(each);
                }
            }

            var looped = new HashSet<CombatTag>(tags.Where(InLoop));
            var roots = tags.Where(t => keep.Contains(t) && (t.Parent == null || !tags.Contains(t.Parent) || looped.Contains(t))).OrderBy(Label).ToList();
            var items = roots.Select(t => Item(t, keep, looped, new HashSet<CombatTag>())).ToList();
            tree.SetRootItems(items);
            tree.Rebuild();
            tree.ExpandAll();
            if(selected != null && ids.TryGetValue(selected, out int id)) tree.SetSelectionByIdWithoutNotify(new[] { id });

            int top = tags.Count(t => t.Parent == null);
            status.text = $"{tags.Count} tags · {top} top level" + (looped.Count > 0 ? $" · {looped.Count} in parent loops" : string.Empty);
            status.EnableInClassList("cg-db-status--warning", looped.Count > 0);
        }

        private TreeViewItemData<CombatTag> Item(CombatTag tag, HashSet<CombatTag> keep, HashSet<CombatTag> looped, HashSet<CombatTag> visiting)
        {
            visiting.Add(tag);
            var children = looped.Contains(tag) ? new List<TreeViewItemData<CombatTag>>() : tags
                .Where(t => t.Parent == tag && keep.Contains(t) && !visiting.Contains(t) && !looped.Contains(t))
                .OrderBy(Label)
                .Select(t => Item(t, keep, looped, visiting))
                .ToList();
            return new TreeViewItemData<CombatTag>(ids[tag], tag, children);
        }

        private static IEnumerable<CombatTag> Chain(CombatTag tag)
        {
            var seen = new HashSet<CombatTag>();
            for(var each = tag; each != null && seen.Add(each); each = each.Parent)
            {
                yield return each;
            }
        }

        private static bool InLoop(CombatTag tag)
        {
            var seen = new HashSet<CombatTag>();
            for(var each = tag.Parent; each != null; each = each.Parent)
            {
                if(each == tag) return true;
                if(!seen.Add(each)) return false;
            }
            return false;
        }

        private static string Label(CombatTag tag) => StudioPrefs.LabelOf(tag);

        private static string SearchText(CombatTag tag) => $"{tag.name} {tag.DisplayName} {tag.Id}".ToLowerInvariant();

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("cg-tree-row");
            row.AddToClassList("cg-tag-row");
            var icon = new BlockIcon();
            icon.SetStyle("cg-kind-tag");
            row.Add(icon);
            row.Add(CombatEditorStyles.Text(null, "cg-tree-row__title"));
            row.Add(CombatEditorStyles.Text(null, "cg-tree-row__summary"));
            row.Add(CombatEditorStyles.Text(null, "cg-tag-row__count"));
            row.AddManipulator(new TagDragger(this, row));
            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if(!(row.userData is CombatTag tag)) return;
                evt.menu.AppendAction("New Child Tag", _ => CreateTag(tag));
                evt.menu.AppendAction("Clear Parent", _ => SetParent(tag, null), tag.Parent != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Select", _ => Selection.activeObject = tag);
                evt.menu.AppendAction("Ping", _ => EditorGUIUtility.PingObject(tag));
                StudioWindows.AppendAssetActions(evt.menu, tag);
            }));
            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            var tag = tree.GetItemDataForIndex<CombatTag>(index);
            row.userData = tag;
            if(tag == null) return;
            bool loop = InLoop(tag);
            row.EnableInClassList("cg-tag-row--loop", loop);
            ((Label)row[1]).text = Label(tag);
            CombatEditorStyles.SetText(row[2], loop ? "parent loop" : StudioPrefs.Get(k_Ids, true) ? tag.Id : null);
            string count = null;
            if(counted && StudioPrefs.Get(k_Counts, true))
            {
                usage.TryGetValue(tag, out int uses);
                count = uses == 1 ? "1 use" : $"{uses} uses";
            }
            CombatEditorStyles.SetText(row[3], count);
            row.tooltip = $"{StudioKinds.NameOf(tag)}\n{tag.Id}\n{AssetDatabase.GetAssetPath(tag)}";
        }

        private void ShowDetails()
        {
            if(details == null) return;
            details.Clear();
            if(selected == null)
            {
                details.Add(CombatEditorStyles.Text("Pick a tag to see its details. Drag tags onto each other to change parents", "cg-placeholder"));
                return;
            }

            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-kind-tag");
            card.Add(CombatEditorStyles.Text("TAG", "cg-card__chip"));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("cg-card__title-row");
            var icon = new BlockIcon();
            icon.AddToClassList("cg-card__icon");
            icon.SetStyle("cg-kind-tag");
            titleRow.Add(icon);
            titleRow.Add(CombatEditorStyles.Text(StudioKinds.NameOf(selected), "cg-card__title"));
            card.Add(titleRow);
            card.Add(CombatEditorStyles.Text(selected.Id, "cg-card__summary"));
            var path = Chain(selected).Reverse().Select(t => StudioKinds.NameOf(t));
            card.Add(CombatEditorStyles.Text(string.Join(" › ", path), "cg-card__summary"));
            var children = tags.Where(t => t.Parent == selected).ToList();
            var below = tags.Count(t => t != selected && Chain(t).Contains(selected));
            card.Add(CombatEditorStyles.Text($"{Count(children.Count, "child", "children")} · {Count(below, "tag", "tags")} below it. Has Tag checks for this tag also pass for every tag below it", "cg-note"));

            var actions = new VisualElement();
            actions.AddToClassList("cg-chips");
            var tag = selected;
            actions.Add(CombatTools.Small("New Child", () => CreateTag(tag)));
            actions.Add(CombatTools.Small("Select", () => Selection.activeObject = tag));
            actions.Add(CombatTools.Small("Find Usages", () => UsageFinderWindow.Open(tag)));
            if(tag.Parent != null) actions.Add(CombatTools.Small("Clear Parent", () => SetParent(tag, null)));
            card.Add(actions);
            details.Add(card);

            var rename = new TextField("File Name") { value = selected.name, isDelayed = true };
            rename.RegisterValueChangedCallback(evt =>
            {
                if(string.IsNullOrWhiteSpace(evt.newValue) || evt.newValue == tag.name) return;
                string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(tag), evt.newValue);
                if(!string.IsNullOrEmpty(error)) Debug.LogWarning($"[{k_Title}] {error}");
            });
            details.Add(rename);
            details.Add(new InspectorElement(selected));
        }

        private static string Count(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";

        internal bool CanParent(CombatTag child, CombatTag parent)
        {
            if(child == null || child == parent) return false;
            if(parent == null) return true;
            return !Chain(parent).Contains(child);
        }

        internal void SetParent(CombatTag child, CombatTag parent)
        {
            if(!CanParent(child, parent)) return;
            Undo.RecordObject(child, parent == null ? "Clear Tag Parent" : "Set Tag Parent");
            child.Parent = parent;
            EditorUtility.SetDirty(child);
            selected = child;
            Rebuild();
        }

        internal IEnumerable<VisualElement> Rows() => tree.Query<VisualElement>(className: "cg-tag-row").Where(r => r.userData != null).ToList();

        internal VisualElement RootDrop => rootDrop;

        private void CreateTag(CombatTag parent)
        {
            string folder = parent != null ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(parent))?.Replace('\\', '/')
                : tags.Count > 0 ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(tags[0]))?.Replace('\\', '/') : "Assets";
            string name = parent != null ? $"{parent.name} Child" : "Tag";
            string path = EditorUtility.SaveFilePanelInProject(parent != null ? $"New Tag under {StudioKinds.NameOf(parent)}" : "New Tag", name, "asset", "Choose where to save the new tag", folder);
            if(string.IsNullOrEmpty(path)) return;
            var tag = CreateInstance<CombatTag>();
            tag.Parent = parent;
            AssetDatabase.CreateAsset(tag, path);
            DefinitionAssets.ResetId(tag, path);
            AssetDatabase.SaveAssets();
            selected = tag;
            Rebuild();
        }

        private void CountUsages()
        {
            usage.Clear();
            var candidates = UsageScanner.Candidates(true, true, true);
            var paths = new Dictionary<string, CombatTag>();
            foreach(var each in tags)
            {
                paths[AssetDatabase.GetAssetPath(each)] = each;
            }
            try
            {
                for(int i = 0; i < candidates.Count; i++)
                {
                    if(i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Counting Tag Usages", candidates[i], i / (float)candidates.Count)) break;
                    foreach(var dependency in AssetDatabase.GetDependencies(candidates[i], false))
                    {
                        if(dependency == candidates[i] || !paths.TryGetValue(dependency, out var tag)) continue;
                        if(tag.Parent != null && AssetDatabase.GetAssetPath(tag.Parent) == candidates[i]) continue;
                        usage.TryGetValue(tag, out int count);
                        usage[tag] = count + 1;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            counted = true;
            if(!StudioPrefs.Get(k_Counts, true)) StudioPrefs.Set(k_Counts, true);
            tree.RefreshItems();
        }

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            StudioPrefs.Toggle(menu, "IDs", k_Ids, true);
            StudioPrefs.Toggle(menu, "Usage Counts", k_Counts, true);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Labels(menu);
            StudioPrefs.Behavior(menu);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Expand All"), false, () => tree.ExpandAll());
            menu.AddItem(new GUIContent("Collapse All"), false, () => tree.CollapseAll());
            menu.DropDown(rect);
        }

        private sealed class TagDragger : PointerManipulator
        {
            private const float k_Threshold = 5f;

            private readonly TagTreeWindow window;
            private readonly VisualElement row;
            private CombatTag dragged;
            private VisualElement over;
            private bool overRoot;
            private Vector3 start;
            private int pointerId = -1;
            private bool dragging;

            public TagDragger(TagTreeWindow window, VisualElement row)
            {
                this.window = window;
                this.row = row;
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnDown);
                target.RegisterCallback<PointerMoveEvent>(OnMove);
                target.RegisterCallback<PointerUpEvent>(OnUp);
                target.RegisterCallback<PointerCaptureOutEvent>(_ => End());
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnDown);
                target.UnregisterCallback<PointerMoveEvent>(OnMove);
                target.UnregisterCallback<PointerUpEvent>(OnUp);
            }

            private void OnDown(PointerDownEvent evt)
            {
                pointerId = -1;
                if(!CanStartManipulation(evt) || !StudioPrefs.DragToReorder || !(row.userData is CombatTag tag)) return;
                dragged = tag;
                pointerId = evt.pointerId;
                start = evt.position;
            }

            private void OnMove(PointerMoveEvent evt)
            {
                if(evt.pointerId != pointerId) return;
                if(!dragging)
                {
                    if((evt.position - start).sqrMagnitude < k_Threshold * k_Threshold) return;
                    dragging = true;
                    target.CapturePointer(pointerId);
                    row.AddToClassList(k_Dragging);
                    window.RootDrop.AddToClassList("cg-tags__root-drop--visible");
                }
                Track(evt.position);
                evt.StopPropagation();
            }

            private void Track(Vector2 position)
            {
                over?.RemoveFromClassList(k_Drop);
                over = null;
                overRoot = window.RootDrop.worldBound.Contains(position);
                window.RootDrop.EnableInClassList("cg-tags__root-drop--over", overRoot);
                if(overRoot) return;
                foreach(var each in window.Rows())
                {
                    if(!each.worldBound.Contains(position) || !(each.userData is CombatTag tag)) continue;
                    if(!window.CanParent(dragged, tag)) break;
                    over = each;
                    over.AddToClassList(k_Drop);
                    break;
                }
            }

            private void OnUp(PointerUpEvent evt)
            {
                if(evt.pointerId != pointerId) return;
                bool dropped = dragging;
                var tag = dragged;
                var parent = over?.userData as CombatTag;
                bool toRoot = overRoot;
                if(dropped && target.HasPointerCapture(pointerId)) target.ReleasePointer(pointerId);
                End();
                if(!dropped) return;
                evt.StopPropagation();
                if(toRoot) window.SetParent(tag, null);
                else if(parent != null && parent != tag.Parent) window.SetParent(tag, parent);
            }

            private void End()
            {
                pointerId = -1;
                dragging = false;
                row.RemoveFromClassList(k_Dragging);
                over?.RemoveFromClassList(k_Drop);
                over = null;
                overRoot = false;
                window.RootDrop.RemoveFromClassList("cg-tags__root-drop--visible");
                window.RootDrop.RemoveFromClassList("cg-tags__root-drop--over");
            }
        }
    }
}
