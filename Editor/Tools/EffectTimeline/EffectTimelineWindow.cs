using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class EffectTimelineWindow : EditorWindow
    {
        public const string k_Title = "Effect Timeline";
        private const string k_Settings = "settings";
        private const string k_Chart = "EffectTimeline.Chart";
        private const string k_Stats = "EffectTimeline.Stats";
        private const string k_Log = "EffectTimeline.Log";
        private const string k_LogTicks = "EffectTimeline.LogTicks";
        private const string k_LogSource = "EffectTimeline.LogSource";
        private const string k_Live = "EffectTimeline.Live";
        private const string k_Decimals = "EffectTimeline.Decimals";

        [SerializeField] private EffectLabSettings settings = new();

        private readonly EffectSimulator simulator = new();
        private SerializedObject serializedWindow;
        private ScrollView results;
        private RunBar runBar;
        private bool queued;

        private static int Decimals => StudioPrefs.GetInt(k_Decimals, 1);

        [MenuItem(CombatTools.k_Menu + k_Title, false, 24)]
        public static void Open()
        {
            var window = GetWindow<EffectTimelineWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
        }

        public static void Open(StatusEffectDefinition effect)
        {
            var window = GetWindow<EffectTimelineWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            if(effect == null) return;
            Undo.RecordObject(window, "Open in Effect Timeline");
            window.settings.Effect = effect;
            window.serializedWindow?.Update();
            window.Run();
        }

        private void OnEnable()
        {
            StudioPrefs.Changed += Queue;
            AssetChanges.Changed += OnAssetsChanged;
            Undo.undoRedoPerformed += Queue;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            StudioPrefs.Changed -= Queue;
            AssetChanges.Changed -= OnAssetsChanged;
            Undo.undoRedoPerformed -= Queue;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            serializedWindow?.Dispose();
            serializedWindow = null;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatTools.Apply(root, "cg-effect-timeline");
            serializedWindow = new SerializedObject(this);

            var toolbar = CombatTools.Toolbar(k_Title);
            toolbar.Add(new ToolbarSpacer { flex = true });
            CombatTools.Finish(toolbar, ShowViewMenu);
            root.Add(toolbar);

            var split = new TwoPaneSplitView(0, 320f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-effect-timeline-split" };
            split.AddToClassList("cg-studio__split");
            split.Add(BuildSetup());
            var right = new VisualElement();
            right.AddToClassList("cg-pane");
            runBar = new RunBar("Simulate Again", "Play the effect out again with the setup on the left", Run, k_Live);
            runBar.AddButton("New Seed", "Simulate again with different random rolls", () =>
            {
                Undo.RecordObject(this, "New Seed");
                settings.Seed = Random.Range(1, 100000);
                serializedWindow.Update();
                Run();
            });
            right.Add(runBar);
            results = new ScrollView(ScrollViewMode.Vertical);
            results.AddToClassList("cg-detail__scroll");
            results.contentContainer.AddToClassList("cg-detail__content");
            right.Add(results);
            split.Add(right);
            root.Add(split);

            root.Bind(serializedWindow);
            var watcher = new VisualElement();
            watcher.AddToClassList("cg-watcher");
            root.Add(watcher);
            watcher.TrackSerializedObjectValue(serializedWindow, _ => Queue());
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

            scroll.Add(CombatEditorStyles.Section("Effect", "cg-effect-timeline-effect", out var effect));
            effect.Add(Field("Effect"));
            effect.Add(CombatTools.Small("Open in Combat Studio", () =>
            {
                if(settings.Effect != null) CombatStudioWindow.Open(settings.Effect);
            }));

            scroll.Add(CombatEditorStyles.Section("Applications", "cg-effect-timeline-apply", out var apply));
            apply.Add(CombatEditorStyles.Text("Apply it several times to see how stacking and refreshing play out", "cg-note"));
            foreach(var name in new[] { "Applications", "Interval", "Stacks", "Power" })
            {
                apply.Add(Field(name));
            }

            scroll.Add(CombatEditorStyles.Section("Simulation", "cg-effect-timeline-sim", out var sim));
            foreach(var name in new[] { "Duration", "TickRate", "Seed", "UsePipeline", "Pipeline" })
            {
                sim.Add(Field(name));
            }

            scroll.Add(CombatEditorStyles.Section("Hits", "cg-effect-timeline-hits", out var hits));
            hits.Add(CombatEditorStyles.Text("Optional hits between the host and the source, for trigger behaviors like thorns or lifesteal", "cg-note"));
            foreach(var name in new[] { "IncomingEvery", "IncomingDamage", "OutgoingEvery", "OutgoingDamage" })
            {
                hits.Add(Field(name));
            }

            scroll.Add(Side("Host", "Who wears the effect"));
            scroll.Add(Side("Source", "Who applied it"));
            return pane;
        }

        private VisualElement Side(string name, string note)
        {
            var section = CombatEditorStyles.Section(name, "cg-effect-timeline-" + name.ToLowerInvariant(), out var content);
            content.Add(CombatEditorStyles.Text(note, "cg-note"));
            foreach(var field in new[] { "Unit", "Profile", "Faction", "Tags", "Stats", "Resources" })
            {
                content.Add(Field($"{name}.{field}"));
            }
            return section;
        }

        private PropertyField Field(string path) => new(serializedWindow.FindProperty($"{k_Settings}.{path}"));

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(AssetChanges.AnyCombat(guids)) Queue();
        }

        private void OnPlayModeChanged(PlayModeStateChange change) => Queue();

        private void Queue()
        {
            runBar?.RefreshLive();
            if(results == null || queued || !StudioPrefs.Get(k_Live, true)) return;
            queued = true;
            results.schedule.Execute(() =>
            {
                queued = false;
                Run();
            }).ExecuteLater(150);
        }

        private void Run()
        {
            if(results == null) return;
            serializedWindow.Update();
            results.Clear();
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Empty("Stop play mode to simulate", "The timeline borrows the combat ticker and random source, so it only runs in edit mode. Use the Combat Monitor while playing");
                runBar.Idle("Paused while in play mode");
                return;
            }
            if(settings.Effect == null)
            {
                Empty("Pick a status effect", "The timeline applies it to a sandbox host and plays it out tick by tick, with real behaviors and payloads");
                runBar.Idle("Nothing to simulate yet. Pick a status effect on the left");
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var report = simulator.Run(settings);
            watch.Stop();
            runBar.Report($"Played {StudioPrefs.LabelOf(settings.Effect)} for {Number(report.Duration)}s: {report.Events.Count} events, {Number(report.DamageTaken)} damage ({watch.ElapsedMilliseconds} ms)");

            results.Add(Summary(report));
            if(StudioPrefs.Get(k_Chart, true)) results.Add(Chart(report));
            foreach(var note in report.Notes)
            {
                results.Add(CombatEditorStyles.Text(note, "cg-note"));
            }
            if(StudioPrefs.Get(k_Stats, true) && report.Stats.Any(s => s.Changed)) results.Add(Stats(report));
            if(StudioPrefs.Get(k_Log, true)) results.Add(Log(report));
        }

        private VisualElement Summary(EffectReport report)
        {
            var effect = settings.Effect;
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-kind-effect");
            string stacking = $"{ObjectNames.NicifyVariableName(effect.Stacking.ToString())} stacking, up to {effect.MaxStacks}";
            card.Add(CombatEditorStyles.Text($"{(effect.IsPermanent ? "PERMANENT" : $"{Number(effect.Duration)}S")} · {stacking.ToUpperInvariant()}", "cg-card__chip"));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("cg-card__title-row");
            var icon = new BlockIcon();
            icon.AddToClassList("cg-card__icon");
            icon.SetStyle("cg-kind-effect");
            titleRow.Add(icon);
            titleRow.Add(CombatEditorStyles.Text(StudioPrefs.LabelOf(effect), "cg-card__title"));
            card.Add(titleRow);

            var tiles = new VisualElement();
            tiles.AddToClassList("cg-tiles");
            tiles.Add(Tile("Lasted", report.EndedAt >= 0f ? $"{Number(report.EndedAt)}s" : "still on"));
            tiles.Add(Tile("Damage Taken", Number(report.DamageTaken)));
            if(report.Healed > 0f) tiles.Add(Tile("Healed", Number(report.Healed)));
            tiles.Add(Tile("Periodic Hits", report.Ticks.ToString()));
            tiles.Add(Tile("Max Stacks", report.MaxStacks.ToString()));
            if(report.DamageToSource > 0f) tiles.Add(Tile("Source Took", Number(report.DamageToSource)));
            if(report.HealedSource > 0f) tiles.Add(Tile("Source Healed", Number(report.HealedSource)));
            card.Add(tiles);

            var parts = new List<string>();
            if(report.EndedAt >= 0f) parts.Add($"ended by {report.EndReason?.ToLowerInvariant()}");
            if(report.HostDied) parts.Add("the host died");
            if(report.DamageTaken > 0f && report.EndedAt > 0f) parts.Add($"{Number(report.DamageTaken / report.EndedAt)} damage per second while it lasted");
            if(parts.Count > 0) card.Add(CombatEditorStyles.Text(string.Join(" · ", parts), "cg-card__summary"));
            return card;
        }

        private VisualElement Chart(EffectReport report)
        {
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Timeline", "cg-slot-list__title"));
            section.Add(header);
            var chart = new EffectChartElement();
            BlockCategories.Apply(chart, BlockCategory.Behavior);
            chart.SetReport(report);
            chart.tooltip = "Blue: stacks. Green line: host health. Bars: damage (red) and heals (teal). Thin lines: applications. Dashed: when it ended";
            section.Add(chart);
            var axis = new VisualElement();
            axis.AddToClassList("cg-histogram__axis");
            axis.Add(CombatEditorStyles.Text("0s", "cg-histogram__min"));
            axis.Add(CombatEditorStyles.Text($"{Number(report.Duration)}s", "cg-histogram__max"));
            section.Add(axis);
            return section;
        }

        private VisualElement Stats(EffectReport report)
        {
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Host Stats", "cg-slot-list__title"));
            section.Add(header);
            foreach(var track in report.Stats.Where(s => s.Changed))
            {
                var row = new VisualElement();
                row.AddToClassList("cg-slot");
                row.AddToClassList("cg-kind-stat");
                var icon = new BlockIcon();
                icon.SetStyle("cg-kind-stat");
                row.Add(icon);
                row.Add(CombatEditorStyles.Text(StudioPrefs.LabelOf(track.Stat), "cg-slot__title"));
                row.Add(CombatEditorStyles.Text($"base {Number(track.Base)} · lowest {Number(track.Min)} · highest {Number(track.Max)} · after {Number(track.End)}", "cg-slot__summary"));
                section.Add(row);
            }
            return section;
        }

        private VisualElement Log(EffectReport report)
        {
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Events", "cg-slot-list__title"));
            section.Add(header);
            bool ticks = StudioPrefs.Get(k_LogTicks, true);
            bool sourceEvents = StudioPrefs.Get(k_LogSource, true);
            int shown = 0;
            foreach(var each in report.Events.OrderBy(e => e.Time))
            {
                if(!ticks && each.Periodic) continue;
                if(!sourceEvents && each.OnSource) continue;
                var row = new VisualElement();
                row.AddToClassList("cg-log-row");
                row.AddToClassList("cg-log-row--" + each.Kind.ToString().ToLowerInvariant());
                row.Add(CombatEditorStyles.Text($"{each.Time:0.00}s", "cg-log-row__time"));
                row.Add(CombatEditorStyles.Text(each.Text, "cg-log-row__text"));
                section.Add(row);
                if(++shown >= 400)
                {
                    section.Add(CombatEditorStyles.Text("Showing the first 400 events", "cg-note"));
                    break;
                }
            }
            if(shown == 0) section.Add(CombatEditorStyles.Text("Nothing happened", "cg-empty-note"));
            return section;
        }

        private void Empty(string title, string text)
        {
            var empty = new VisualElement();
            empty.AddToClassList("cg-outline-empty");
            empty.Add(CombatEditorStyles.Text(title, "cg-outline-empty__title"));
            empty.Add(CombatEditorStyles.Text(text, "cg-outline-empty__text"));
            results.Add(empty);
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
            StudioPrefs.Toggle(menu, "Host Stats", k_Stats, true);
            StudioPrefs.Toggle(menu, "Event Log", k_Log, true);
            StudioPrefs.Toggle(menu, "Log/Periodic Damage", k_LogTicks, true);
            StudioPrefs.Toggle(menu, "Log/Source Events", k_LogSource, true);
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
    }
}
