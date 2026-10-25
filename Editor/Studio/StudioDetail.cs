using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class StudioDetail : VisualElement
    {
        private const float k_RecipeIndent = 14f;
        private const string k_About = "About";
        private const string k_Examples = "Examples";
        private const string k_Recipe = "Recipe";
        private const string k_Details = "Details";
        private const string k_Grips = "Grips";

        private readonly CombatStudioWindow window;
        private readonly ScrollView scroll;
        private SerializedObject serializedObject;
        private StudioOutline outline;
        private OutlineNode node;
        private Label headerTitle;
        private Label headerSummary;

        public StudioDetail(CombatStudioWindow window)
        {
            this.window = window;
            AddToClassList("cg-detail");
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cg-detail__scroll");
            scroll.contentContainer.AddToClassList("cg-detail__content");
            Add(scroll);
        }

        public void Show(SerializedObject serializedObject, StudioOutline outline, OutlineNode node)
        {
            this.serializedObject = serializedObject;
            this.outline = outline;
            this.node = node;
            headerTitle = null;
            headerSummary = null;
            scroll.Clear();

            if(serializedObject == null || node == null)
            {
                scroll.Add(CombatEditorStyles.Text("Pick an asset on the left, or create one with New", "cg-placeholder"));
                return;
            }

            scroll.Add(Breadcrumb(node));
            switch(node.Kind)
            {
                case OutlineKind.Root:
                    ShowRoot();
                    break;
                case OutlineKind.Section:
                    ShowSection(serializedObject.FindProperty(node.Path));
                    break;
                default:
                    ShowBlock(serializedObject.FindProperty(node.Path));
                    break;
            }
            scroll.contentContainer.Bind(serializedObject);
        }

        public static void Options(GenericMenu menu)
        {
            StudioPrefs.Toggle(menu, "About Box", k_About, true);
            StudioPrefs.Toggle(menu, "Examples", k_Examples, true);
            StudioPrefs.Toggle(menu, "Asset Details", k_Details, true);
            StudioPrefs.Toggle(menu, "Recipe", k_Recipe, true);
            StudioPrefs.Toggle(menu, "Drag Handles", k_Grips, true);
        }

        public void RefreshHeader()
        {
            if(serializedObject == null || node == null) return;
            if(headerTitle != null) headerTitle.text = StudioOutline.TitleOf(node, serializedObject);
            if(headerSummary != null) CombatEditorStyles.SetText(headerSummary, StudioOutline.SummaryOf(node, serializedObject));
        }

        private void ShowRoot()
        {
            var asset = (ScriptableObject)serializedObject.targetObject;
            var kind = StudioKinds.All[Mathf.Max(0, StudioKinds.IndexOf(asset.GetType()))];
            VisualElement actions = null;
            if(asset is DamagePipeline pipeline)
            {
                actions = new VisualElement();
                actions.AddToClassList("cg-card__actions");
                var test = new Button(() => PipelineTesterWindow.Open(pipeline)) { text = "Test", tooltip = "Open this pipeline in the Pipeline Tester" };
                test.AddToClassList("cg-card__button");
                actions.Add(test);
            }
            var card = Card(StudioOutline.TitleOf(node, serializedObject), AssetDatabase.GetAssetPath(asset), kind.Singular, kind.Category, actions);
            int empty = StudioIndex.Of(asset).EmptySlots;
            if(empty > 0) card.Add(CombatEditorStyles.Text(empty == 1 ? "1 empty slot" : $"{empty} empty slots", "cg-card__warning"));
            scroll.Add(card);

            var details = new List<SerializedProperty>();
            foreach(var property in BlockTypes.Children(serializedObject))
            {
                if(property.propertyType == SerializedPropertyType.ManagedReference || BlockTypes.IsBlockArray(property) || BlockTypes.HoldsBlocks(property)) continue;
                details.Add(property);
            }
            if(details.Count > 0 && StudioPrefs.Get(k_Details, true))
            {
                scroll.Add(CombatEditorStyles.Text("Details", "cg-section-title"));
                foreach(var property in details)
                {
                    scroll.Add(new PropertyField(property));
                }
            }

            if(!StudioPrefs.Get(k_Recipe, true)) return;
            scroll.Add(CombatEditorStyles.Text("Recipe", "cg-section-title"));
            var recipe = new VisualElement();
            recipe.AddToClassList("cg-recipe");
            foreach(var each in outline.Ordered)
            {
                if(each.Kind != OutlineKind.Root) recipe.Add(RecipeLine(each, recipe));
            }
            if(recipe.childCount == 0) recipe.Add(CombatEditorStyles.Text("Nothing here yet", "cg-empty-note"));
            scroll.Add(recipe);
        }

        private void ShowSection(SerializedProperty array)
        {
            if(array == null) return;
            var category = BlockCategories.Of(BlockTypes.SlotType(array));
            scroll.Add(Card(node.Label, StudioOutline.SummaryOf(node, serializedObject), BlockCategories.Label(category), category, null));
            scroll.Add(SlotList(array, null));
        }

        private void ShowBlock(SerializedProperty property)
        {
            if(property == null) return;
            string path = property.propertyPath;
            var type = BlockTypes.ValueType(property);
            var category = BlockCategories.Of(type ?? BlockTypes.SlotType(property));

            var actions = new VisualElement();
            actions.AddToClassList("cg-card__actions");
            Button change = null;
            change = new Button(() => Pick(change, path)) { text = type == null ? "Choose Type" : "Change Type" };
            change.AddToClassList("cg-card__button");
            actions.Add(change);
            actions.Add(MenuButton(path, change));

            string chip = string.IsNullOrEmpty(node.Label) ? BlockCategories.Label(category) : $"{BlockCategories.Label(category)} · {node.Label}";
            scroll.Add(Card(StudioOutline.TitleOf(node, serializedObject), StudioOutline.SummaryOf(node, serializedObject), chip, category, actions));

            if(type == null)
            {
                scroll.Add(CombatEditorStyles.Text("This slot is empty. Choose a type to fill it.", "cg-empty-note"));
                return;
            }

            var info = BlockTypes.InfoOf(type);
            if(info != null && StudioPrefs.Get(k_About, true)) scroll.Add(About(info));

            var fields = new VisualElement();
            fields.AddToClassList("cg-fields");
            Fields(property, fields);
            scroll.Add(fields);
        }

        private static VisualElement About(BlockInfoAttribute info)
        {
            var about = new VisualElement();
            about.AddToClassList("cg-info");
            about.Add(CombatEditorStyles.Text(info.Description, "cg-info__text"));
            if(!string.IsNullOrEmpty(info.Example) && StudioPrefs.Get(k_Examples, true))
            {
                about.Add(CombatEditorStyles.Text("EXAMPLE", "cg-info__example-title"));
                about.Add(CombatEditorStyles.Text(info.Example, "cg-info__example"));
            }
            return about;
        }

        private void Fields(SerializedProperty parent, VisualElement into)
        {
            foreach(var child in BlockTypes.Children(parent))
            {
                if(child.propertyType == SerializedPropertyType.ManagedReference) into.Add(Slot(child, child.displayName, null));
                else if(BlockTypes.IsBlockArray(child)) into.Add(SlotList(child, child.displayName));
                else if(!child.isArray && BlockTypes.HoldsBlocks(child)) into.Add(Group(child));
                else into.Add(new PropertyField(child));
            }
        }

        private VisualElement Group(SerializedProperty property)
        {
            var group = new VisualElement();
            group.AddToClassList("cg-group");
            group.Add(CombatEditorStyles.Text(property.displayName, "cg-group__title"));
            Fields(property, group);
            return group;
        }

        private VisualElement SlotList(SerializedProperty array, string label)
        {
            string path = array.propertyPath;
            var list = new VisualElement();
            list.AddToClassList("cg-slot-list");

            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text(label ?? string.Empty, "cg-slot-list__title"));
            Button add = null;
            add = new Button(() => BlockTypePicker.Show(add.worldBound, BlockTypes.SlotType(serializedObject.FindProperty(path)), false,
                type => window.AfterEdit(BlockEdits.Add(serializedObject, path, type)))) { text = "+ Add" };
            add.AddToClassList("cg-add-button");
            header.Add(add);
            list.Add(header);

            if(array.arraySize == 0) list.Add(CombatEditorStyles.Text("Empty", "cg-empty-note"));
            for(int i = 0; i < array.arraySize; i++)
            {
                list.Add(Slot(array.GetArrayElementAtIndex(i), null, list));
            }
            return list;
        }

        private VisualElement Slot(SerializedProperty property, string label, VisualElement list)
        {
            string path = property.propertyPath;
            var type = BlockTypes.ValueType(property);
            var category = BlockCategories.Of(type ?? BlockTypes.SlotType(property));

            var row = new VisualElement { userData = path };
            row.AddToClassList("cg-slot");
            BlockCategories.Apply(row, category);
            row.EnableInClassList("cg-slot--empty", type == null);
            row.tooltip = BlockTypes.TooltipOf(type);

            if(list != null)
            {
                if(StudioPrefs.Get(k_Grips, true) && StudioPrefs.DragToReorder) row.Add(new SlotGrip());
                row.AddManipulator(new SlotDragger(PathOf, () => list.Children(), Move));
            }

            var icon = new BlockIcon();
            icon.SetCategory(category);
            row.Add(icon);
            if(!string.IsNullOrEmpty(label)) row.Add(CombatEditorStyles.Text(label, "cg-slot__label"));
            row.Add(CombatEditorStyles.Text(type == null ? "Choose a type" : BlockTypes.DisplayName(type), "cg-slot__title"));
            row.Add(CombatEditorStyles.Text(BlockSummary.Of(property), "cg-slot__summary"));

            var menu = MenuButton(path, row);
            row.Add(menu);

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if(evt.target is VisualElement target && menu.Contains(target)) return;
                if(type == null) Pick(row, path);
                else window.Select(path);
            });
            row.AddManipulator(new ContextualMenuManipulator(evt => BlockMenu.Append(evt.menu, Actions(path, row))));
            return row;
        }

        private static string PathOf(VisualElement row)
        {
            switch(row.userData)
            {
                case OutlineNode each: return each.Path;
                case string path: return path;
                default: return null;
            }
        }

        private void Move(string path, int to)
        {
            BlockEdits.MoveTo(serializedObject, path, to);
            window.AfterEdit(null);
        }

        private VisualElement RecipeLine(OutlineNode each, VisualElement recipe)
        {
            var category = StudioOutline.CategoryOf(each, serializedObject);
            var line = new VisualElement { userData = each };
            line.AddToClassList("cg-recipe-line");
            line.EnableInClassList("cg-recipe-line--section", each.Kind == OutlineKind.Section);
            line.style.paddingLeft = (each.Depth - 1) * k_RecipeIndent;
            BlockCategories.Apply(line, category);
            if(each.Kind == OutlineKind.Block) line.tooltip = BlockTypes.TooltipOf(BlockTypes.ValueType(serializedObject.FindProperty(each.Path)));

            var icon = new BlockIcon();
            icon.SetCategory(category);
            line.Add(icon);
            if(each.Kind == OutlineKind.Block && !string.IsNullOrEmpty(each.Label)) line.Add(CombatEditorStyles.Text(each.Label, "cg-recipe-line__label"));
            line.Add(CombatEditorStyles.Text(StudioOutline.TitleOf(each, serializedObject), "cg-recipe-line__title"));
            line.Add(CombatEditorStyles.Text(StudioOutline.SummaryOf(each, serializedObject), "cg-recipe-line__summary"));
            line.RegisterCallback<ClickEvent>(_ => window.Select(each.Path));
            if(each.Kind == OutlineKind.Block) line.AddManipulator(new SlotDragger(PathOf, () => recipe.Children(), Move));
            return line;
        }

        private VisualElement Breadcrumb(OutlineNode current)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-breadcrumb");
            var chain = new List<OutlineNode>();
            for(var each = current; each != null; each = outline.Parent(each))
            {
                chain.Insert(0, each);
            }
            for(int i = 0; i < chain.Count; i++)
            {
                if(i > 0) row.Add(CombatEditorStyles.Text("/", "cg-crumb__separator"));
                var target = chain[i];
                var crumb = new Button(() => window.Select(target.Path)) { text = StudioOutline.TitleOf(target, serializedObject) };
                crumb.AddToClassList("cg-crumb");
                crumb.EnableInClassList("cg-crumb--current", target == current);
                row.Add(crumb);
            }
            return row;
        }

        private VisualElement Card(string title, string summary, string chip, BlockCategory category, VisualElement actions)
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            BlockCategories.Apply(card, category);

            card.Add(CombatEditorStyles.Text(chip, "cg-card__chip"));

            var titleRow = new VisualElement();
            titleRow.AddToClassList("cg-card__title-row");
            var icon = new BlockIcon();
            icon.AddToClassList("cg-card__icon");
            icon.SetCategory(category);
            titleRow.Add(icon);
            headerTitle = CombatEditorStyles.Text(title, "cg-card__title");
            titleRow.Add(headerTitle);
            if(actions != null) titleRow.Add(actions);
            card.Add(titleRow);

            headerSummary = CombatEditorStyles.Text(null, "cg-card__summary");
            CombatEditorStyles.SetText(headerSummary, summary);
            card.Add(headerSummary);
            return card;
        }

        private Button MenuButton(string path, VisualElement anchor)
        {
            Button button = null;
            button = new Button(() => BlockMenu.Show(button.worldBound, Actions(path, anchor)));
            button.AddToClassList("cg-menu-button");
            button.Add(CombatEditorStyles.Icon("_Menu", "cg-menu-button__icon"));
            return button;
        }

        private List<BlockAction> Actions(string path, VisualElement anchor)
        {
            var actions = BlockMenu.For(serializedObject, path, window.AfterEdit);
            actions.Insert(0, BlockAction.Separator);
            actions.Insert(0, new BlockAction("Change Type...", true, () => Pick(anchor, path)));
            return actions;
        }

        private void Pick(VisualElement anchor, string path)
        {
            var property = serializedObject.FindProperty(path);
            if(property == null) return;
            var current = BlockTypes.ValueType(property);
            BlockTypePicker.Show(anchor.worldBound, BlockTypes.SlotType(property), true, type =>
            {
                if(type != current) window.AfterEdit(BlockEdits.SetType(serializedObject, path, type));
            });
        }
    }
}
