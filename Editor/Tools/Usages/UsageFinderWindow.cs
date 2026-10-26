using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public sealed class UsageFinderWindow : EditorWindow
    {
        private enum Mode { References, BlockType, Unused }

        public const string k_Title = "Usage Finder";
        private const string k_Prefabs = "Usages.AllPrefabs";
        private const string k_Scenes = "Usages.Scenes";
        private const string k_Text = "Usages.Text";
        private const string k_Paths = "Usages.Paths";
        private const string k_Follow = "Usages.Follow";

        private static readonly Type[] blockBases =
        {
            typeof(IPayload), typeof(IEmission), typeof(IHitQuery), typeof(ICondition), typeof(IValueScaler),
            typeof(IFireGate), typeof(IFireMode), typeof(IDamageStep), typeof(IRegenRule), typeof(IStatusEffectBehavior)
        };

        [SerializeField] private Mode mode;
        [SerializeField] private Object target;
        [SerializeField] private string blockTypeName;

        private readonly List<Usage> usages = new();
        private readonly List<Object> unused = new();
        private ToggleButtonGroup modes;
        private VisualElement query;
        private ScrollView results;
        private Label status;
        private bool searched;

        private Type BlockType => string.IsNullOrEmpty(blockTypeName) ? null : Type.GetType(blockTypeName);

        [MenuItem(CombatTools.k_Menu + k_Title, false, 21)]
        public static void Open()
        {
            var window = GetWindow<UsageFinderWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
        }

        public static void Open(Object asset)
        {
            var window = GetWindow<UsageFinderWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            window.mode = Mode.References;
            window.target = asset;
            window.Rebuild();
            window.Find();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            StudioPrefs.Changed += Render;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            StudioPrefs.Changed -= Render;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatTools.Apply(root, "cg-usages");

            var toolbar = CombatTools.Toolbar(k_Title);
            modes = new ToggleButtonGroup { isMultipleSelection = false, allowEmptySelection = false };
            modes.AddToClassList("cg-toolbar__group");
            modes.Add(new Button { text = "References", tooltip = "Everything that points at an asset" });
            modes.Add(new Button { text = "Block Type", tooltip = "Every place a building block type is used" });
            modes.Add(new Button { text = "Unused", tooltip = "Definitions nothing points at" });
            modes.RegisterValueChangedCallback(evt =>
            {
                mode = (Mode)First(evt.newValue);
                searched = false;
                usages.Clear();
                unused.Clear();
                Rebuild();
            });
            toolbar.Add(modes);
            toolbar.Add(new ToolbarSpacer { flex = true });
            CombatTools.Finish(toolbar, ShowViewMenu);
            root.Add(toolbar);

            query = new VisualElement();
            query.AddToClassList("cg-usages__query");
            root.Add(query);

            results = new ScrollView(ScrollViewMode.Vertical);
            results.AddToClassList("cg-detail__scroll");
            results.contentContainer.AddToClassList("cg-detail__content");
            root.Add(results);
            status = CombatEditorStyles.Text(null, "cg-db-status");
            root.Add(status);
            Rebuild();
        }

        private void Rebuild()
        {
            if(query == null) return;
            modes.SetValueWithoutNotify(State(3, (int)mode));
            query.Clear();
            switch(mode)
            {
                case Mode.References:
                    var field = new ObjectField("Find usages of") { objectType = typeof(Object), allowSceneObjects = false, value = target };
                    field.AddToClassList("cg-usages__target");
                    field.RegisterValueChangedCallback(evt =>
                    {
                        target = evt.newValue;
                        Find();
                    });
                    query.Add(field);
                    query.Add(CombatTools.Small("Use Selection", () =>
                    {
                        target = Selection.activeObject;
                        Rebuild();
                        Find();
                    }));
                    var follow = new Toggle("Follow Selection") { value = StudioPrefs.Get(k_Follow, false) };
                    follow.AddToClassList("cg-usages__follow");
                    follow.RegisterValueChangedCallback(evt => StudioPrefs.Store(k_Follow, evt.newValue));
                    query.Add(follow);
                    break;
                case Mode.BlockType:
                    Button pick = null;
                    pick = new Button(() => AllBlocksPicker.Show(pick.worldBound, type =>
                    {
                        blockTypeName = type?.AssemblyQualifiedName;
                        Rebuild();
                        Find();
                    })) { text = BlockType != null ? $"{BlockCategories.Label(BlockCategories.Of(BlockType))} › {BlockTypes.DisplayName(BlockType)}" : "Pick a block type" };
                    pick.AddToClassList("cg-picker");
                    query.Add(CombatEditorStyles.Text("Find every", "cg-usages__label"));
                    query.Add(pick);
                    break;
                default:
                    query.Add(CombatEditorStyles.Text("Definitions and pipelines that no asset, prefab or scene points at.", "cg-usages__label"));
                    break;
            }
            query.Add(new VisualElement { style = { flexGrow = 1 } });
            var find = CombatTools.Small("Find", Find);
            find.AddToClassList("cg-wizard__primary");
            query.Add(find);
            Render();
        }

        private void OnSelectionChanged()
        {
            if(mode != Mode.References || !StudioPrefs.Get(k_Follow, false)) return;
            var selected = Selection.activeObject;
            if(selected == null || selected == target || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(selected))) return;
            target = selected;
            Rebuild();
            Find();
        }

        private void Find()
        {
            usages.Clear();
            unused.Clear();
            searched = true;
            switch(mode)
            {
                case Mode.References:
                    if(target == null) break;
                    usages.AddRange(UsageScanner.References(target, StudioPrefs.Get(k_Prefabs, true), StudioPrefs.Get(k_Scenes, true)));
                    if(StudioPrefs.Get(k_Text, true) && target is GameDefinition definition) usages.AddRange(UsageScanner.TextMentions(definition.Id));
                    break;
                case Mode.BlockType:
                    usages.AddRange(UsageScanner.BlockUsages(BlockType));
                    break;
                default:
                    unused.AddRange(UsageScanner.Unused(StudioPrefs.Get(k_Scenes, true)));
                    break;
            }
            Render();
        }

        private void Render()
        {
            if(results == null) return;
            results.Clear();
            if(mode == Mode.Unused)
            {
                RenderUnused();
                return;
            }

            if(!searched || (mode == Mode.References && target == null) || (mode == Mode.BlockType && BlockType == null))
            {
                status.text = string.Empty;
                Empty(mode == Mode.References ? "Pick an asset to find where it's used" : "Pick a block type to find where it's used", "Right click a definition in the Database or Studio and choose Find Usages");
                return;
            }

            var byAsset = usages.GroupBy(u => u.Asset).OrderBy(g => g.Key != null ? StudioPrefs.LabelOf(g.Key) : string.Empty).ToList();
            string subject = mode == Mode.References ? StudioPrefs.LabelOf(target) : BlockTypes.DisplayName(BlockType);
            status.text = usages.Count == 0 ? $"{subject} isn't used anywhere" : $"{subject} is used {Times(usages.Count)} in {Count(byAsset.Count, "asset")}";
            if(usages.Count == 0)
            {
                Empty("Not used anywhere", mode == Mode.References ? "Nothing in the project points at it. Code can still load it by name, path or ID" : "No combat asset or unit uses this block");
                return;
            }

            foreach(var group in byAsset)
            {
                var card = new VisualElement();
                card.AddToClassList("cg-card");
                card.AddToClassList("cg-usages__group");
                card.AddToClassList(DatabaseCatalog.StyleOf(group.Key));
                var header = new VisualElement();
                header.AddToClassList("cg-doctor__header");
                var icon = new BlockIcon();
                icon.SetStyle(DatabaseCatalog.StyleOf(group.Key));
                header.Add(icon);
                header.Add(CombatEditorStyles.Text(group.Key != null ? StudioPrefs.LabelOf(group.Key) : "Unknown", "cg-doctor__title"));
                header.Add(CombatEditorStyles.Chip(Times(group.Count())));
                var asset = group.Key;
                header.Add(CombatTools.Small("Open", () => CombatTools.Jump(asset)));
                header.Add(CombatTools.Small("Ping", () => EditorGUIUtility.PingObject(asset)));
                card.Add(header);
                if(asset != null) card.Add(CombatEditorStyles.Text(AssetDatabase.GetAssetPath(asset), "cg-card__summary"));
                if(StudioPrefs.Get(k_Paths, true))
                {
                    foreach(var usage in group)
                    {
                        card.Add(UsageRow(usage));
                    }
                }
                results.Add(card);
            }
        }

        private VisualElement UsageRow(Usage usage)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-doctor__row");
            row.Add(CombatEditorStyles.Text(usage.Where, "cg-doctor__message"));
            var asset = usage.Asset;
            string path = usage.Path;
            int line = usage.Line;
            row.Add(CombatTools.Small("Go", () =>
            {
                if(line > 0) AssetDatabase.OpenAsset(asset, line);
                else CombatTools.Jump(asset, path);
            }));
            return row;
        }

        private void RenderUnused()
        {
            if(!searched)
            {
                status.text = string.Empty;
                Empty("Find unused definitions", "Press Find to scan every asset, prefab and scene");
                return;
            }
            status.text = unused.Count == 0 ? "Everything is used somewhere" : $"{Count(unused.Count, "definition")} nothing points at";
            if(unused.Count == 0)
            {
                Empty("Everything is used", "Every definition and pipeline is referenced by something");
                return;
            }

            results.Add(CombatEditorStyles.Text("Assets loaded from code by name, path, ID, Resources or Addressables still count as unused here. Check before deleting.", "cg-note"));
            var actions = new VisualElement();
            actions.AddToClassList("cg-chips");
            actions.Add(CombatTools.Small("Select All in Project", () => Selection.objects = unused.ToArray()));
            results.Add(actions);

            foreach(var group in unused.GroupBy(DatabaseCatalog.StyleOf).OrderBy(g => g.Key))
            {
                var card = new VisualElement();
                card.AddToClassList("cg-card");
                card.AddToClassList(group.Key);
                var first = group.First();
                card.Add(CombatEditorStyles.Text(ObjectNames.NicifyVariableName(first.GetType().Name).ToUpperInvariant(), "cg-card__chip"));
                foreach(var asset in group.OrderBy(StudioPrefs.LabelOf))
                {
                    var row = new VisualElement();
                    row.AddToClassList("cg-doctor__row");
                    var icon = new BlockIcon();
                    icon.SetStyle(group.Key);
                    icon.SetTint(DatabaseCatalog.ColorOf(asset));
                    row.Add(icon);
                    row.Add(CombatEditorStyles.Text(StudioPrefs.LabelOf(asset), "cg-doctor__message"));
                    row.Add(CombatEditorStyles.Text(AssetDatabase.GetAssetPath(asset), "cg-doctor__where"));
                    var each = asset;
                    row.Add(CombatTools.Small("Ping", () => EditorGUIUtility.PingObject(each)));
                    row.Add(CombatTools.Small("Open", () => CombatTools.Jump(each)));
                    card.Add(row);
                }
                results.Add(card);
            }
        }

        private void Empty(string title, string text)
        {
            var empty = new VisualElement();
            empty.AddToClassList("cg-outline-empty");
            empty.Add(CombatEditorStyles.Text(title, "cg-outline-empty__title"));
            empty.Add(CombatEditorStyles.Text(text, "cg-outline-empty__text"));
            results.Add(empty);
        }

        private static string Times(int count) => count == 1 ? "once" : $"{count} times";
        private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Search In"));
            StudioPrefs.Toggle(menu, "All Prefabs", k_Prefabs, true);
            StudioPrefs.Toggle(menu, "Scenes", k_Scenes, true);
            StudioPrefs.Toggle(menu, "Text and Code (by ID)", k_Text, true);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Show Each Place", k_Paths, true);
            StudioPrefs.Labels(menu);
            menu.DropDown(rect);
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

        private sealed class AllBlocksPicker : AdvancedDropdown
        {
            private readonly Action<Type> picked;
            private int nextId;

            private AllBlocksPicker(Action<Type> picked) : base(new AdvancedDropdownState())
            {
                this.picked = picked;
                minimumSize = new Vector2(280f, 360f);
            }

            public static void Show(Rect rect, Action<Type> picked) => new AllBlocksPicker(picked).Show(rect);

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Block Types");
                foreach(var baseType in blockBases)
                {
                    var folder = new AdvancedDropdownItem(BlockCategories.Label(BlockCategories.Of(baseType))) { id = ++nextId };
                    foreach(var type in BlockTypes.Candidates(baseType))
                    {
                        folder.AddChild(new TypeItem(BlockTypes.MenuPath(type).Replace('/', ' '), type, ++nextId));
                    }
                    if(folder.children.Any()) root.AddChild(folder);
                }
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if(item is TypeItem typed) picked?.Invoke(typed.Type);
            }

            private sealed class TypeItem : AdvancedDropdownItem
            {
                public readonly Type Type;

                public TypeItem(string name, Type type, int id) : base(name)
                {
                    Type = type;
                    this.id = id;
                }
            }
        }
    }
}
