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
    public sealed class FactionMatrixWindow : EditorWindow
    {
        public const string k_Title = "Faction Matrix";
        private const string k_BothSides = "Factions.BothSides";
        private const string k_Counts = "Factions.Counts";
        private const string k_Declared = "Factions.Declared";
        private const string k_Compact = "Factions.Compact";

        private readonly List<Faction> factions = new();
        private ScrollView grid;
        private Label status;

        [MenuItem(CombatTools.k_Menu + k_Title, false, 25)]
        public static void Open()
        {
            var window = GetWindow<FactionMatrixWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
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
            CombatTools.Apply(root, "cg-factions");
            var toolbar = CombatTools.Toolbar(k_Title);
            toolbar.Add(new ToolbarButton(CreateFaction) { text = "New Faction" });
            toolbar.Add(new ToolbarButton(MakeAllSymmetric) { text = "Make Symmetric", tooltip = "Write every relation on both factions so each asset tells the whole story" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            CombatTools.Finish(toolbar, ShowViewMenu);
            root.Add(toolbar);

            var legend = new VisualElement();
            legend.AddToClassList("cg-factions__legend");
            legend.Add(Swatch("Hostile", "cg-relation--hostile"));
            legend.Add(Swatch("Friendly", "cg-relation--friendly"));
            legend.Add(Swatch("Neutral", "cg-relation--neutral"));
            legend.Add(CombatEditorStyles.Text("Click a cell to cycle Neutral → Hostile → Friendly. Right click for more. Rows and columns read the same way: hostile wins if either side lists the other.", "cg-note"));
            root.Add(legend);

            grid = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            grid.AddToClassList("cg-factions__grid");
            root.Add(grid);
            status = CombatEditorStyles.Text(null, "cg-db-status");
            root.Add(status);
            Rebuild();
        }

        private void OnAssetsChanged(IReadOnlyCollection<string> guids) => Rebuild();

        private void Rebuild()
        {
            if(grid == null) return;
            factions.Clear();
            foreach(var guid in AssetDatabase.FindAssets("t:Faction"))
            {
                var faction = AssetDatabase.LoadAssetAtPath<Faction>(AssetDatabase.GUIDToAssetPath(guid));
                if(faction != null) factions.Add(faction);
            }
            factions.Sort((a, b) => string.Compare(StudioPrefs.LabelOf(a), StudioPrefs.LabelOf(b), StringComparison.OrdinalIgnoreCase));

            grid.Clear();
            grid.EnableInClassList("cg-factions__grid--compact", StudioPrefs.Get(k_Compact, false));
            if(factions.Count == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList("cg-outline-empty");
                empty.Add(CombatEditorStyles.Text("No factions yet", "cg-outline-empty__title"));
                empty.Add(CombatEditorStyles.Text("Want to make one?", "cg-outline-empty__text"));
                var create = new Button(CreateFaction) { text = "Create Faction" };
                create.AddToClassList("cg-outline-empty__create");
                empty.Add(create);
                grid.Add(empty);
                status.text = string.Empty;
                return;
            }

            var counts = StudioPrefs.Get(k_Counts, true) ? UnitCounts() : null;
            var header = new VisualElement();
            header.AddToClassList("cg-factions__row");
            header.Add(Corner());
            foreach(var column in factions)
            {
                header.Add(Header(column, counts, true));
            }
            grid.Add(header);

            foreach(var row in factions)
            {
                var line = new VisualElement();
                line.AddToClassList("cg-factions__row");
                line.Add(Header(row, counts, false));
                foreach(var column in factions)
                {
                    line.Add(Cell(row, column));
                }
                grid.Add(line);
            }

            int hostile = 0;
            int friendly = 0;
            for(int i = 0; i < factions.Count; i++)
            {
                for(int j = i + 1; j < factions.Count; j++)
                {
                    var relation = factions[i].RelationTo(factions[j]);
                    if(relation == FactionRelation.Hostile) hostile++;
                    else if(relation == FactionRelation.Friendly) friendly++;
                }
            }
            status.text = $"{factions.Count} factions · {hostile} hostile pairs · {friendly} friendly pairs";
        }

        private static VisualElement Corner()
        {
            var corner = new VisualElement();
            corner.AddToClassList("cg-factions__corner");
            corner.Add(CombatEditorStyles.Text("row → column", "cg-factions__corner-text"));
            return corner;
        }

        private VisualElement Header(Faction faction, Dictionary<Faction, int> counts, bool column)
        {
            var header = new VisualElement();
            header.AddToClassList(column ? "cg-factions__column" : "cg-factions__row-header");
            header.style.borderLeftColor = faction.Color;
            header.style.borderTopColor = faction.Color;
            var name = CombatEditorStyles.Text(StudioPrefs.LabelOf(faction), "cg-factions__name");
            header.Add(name);
            if(counts != null)
            {
                counts.TryGetValue(faction, out int units);
                header.Add(CombatEditorStyles.Text(units == 1 ? "1 unit" : $"{units} units", "cg-factions__count"));
            }
            header.tooltip = $"{StudioKinds.NameOf(faction)}\n{AssetDatabase.GetAssetPath(faction)}";
            header.RegisterCallback<ClickEvent>(evt =>
            {
                Selection.activeObject = faction;
                if(evt.clickCount == 2) EditorGUIUtility.PingObject(faction);
            });
            header.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Select", _ => Selection.activeObject = faction);
                evt.menu.AppendAction("Ping", _ => EditorGUIUtility.PingObject(faction));
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Hostile to Everyone", _ => SetAll(faction, FactionRelation.Hostile));
                evt.menu.AppendAction("Friendly to Everyone", _ => SetAll(faction, FactionRelation.Friendly));
                evt.menu.AppendAction("Neutral to Everyone", _ => SetAll(faction, FactionRelation.Neutral));
                StudioWindows.AppendAssetActions(evt.menu, faction);
            }));
            return header;
        }

        private VisualElement Cell(Faction row, Faction column)
        {
            var cell = new Button();
            cell.AddToClassList("cg-factions__cell");
            if(row == column)
            {
                cell.text = "self";
                cell.AddToClassList("cg-relation--self");
                cell.SetEnabled(false);
                return cell;
            }

            var relation = row.RelationTo(column);
            cell.text = relation.ToString();
            cell.AddToClassList("cg-relation--" + relation.ToString().ToLowerInvariant());
            string declared = Declared(row, column);
            if(StudioPrefs.Get(k_Declared, true) && declared != null) cell.Add(CombatEditorStyles.Text(declared, "cg-factions__declared"));
            cell.tooltip = Explain(row, column, relation);
            cell.clicked += () => Set(row, column, Next(relation));
            cell.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Hostile", _ => Set(row, column, FactionRelation.Hostile), relation == FactionRelation.Hostile ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Friendly", _ => Set(row, column, FactionRelation.Friendly), relation == FactionRelation.Friendly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Neutral", _ => Set(row, column, FactionRelation.Neutral), relation == FactionRelation.Neutral ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Make Symmetric", _ => MakeSymmetric(row, column));
            }));
            return cell;
        }

        private static FactionRelation Next(FactionRelation relation)
        {
            switch(relation)
            {
                case FactionRelation.Neutral: return FactionRelation.Hostile;
                case FactionRelation.Hostile: return FactionRelation.Friendly;
                default: return FactionRelation.Neutral;
            }
        }

        private static bool Lists(Faction[] list, Faction faction) => list != null && list.Contains(faction);

        private static string Declared(Faction a, Faction b)
        {
            bool aHostile = Lists(a.HostileTo, b);
            bool bHostile = Lists(b.HostileTo, a);
            bool aFriendly = Lists(a.FriendlyTo, b);
            bool bFriendly = Lists(b.FriendlyTo, a);
            if((aHostile && bHostile) || (aFriendly && bFriendly && !aHostile && !bHostile)) return null;
            if(aHostile || aFriendly) return "this row lists it";
            if(bHostile || bFriendly) return "only the column lists it";
            return null;
        }

        private static string Explain(Faction a, Faction b, FactionRelation relation)
        {
            var reasons = new List<string>();
            if(Lists(a.HostileTo, b)) reasons.Add($"{StudioKinds.NameOf(a)} lists {StudioKinds.NameOf(b)} as hostile");
            if(Lists(b.HostileTo, a)) reasons.Add($"{StudioKinds.NameOf(b)} lists {StudioKinds.NameOf(a)} as hostile");
            if(Lists(a.FriendlyTo, b)) reasons.Add($"{StudioKinds.NameOf(a)} lists {StudioKinds.NameOf(b)} as friendly");
            if(Lists(b.FriendlyTo, a)) reasons.Add($"{StudioKinds.NameOf(b)} lists {StudioKinds.NameOf(a)} as friendly");
            if(reasons.Count == 0) reasons.Add("Neither lists the other");
            return $"{StudioKinds.NameOf(a)} → {StudioKinds.NameOf(b)}: {relation}\n" + string.Join("\n", reasons);
        }

        private void Set(Faction a, Faction b, FactionRelation relation)
        {
            Undo.RecordObjects(new Object[] { a, b }, "Set Faction Relation");
            Strip(a, b);
            Strip(b, a);
            bool both = StudioPrefs.Get(k_BothSides, true);
            if(relation == FactionRelation.Hostile)
            {
                a.HostileTo = Append(a.HostileTo, b);
                if(both) b.HostileTo = Append(b.HostileTo, a);
            }
            else if(relation == FactionRelation.Friendly)
            {
                a.FriendlyTo = Append(a.FriendlyTo, b);
                if(both) b.FriendlyTo = Append(b.FriendlyTo, a);
            }
            EditorUtility.SetDirty(a);
            EditorUtility.SetDirty(b);
            Rebuild();
        }

        private void SetAll(Faction faction, FactionRelation relation)
        {
            foreach(var other in factions)
            {
                if(other != faction) Set(faction, other, relation);
            }
        }

        private void MakeSymmetric(Faction a, Faction b)
        {
            var relation = a.RelationTo(b);
            Set(a, b, relation == FactionRelation.Unknown ? FactionRelation.Neutral : relation);
        }

        private void MakeAllSymmetric()
        {
            bool both = StudioPrefs.Get(k_BothSides, true);
            StudioPrefs.Store(k_BothSides, true);
            for(int i = 0; i < factions.Count; i++)
            {
                for(int j = i + 1; j < factions.Count; j++)
                {
                    MakeSymmetric(factions[i], factions[j]);
                }
            }
            StudioPrefs.Store(k_BothSides, both);
            Rebuild();
        }

        private static void Strip(Faction faction, Faction other)
        {
            faction.HostileTo = (faction.HostileTo ?? Array.Empty<Faction>()).Where(f => f != other).ToArray();
            faction.FriendlyTo = (faction.FriendlyTo ?? Array.Empty<Faction>()).Where(f => f != other).ToArray();
        }

        private static Faction[] Append(Faction[] list, Faction faction)
        {
            var result = (list ?? Array.Empty<Faction>()).Where(f => f != null).ToList();
            if(!result.Contains(faction)) result.Add(faction);
            return result.ToArray();
        }

        private static Dictionary<Faction, int> UnitCounts()
        {
            var counts = new Dictionary<Faction, int>();
            foreach(var unit in UnitLibrary.All)
            {
                var faction = UnitLibrary.FactionOf(unit);
                if(faction == null) continue;
                counts.TryGetValue(faction, out int count);
                counts[faction] = count + 1;
            }
            return counts;
        }

        private static VisualElement Swatch(string text, string className)
        {
            var swatch = new Label(text);
            swatch.AddToClassList("cg-factions__swatch");
            swatch.AddToClassList(className);
            return swatch;
        }

        private void CreateFaction()
        {
            string folder = factions.Count > 0 ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(factions[0]))?.Replace('\\', '/') : "Assets";
            string path = EditorUtility.SaveFilePanelInProject("New Faction", "Faction", "asset", "Choose where to save the new faction", folder);
            if(string.IsNullOrEmpty(path)) return;
            var faction = CreateInstance<Faction>();
            AssetDatabase.CreateAsset(faction, path);
            DefinitionAssets.ResetId(faction, path);
            AssetDatabase.SaveAssets();
            Rebuild();
        }

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            StudioPrefs.Toggle(menu, "Edit Both Factions", k_BothSides, true);
            StudioPrefs.Toggle(menu, "Unit Counts", k_Counts, true);
            StudioPrefs.Toggle(menu, "Show One-Sided Relations", k_Declared, true);
            StudioPrefs.Toggle(menu, "Compact", k_Compact, false);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Labels(menu);
            menu.DropDown(rect);
        }
    }
}
