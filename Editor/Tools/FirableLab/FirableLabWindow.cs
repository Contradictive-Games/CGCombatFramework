using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class FirableLabWindow : EditorWindow
    {
        private enum View { Lab, Compare }

        public const string k_Title = "Firable Lab";
        private const string k_Settings = "settings";
        private const string k_Chart = "FirableLab.Chart";
        private const string k_Breakdown = "FirableLab.Breakdown";
        private const string k_Effects = "FirableLab.Effects";
        private const string k_Raw = "FirableLab.Raw";
        private const string k_Decimals = "FirableLab.Decimals";
        private const string k_Live = "FirableLab.Live";

        [SerializeField] private FireLabSettings settings = new();
        [SerializeField] private View view;

        private readonly List<FireReport> compared = new();
        private FirableSimulator simulator;
        private SerializedObject serializedWindow;
        private ToggleButtonGroup views;
        private ScrollView results;
        private VisualElement compare;
        private MultiColumnListView table;
        private RunBar runBar;
        private bool queued;
        private FirableDefinition remembered;

        private static int Decimals => StudioPrefs.GetInt(k_Decimals, 1);

        [MenuItem(CombatTools.k_Menu + k_Title, false, 23)]
        public static void Open() => OpenWith(RecentAssets.Latest<FirableDefinition>(), false);

        public static void Open(FirableDefinition firable) => EditorApplication.delayCall += () => OpenWith(firable, true);

        private static void OpenWith(FirableDefinition firable, bool requested)
        {
            var window = GetWindow<FirableLabWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            if(firable == null || !requested && firable == window.settings.Firable) return;
            Undo.RecordObject(window, "Open in Firable Lab");
            window.settings.Firable = firable;
            if(requested) window.view = View.Lab;
            window.serializedWindow?.Update();
            window.ApplyView();
            window.Run();
        }

        private void OnEnable()
        {
            simulator = new FirableSimulator();
            StudioPrefs.Changed += Queue;
            AssetChanges.Changed += OnAssetsChanged;
            Undo.undoRedoPerformed += Queue;
        }

        private void OnDisable()
        {
            StudioPrefs.Changed -= Queue;
            AssetChanges.Changed -= OnAssetsChanged;
            Undo.undoRedoPerformed -= Queue;
            simulator?.Dispose();
            simulator = null;
            serializedWindow?.Dispose();
            serializedWindow = null;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatTools.Apply(root, "cg-firable-lab");
            serializedWindow = new SerializedObject(this);

            var toolbar = CombatTools.Toolbar(k_Title);
            views = new ToggleButtonGroup { isMultipleSelection = false, allowEmptySelection = false };
            views.AddToClassList("cg-toolbar__group");
            views.Add(new Button { text = "Lab", tooltip = "One firable in depth" });
            views.Add(new Button { text = "Compare", tooltip = "Every firable side by side against the same defender" });
            views.RegisterValueChangedCallback(evt =>
            {
                view = (View)First(evt.newValue);
                ApplyView();
                Run();
            });
            toolbar.Add(views);
            toolbar.Add(new ToolbarSpacer { flex = true });
            CombatTools.Finish(toolbar, ShowViewMenu);
            root.Add(toolbar);

            var split = new TwoPaneSplitView(0, 320f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-firable-lab-split" };
            split.AddToClassList("cg-studio__split");
            split.Add(BuildSetup());

            var right = new VisualElement();
            right.AddToClassList("cg-pane");
            runBar = new RunBar("Simulate Again", "Fire it again with the setup on the left", Run, k_Live);
            runBar.AddButton("New Seed", "Simulate again with different random rolls", () =>
            {
                Undo.RecordObject(this, "New Seed");
                settings.Seed = UnityEngine.Random.Range(1, 100000);
                serializedWindow.Update();
                Run();
            });
            right.Add(runBar);
            results = new ScrollView(ScrollViewMode.Vertical);
            results.AddToClassList("cg-detail__scroll");
            results.contentContainer.AddToClassList("cg-detail__content");
            right.Add(results);
            compare = BuildCompare();
            right.Add(compare);
            split.Add(right);
            root.Add(split);

            root.Bind(serializedWindow);
            var watcher = new VisualElement();
            watcher.AddToClassList("cg-watcher");
            root.Add(watcher);
            watcher.TrackSerializedObjectValue(serializedWindow, _ => Queue());
            remembered = settings.Firable;
            ApplyView();
            Run();
        }

        private VisualElement BuildSetup()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            pane.Add(CombatEditorStyles.Header("Setup"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cg-detail__scroll");
            scroll.contentContainer.AddToClassList("cg-setup__content");
            pane.Add(scroll);

            scroll.Add(CombatEditorStyles.Section("Firable", "cg-firable-lab-firable", out var firable));
            firable.Add(Field("Firable"));
            var buttons = new VisualElement();
            buttons.AddToClassList("cg-chips");
            buttons.Add(CombatTools.Small("Open in Combat Studio", () =>
            {
                if(settings.Firable != null) CombatStudioWindow.Open(settings.Firable);
            }));
            buttons.Add(CombatTools.Small("Pick from List", () => ShowFirableMenu()));
            firable.Add(buttons);

            scroll.Add(CombatEditorStyles.Section("Simulation", "cg-firable-lab-sim", out var sim));
            foreach(var name in new[] { "Duration", "TickRate", "Input", "ClicksPerSecond", "Distance", "UseHitbox", "HitboxMultiplier", "HitZone", "Samples", "Seed" })
            {
                sim.Add(Field(name));
            }

            scroll.Add(CombatEditorStyles.Section("Attacker", "cg-firable-lab-attacker", out var attacker));
            attacker.Add(CombatEditorStyles.Text("Whose stats and resources fire it: cooldown reduction, reload speed, mana for costs and stat scalers", "cg-note"));
            foreach(var name in new[] { "Unit", "Profile", "Faction", "Tags", "Stats", "Resources" })
            {
                attacker.Add(Field("Attacker." + name));
            }

            scroll.Add(CombatEditorStyles.Section("Defender", "cg-firable-lab-defender", out var defender));
            defender.Add(Field("Pipeline"));
            foreach(var name in new[] { "Unit", "Profile", "Faction", "Tags", "Stats", "Resources" })
            {
                defender.Add(Field("Defender." + name));
            }
            return pane;
        }

        private PropertyField Field(string path) => new(serializedWindow.FindProperty($"{k_Settings}.{path}"));

        private VisualElement BuildCompare()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-firable-lab__compare");
            var columns = new Columns
            {
                MakeColumn("name", "Firable", 160f, (e, r) => ((Label)e).text = StudioPrefs.LabelOf(r.Firable)),
                MakeColumn("mode", "Mode", 90f, (e, r) => ((Label)e).text = r.Mode),
                MakeColumn("rate", "Shots/s", 64f, (e, r) => ((Label)e).text = Number(r.ShotsPerSecond)),
                MakeColumn("shot", "Dmg/Shot", 72f, (e, r) => ((Label)e).text = Number(r.MitigatedPerShot)),
                MakeColumn("dps", "DPS", 64f, (e, r) => ((Label)e).text = Number(r.Sustained)),
                MakeColumn("burst", "Burst DPS", 72f, (e, r) => ((Label)e).text = Number(r.Burst)),
                MakeColumn("mag", "Mag", 48f, (e, r) => ((Label)e).text = r.Magazine > 0 ? r.Magazine.ToString() : "-"),
                MakeColumn("reload", "Reload", 56f, (e, r) => ((Label)e).text = r.Reload > 0f ? $"{Number(r.Reload)}s" : "-"),
                MakeColumn("ttk", "TTK", 64f, (e, r) => ((Label)e).text = r.TimeToKill > 0f ? $"{(r.TimeToKillEstimated ? "~" : string.Empty)}{Number(r.TimeToKill)}s" : "-"),
                MakeColumn("reach", "Reach", 56f, (e, r) => ((Label)e).text = r.Reach > 0f ? $"{Number(r.Reach)}m" : "-")
            };
            table = new MultiColumnListView(columns)
            {
                itemsSource = compared,
                fixedItemHeight = 22f,
                selectionType = SelectionType.Single,
                sortingMode = ColumnSortingMode.Custom,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            table.AddToClassList("cg-db-table");
            table.columnSortingChanged += Sort;
            table.itemsChosen += chosen =>
            {
                if(!(chosen.FirstOrDefault() is FireReport report)) return;
                Undo.RecordObject(this, "Open in Firable Lab");
                settings.Firable = report.Firable;
                view = View.Lab;
                serializedWindow.Update();
                ApplyView();
                Run();
            };
            pane.Add(CombatEditorStyles.Text("Every firable fired by the same attacker at the same defender. Double click a row to open it in the Lab. Effects aren't counted.", "cg-note"));
            pane.Add(table);
            return pane;
        }

        private Column MakeColumn(string name, string title, float width, Action<VisualElement, FireReport> bind)
        {
            return new Column
            {
                name = name,
                title = title,
                width = width,
                stretchable = name == "name",
                sortable = true,
                optional = name != "name",
                makeCell = () => CombatEditorStyles.Text(null, "cg-cell"),
                bindCell = (element, index) => bind(element, compared[index])
            };
        }

        private void ApplyView()
        {
            if(results == null) return;
            views.SetValueWithoutNotify(State(2, (int)view));
            CombatEditorStyles.Show(results, view == View.Lab);
            CombatEditorStyles.Show(compare, view == View.Compare);
        }

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(AssetChanges.AnyCombat(guids)) Queue();
        }

        private void Queue()
        {
            runBar?.RefreshLive();
            if(results == null || queued || !StudioPrefs.Get(k_Live, true)) return;
            queued = true;
            results.schedule.Execute(() =>
            {
                queued = false;
                if(view == View.Lab) Run();
            }).ExecuteLater(150);
        }

        private void Run()
        {
            if(results == null || simulator == null) return;
            serializedWindow.Update();
            if(view == View.Compare) RunCompare();
            else RunLab();
        }

        private void RunLab()
        {
            results.Clear();
            var firable = settings.Firable;
            if(firable == null)
            {
                runBar.Idle("Nothing to simulate yet. Pick a firable on the left");
                var empty = new VisualElement();
                empty.AddToClassList("cg-outline-empty");
                empty.Add(CombatEditorStyles.Text("Pick a firable", "cg-outline-empty__title"));
                empty.Add(CombatEditorStyles.Text("The Lab fires it with the real fire mode and gates, then works out damage per shot, DPS and time to kill", "cg-outline-empty__text"));
                empty.Add(CombatTools.Small("Pick from List", () => ShowFirableMenu()));
                results.Add(empty);
                return;
            }
            if(firable != remembered)
            {
                remembered = firable;
                RecentAssets.Remember(firable);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var report = simulator.Run(settings, firable);
            watch.Stop();
            runBar.Report($"Fired {StudioPrefs.LabelOf(firable)} for {Number(report.Duration)}s: {report.Shots.Count} shots, {Number(report.Sustained)} DPS ({watch.ElapsedMilliseconds} ms)");

            results.Add(Summary(report));
            if(StudioPrefs.Get(k_Chart, true)) results.Add(Chart(report));
            foreach(var note in report.Notes)
            {
                results.Add(CombatEditorStyles.Text(note, "cg-note"));
            }
            if(StudioPrefs.Get(k_Breakdown, true)) results.Add(Breakdown(report));
            if(StudioPrefs.Get(k_Effects, true) && report.Effects.Count > 0) results.Add(EffectsSection(report));
        }

        private VisualElement Summary(FireReport report)
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-kind-firable");
            card.Add(CombatEditorStyles.Text($"{report.Mode.ToUpperInvariant()} · {Number(report.Duration)}S", "cg-card__chip"));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("cg-card__title-row");
            var icon = new BlockIcon();
            icon.AddToClassList("cg-card__icon");
            icon.SetStyle("cg-kind-firable");
            titleRow.Add(icon);
            titleRow.Add(CombatEditorStyles.Text(StudioPrefs.LabelOf(report.Firable), "cg-card__title"));
            card.Add(titleRow);

            var tiles = new VisualElement();
            tiles.AddToClassList("cg-tiles");
            tiles.Add(Tile("Shots / s", Number(report.ShotsPerSecond)));
            tiles.Add(Tile("Damage / Shot", Number(StudioPrefs.Get(k_Raw, false) ? report.RawPerShot : report.MitigatedPerShot)));
            tiles.Add(Tile("DPS", Number(report.Sustained)));
            tiles.Add(Tile("Burst DPS", Number(report.Burst)));
            if(report.Magazine > 0) tiles.Add(Tile("Magazine", report.Magazine.ToString()));
            if(report.Reload > 0f) tiles.Add(Tile("Reload", $"{Number(report.Reload)}s"));
            if(report.Health > 0f)
            {
                tiles.Add(Tile("Time to Kill", report.TimeToKill > 0f ? $"{(report.TimeToKillEstimated ? "~" : string.Empty)}{Number(report.TimeToKill)}s" : "never"));
                if(report.ShotsToKill > 0) tiles.Add(Tile("Shots to Kill", report.ShotsToKill.ToString()));
            }
            if(report.Reach > 0f) tiles.Add(Tile("Reach", $"{Number(report.Reach)}m"));
            card.Add(tiles);

            var parts = new List<string> { $"{Number(report.RawPerShot)} per shot before the pipeline, {Number(report.MitigatedPerShot)} after" };
            if(report.Health > 0f) parts.Add($"defender has {Number(report.Health)} health");
            if(report.Waiting.Count > 0)
            {
                float waited = report.Waiting.Values.Sum();
                var top = report.Waiting.OrderByDescending(p => p.Value).Select(p => $"{FirableSimulator.Reason(p.Key)} {Mathf.RoundToInt(p.Value / report.Duration * 100f)}%");
                parts.Add($"waiting {Mathf.RoundToInt(waited / report.Duration * 100f)}% of the time ({string.Join(", ", top)})");
            }
            card.Add(CombatEditorStyles.Text(string.Join(" · ", parts), "cg-card__summary"));
            return card;
        }

        private VisualElement Chart(FireReport report)
        {
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Damage Over Time", "cg-slot-list__title"));
            section.Add(header);
            var chart = new FireTimelineElement();
            BlockCategories.Apply(chart, BlockCategory.FireMode);
            chart.SetReport(report);
            chart.tooltip = "Line: total damage dealt. Ticks: shots. Shaded: reloading, charging or channeling. Red: defender health and the moment it dies";
            section.Add(chart);
            var axis = new VisualElement();
            axis.AddToClassList("cg-histogram__axis");
            axis.Add(CombatEditorStyles.Text("0s", "cg-histogram__min"));
            axis.Add(CombatEditorStyles.Text($"{Number(report.Duration)}s", "cg-histogram__max"));
            section.Add(axis);
            return section;
        }

        private VisualElement Breakdown(FireReport report)
        {
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Damage Per Shot", "cg-slot-list__title"));
            section.Add(header);
            if(report.Hits.Count == 0)
            {
                section.Add(CombatEditorStyles.Text("No Deal Damage blocks found", "cg-empty-note"));
                return section;
            }
            foreach(var hit in report.Hits)
            {
                var row = new VisualElement();
                row.AddToClassList("cg-slot");
                row.AddToClassList("cg-trace-row");
                BlockCategories.Apply(row, BlockCategory.Payload);
                var line = new VisualElement();
                line.AddToClassList("cg-trace-row__line");
                var icon = new BlockIcon();
                icon.SetCategory(BlockCategory.Payload);
                line.Add(icon);
                string type = hit.Type != null ? StudioKinds.NameOf(hit.Type) : "untyped";
                line.Add(CombatEditorStyles.Text($"{type} damage", "cg-slot__title"));
                line.Add(CombatEditorStyles.Text(hit.Multiplier > 1.0001f || hit.Multiplier < 0.9999f ? $"×{Number(hit.Multiplier)}" : null, "cg-slot__summary"));
                line.Add(CombatEditorStyles.Text($"{Number(hit.Raw)} → {Number(hit.Mitigated)}", "cg-trace-row__values"));
                row.Add(line);
                var notes = new List<string> { hit.Where };
                if(hit.Scaled) notes.Add("scaled");
                if(!string.IsNullOrEmpty(hit.Note)) notes.Add(hit.Note);
                row.Add(CombatEditorStyles.Text(string.Join(" · ", notes), "cg-trace-row__notes"));
                section.Add(row);
            }
            return section;
        }

        private VisualElement EffectsSection(FireReport report)
        {
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Status Effects Applied", "cg-slot-list__title"));
            section.Add(header);
            foreach(var hit in report.Effects)
            {
                var row = new VisualElement();
                row.AddToClassList("cg-slot");
                row.AddToClassList("cg-trace-row");
                BlockCategories.Apply(row, BlockCategory.Behavior);
                var line = new VisualElement();
                line.AddToClassList("cg-trace-row__line");
                var icon = new BlockIcon();
                icon.SetCategory(BlockCategory.Behavior);
                line.Add(icon);
                line.Add(CombatEditorStyles.Text(StudioPrefs.LabelOf(hit.Effect), "cg-slot__title"));
                float damage = FirableDamage.EffectDamage(hit.Effect, out int ticks);
                string summary = damage > 0f ? $"about {Number(damage)} damage per application{(ticks > 0 ? $" over {ticks} ticks" : string.Empty)}, before the pipeline" : "no direct damage found";
                line.Add(CombatEditorStyles.Text($"×{hit.Stacks} stacks · {summary}", "cg-slot__summary"));
                var effect = hit.Effect;
                line.Add(CombatTools.Small("Timeline", () => EffectTimelineWindow.Open(effect)));
                row.Add(line);
                row.Add(CombatEditorStyles.Text(hit.Where + (hit.Estimate ? " · estimate" : string.Empty), "cg-trace-row__notes"));
                section.Add(row);
            }
            return section;
        }

        private void RunCompare()
        {
            compared.Clear();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var firables = new List<FirableDefinition>();
            foreach(var guid in AssetDatabase.FindAssets("t:FirableDefinition"))
            {
                var each = AssetDatabase.LoadAssetAtPath<FirableDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(each != null) firables.Add(each);
            }
            try
            {
                for(int i = 0; i < firables.Count; i++)
                {
                    if(firables.Count > 8 && EditorUtility.DisplayCancelableProgressBar(k_Title, $"Firing {StudioKinds.NameOf(firables[i])}", i / (float)firables.Count)) break;
                    compared.Add(simulator.Run(settings, firables[i]));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            watch.Stop();
            Sort();
            runBar.Report($"Compared {compared.Count} firables over {Number(settings.Duration)}s each ({watch.ElapsedMilliseconds} ms)");
        }

        private void Sort()
        {
            var column = table.sortedColumns.FirstOrDefault();
            Func<FireReport, float> key = r => r.Sustained;
            bool descending = true;
            string name = column?.columnName;
            if(column != null) descending = column.direction == SortDirection.Descending;
            switch(name)
            {
                case "name":
                    compared.Sort((a, b) => string.Compare(StudioPrefs.LabelOf(a.Firable), StudioPrefs.LabelOf(b.Firable), StringComparison.OrdinalIgnoreCase) * (descending ? -1 : 1));
                    table.RefreshItems();
                    return;
                case "mode":
                    compared.Sort((a, b) => string.Compare(a.Mode, b.Mode, StringComparison.OrdinalIgnoreCase) * (descending ? -1 : 1));
                    table.RefreshItems();
                    return;
                case "rate": key = r => r.ShotsPerSecond; break;
                case "shot": key = r => r.MitigatedPerShot; break;
                case "burst": key = r => r.Burst; break;
                case "mag": key = r => r.Magazine; break;
                case "reload": key = r => r.Reload; break;
                case "ttk": key = r => r.TimeToKill > 0f ? r.TimeToKill : float.MaxValue; break;
                case "reach": key = r => r.Reach; break;
            }
            compared.Sort((a, b) => key(a).CompareTo(key(b)) * (descending ? -1 : 1));
            table.RefreshItems();
        }

        private void ShowFirableMenu()
        {
            var menu = new GenericMenu();
            foreach(var guid in AssetDatabase.FindAssets("t:FirableDefinition"))
            {
                var each = AssetDatabase.LoadAssetAtPath<FirableDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(each == null) continue;
                var firable = each;
                menu.AddItem(new GUIContent(StudioPrefs.LabelOf(firable)), firable == settings.Firable, () =>
                {
                    Undo.RecordObject(this, "Pick Firable");
                    settings.Firable = firable;
                    serializedWindow.Update();
                    Run();
                });
            }
            if(menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No firables in the project"));
            menu.ShowAsContext();
        }

        private static VisualElement Tile(string label, string value)
        {
            var tile = new VisualElement();
            tile.AddToClassList("cg-tile-stat");
            tile.Add(CombatEditorStyles.Text(value, "cg-tile-stat__value"));
            tile.Add(CombatEditorStyles.Text(label, "cg-tile-stat__label"));
            return tile;
        }

        private static string Number(float value) => CombatEditorStyles.Number(value, Decimals);

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Show"));
            StudioPrefs.Toggle(menu, "Chart", k_Chart, true);
            StudioPrefs.Toggle(menu, "Damage Breakdown", k_Breakdown, true);
            StudioPrefs.Toggle(menu, "Status Effects", k_Effects, true);
            StudioPrefs.Toggle(menu, "Damage Before Pipeline", k_Raw, false);
            menu.AddSeparator(string.Empty);
            for(int i = 0; i <= 3; i++)
            {
                StudioPrefs.Choice(menu, $"Decimals/{i}", k_Decimals, i, 1);
            }
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Live Updates", k_Live, true);
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
    }
}
