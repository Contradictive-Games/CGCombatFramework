using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class BlockPickerWindow : EditorWindow
    {
        private const float k_MinWidth = 300f;
        private const float k_Height = 400f;
        private const float k_RowHeight = 20f;
        private const float k_Indent = 14f;
        private const float k_Inset = 4f;

        private readonly HashSet<string> closed = new();
        private readonly List<PickerRow> rows = new();
        private BlockPickerModel model;
        private Action<Type> picked;
        private Type current;
        private ToolbarSearchField search;
        private ListView list;
        private Label paneTitle;
        private Label paneText;
        private Label paneExample;
        private int highlight = -1;
        private bool searching;
        private bool built;
        private bool shown;

        internal IReadOnlyList<PickerRow> Shown => rows;
        internal PickerRow Highlighted => highlight >= 0 && highlight < rows.Count ? rows[highlight] : null;

        public static void Show(Rect rect, BlockPickerModel model, Type current, Action<Type> picked)
        {
            if(model == null) return;
            var screen = GUIUtility.GUIToScreenRect(rect);
            var window = Create(model, current, picked);
            window.ShowAsDropDown(screen, new Vector2(Mathf.Max(k_MinWidth, screen.width), k_Height));
            window.shown = true;
        }

        internal static BlockPickerWindow Create(BlockPickerModel model, Type current, Action<Type> picked)
        {
            var window = CreateInstance<BlockPickerWindow>();
            window.model = model;
            window.current = current;
            window.picked = picked;
            window.Build();
            return window;
        }

        private void CreateGUI()
        {
            if(built) return;
            EditorApplication.delayCall += () =>
            {
                if(this != null) Close();
            };
        }

        private void Build()
        {
            built = true;
            wantsMouseMove = true;
            LoadClosed();
            foreach(var path in model.PathTo(current))
            {
                closed.Remove(path);
            }

            var root = rootVisualElement;
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-type-picker");

            var header = new VisualElement();
            header.AddToClassList("cg-type-picker__header");
            if(model.Category.HasValue)
            {
                var icon = new BlockIcon();
                icon.AddToClassList("cg-type-picker__header-icon");
                icon.SetCategory(model.Category.Value);
                header.Add(icon);
            }
            header.Add(CombatEditorStyles.Text(model.Title, "cg-type-picker__title"));
            root.Add(header);

            search = new ToolbarSearchField();
            search.AddToClassList("cg-type-picker__search");
            search.style.width = StyleKeyword.Auto;
            search.RegisterValueChangedCallback(evt => OnSearch(evt.newValue));
            root.Add(search);

            list = new ListView
            {
                fixedItemHeight = k_RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                focusable = false,
                itemsSource = rows,
                makeItem = () => new RowElement(this),
                bindItem = (element, index) => ((RowElement)element).Bind(index)
            };
            list.AddToClassList("cg-type-picker__list");
            list.RegisterCallback<PointerUpEvent>(_ => FocusSearch(), TrickleDown.TrickleDown);
            list.RegisterCallback<GeometryChangedEvent>(OnFirstLayout);
            root.Add(list);

            var pane = new ScrollView(ScrollViewMode.Vertical);
            pane.AddToClassList("cg-type-picker__pane");
            paneTitle = CombatEditorStyles.Text(null, "cg-type-picker__pane-title");
            paneText = CombatEditorStyles.Text(null, "cg-type-picker__pane-text");
            paneExample = CombatEditorStyles.Text(null, "cg-type-picker__pane-example");
            pane.Add(paneTitle);
            pane.Add(paneText);
            pane.Add(paneExample);
            root.Add(pane);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            Fill(model.Rows(closed));
            SetHighlight(Math.Max(IndexOfType(current), rows.Count > 0 ? 0 : -1), true);
            FocusSearch();
        }

        internal void Search(string query)
        {
            search.SetValueWithoutNotify(query);
            OnSearch(query);
        }

        internal bool Press(KeyCode key)
        {
            switch(key)
            {
                case KeyCode.DownArrow:
                    Move(1);
                    return true;
                case KeyCode.UpArrow:
                    Move(-1);
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Activate(highlight);
                    return true;
                case KeyCode.Escape:
                    Dismiss();
                    return true;
                case KeyCode.LeftArrow when !searching:
                    Collapse();
                    return true;
                case KeyCode.RightArrow when !searching:
                    Expand();
                    return true;
                default:
                    return false;
            }
        }

        internal void Activate(int index)
        {
            if(index < 0 || index >= rows.Count) return;
            var row = rows[index];
            if(row.Kind == PickerRowKind.Heading)
            {
                SetOpen(row.Path, closed.Contains(row.Path));
                FocusSearch();
                return;
            }
            var callback = picked;
            picked = null;
            Dismiss();
            callback?.Invoke(row.Type);
        }

        private void Dismiss()
        {
            if(shown) Close();
            else DestroyImmediate(this);
        }

        private void FocusSearch() => rootVisualElement.schedule.Execute(() => search?.Q<TextField>()?.Focus());

        private void OnFirstLayout(GeometryChangedEvent evt)
        {
            list.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);
            if(highlight >= 0) list.ScrollToItem(highlight);
        }

        private void OnSearch(string query)
        {
            var keep = Highlighted;
            searching = BlockPickerModel.IsSearch(query);
            Fill(searching ? model.Search(query) : model.Rows(closed));
            int index = searching ? (rows.Count > 0 ? 0 : -1) : Math.Max(IndexOf(keep), rows.Count > 0 ? 0 : -1);
            highlight = -1;
            SetHighlight(index, true);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if(Press(evt.keyCode)) evt.StopPropagation();
        }

        private void Fill(IReadOnlyList<PickerRow> source)
        {
            rows.Clear();
            for(int i = 0; i < source.Count; i++)
            {
                rows.Add(source[i]);
            }
            list.RefreshItems();
        }

        private void Move(int delta)
        {
            if(rows.Count == 0) return;
            int next = highlight < 0 ? (delta > 0 ? 0 : rows.Count - 1) : Mathf.Clamp(highlight + delta, 0, rows.Count - 1);
            SetHighlight(next, true);
        }

        private void SetHighlight(int index, bool scroll)
        {
            if(index >= rows.Count) index = rows.Count - 1;
            if(index != highlight)
            {
                int old = highlight;
                highlight = index;
                if(old >= 0 && old < rows.Count) list.RefreshItem(old);
                if(index >= 0) list.RefreshItem(index);
            }
            if(scroll && index >= 0) list.ScrollToItem(index);
            ShowDetails();
        }

        private void Hover(int index)
        {
            if(index != highlight && index >= 0 && index < rows.Count) SetHighlight(index, false);
        }

        private void Collapse()
        {
            var row = Highlighted;
            if(row == null || row.Kind == PickerRowKind.None) return;
            if(row.Kind == PickerRowKind.Heading && !closed.Contains(row.Path))
            {
                SetOpen(row.Path, false);
                return;
            }
            string parent = row.Kind == PickerRowKind.Heading ? ParentOf(row.Path) : row.Path;
            if(string.IsNullOrEmpty(parent)) return;
            int index = IndexOfHeading(parent);
            if(index >= 0) SetHighlight(index, true);
        }

        private void Expand()
        {
            var row = Highlighted;
            if(row != null && row.Kind == PickerRowKind.Heading && closed.Contains(row.Path)) SetOpen(row.Path, true);
        }

        private void SetOpen(string path, bool open)
        {
            bool changed = open ? closed.Remove(path) : closed.Add(path);
            if(!changed) return;
            SaveClosed();
            var keep = Highlighted;
            Fill(model.Rows(closed));
            highlight = -1;
            SetHighlight(Math.Max(IndexOf(keep), 0), true);
        }

        private void ShowDetails()
        {
            BlockPickerModel.Describe(Highlighted, out string title, out string text, out string example);
            CombatEditorStyles.SetText(paneTitle, title);
            CombatEditorStyles.SetText(paneText, text);
            CombatEditorStyles.SetText(paneExample, example);
        }

        private int IndexOf(PickerRow row)
        {
            if(row == null) return -1;
            for(int i = 0; i < rows.Count; i++)
            {
                var each = rows[i];
                if(each.Kind == row.Kind && each.Type == row.Type && each.Path == row.Path) return i;
            }
            return row.Kind == PickerRowKind.Block ? IndexOfType(row.Type) : -1;
        }

        private int IndexOfType(Type type)
        {
            if(type == null) return -1;
            for(int i = 0; i < rows.Count; i++)
            {
                if(rows[i].Kind == PickerRowKind.Block && rows[i].Type == type) return i;
            }
            return -1;
        }

        private int IndexOfHeading(string path)
        {
            for(int i = 0; i < rows.Count; i++)
            {
                if(rows[i].Kind == PickerRowKind.Heading && rows[i].Path == path) return i;
            }
            return -1;
        }

        private static string ParentOf(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : null;
        }

        private void LoadClosed()
        {
            closed.Clear();
            foreach(var path in StudioPrefs.GetString(model.Key).Split('\n'))
            {
                if(path.Length > 0) closed.Add(path);
            }
        }

        private void SaveClosed()
        {
            if(closed.Count == 0) StudioPrefs.Remove(model.Key);
            else StudioPrefs.Store(model.Key, string.Join("\n", closed));
        }

        private sealed class RowElement : VisualElement
        {
            private readonly BlockPickerWindow window;
            private readonly Image arrow;
            private readonly BlockIcon icon;
            private readonly Label label;
            private readonly Label folder;
            private int index = -1;

            public RowElement(BlockPickerWindow window)
            {
                this.window = window;
                AddToClassList("cg-type-picker__row");
                arrow = CombatEditorStyles.Icon("icon dropdown", "cg-type-picker__arrow");
                icon = new BlockIcon();
                icon.AddToClassList("cg-type-picker__icon");
                label = CombatEditorStyles.Text(null, "cg-type-picker__name");
                folder = CombatEditorStyles.Text(null, "cg-type-picker__folder");
                Add(arrow);
                Add(icon);
                Add(label);
                Add(folder);
                RegisterCallback<PointerMoveEvent>(_ => window.Hover(index));
                RegisterCallback<ClickEvent>(_ => window.Activate(index));
            }

            public void Bind(int row)
            {
                index = row;
                var data = window.rows[row];
                bool heading = data.Kind == PickerRowKind.Heading;
                bool open = heading && !window.closed.Contains(data.Path);
                style.paddingLeft = k_Inset + data.Depth * k_Indent;
                CombatEditorStyles.Show(arrow, heading);
                arrow.style.rotate = new Rotate(Angle.Degrees(open ? 0f : -90f));
                CombatEditorStyles.Show(icon, data.Kind == PickerRowKind.Block);
                if(data.Kind == PickerRowKind.Block)
                {
                    icon.SetCategory(BlockCategories.Of(data.Type));
                    icon.SetLook(Looks.OfBlock(data.Type));
                }
                label.text = heading && !open ? $"{data.Label} ({data.Count})" : data.Label;
                CombatEditorStyles.SetText(folder, window.searching ? data.Path : null);
                EnableInClassList("cg-type-picker__row--heading", heading);
                EnableInClassList("cg-type-picker__row--none", data.Kind == PickerRowKind.None);
                EnableInClassList("cg-type-picker__row--on", row == window.highlight);
            }
        }
    }
}
