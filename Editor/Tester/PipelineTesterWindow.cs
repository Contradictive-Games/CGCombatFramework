using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class PipelineTesterWindow : EditorWindow
    {
        private const string k_Title = "Pipeline Tester";
        private const string k_Bars = "Tester.Bars";
        private const string k_Summaries = "Tester.Summaries";
        private const string k_Rolls = "Tester.Rolls";
        private const string k_Health = "Tester.Health";
        private const string k_Distribution = "Tester.Distribution";
        private const string k_Explorer = "Tester.Explorer";
        private const string k_Decimals = "Tester.Decimals";
        private const string k_Live = "Tester.Live";
        private const string k_Settings = "settings";
        private const string k_Order = "order";
        private const int k_Bins = 24;
        private const int k_ExploreSamples = 200;
        private const long k_PollMs = 500;

        [SerializeField] private TesterSettings settings = new();
        [SerializeField] private List<int> order = new();
        [SerializeField] private List<int> muted = new();
        [SerializeField] private DamagePipeline orderFor;

        private PipelineSimulation simulation;
        private SerializedObject serializedWindow;
        private ScrollView resultScroll;
        private RunBar runBar;
        private Label pipelineNote;
        private Label attackerNote;
        private Label defenderNote;
        private List<OrderGroup> explored;
        private string exploredKey;
        private string signature;
        private bool recalculateQueued;

        private static int Decimals => StudioPrefs.GetInt(k_Decimals, 1);

        [MenuItem(StudioWindows.k_Menu + k_Title, false, 2)]
        public static void Open() => Open(null);

        public static void Open(DamagePipeline pipeline)
        {
            var window = GetWindow<PipelineTesterWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            if(pipeline == null) return;
            Undo.RecordObject(window, "Test Pipeline");
            window.settings.Pipeline = pipeline;
            window.Refresh();
        }

        public static void OpenWithDefender(GameObject unit)
        {
            var window = GetWindow<PipelineTesterWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
            if(unit == null) return;
            Undo.RecordObject(window, "Test Unit");
            window.settings.Defender.Unit = unit;
            window.Refresh();
        }

        private void OnEnable()
        {
            simulation = new PipelineSimulation();
            StudioPrefs.Changed += Recalculate;
            Undo.undoRedoPerformed += Refresh;
            AssetChanges.Changed += OnAssetsChanged;
        }

        private void OnDisable()
        {
            StudioPrefs.Changed -= Recalculate;
            Undo.undoRedoPerformed -= Refresh;
            AssetChanges.Changed -= OnAssetsChanged;
            simulation?.Dispose();
            simulation = null;
            serializedWindow?.Dispose();
            serializedWindow = null;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-studio");
            root.AddToClassList("cg-tester");

            serializedWindow = new SerializedObject(this);
            root.Add(BuildToolbar());

            var split = new TwoPaneSplitView(0, 320f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-tester-split" };
            split.AddToClassList("cg-studio__split");
            split.Add(BuildSetup());

            var right = new VisualElement();
            right.AddToClassList("cg-pane");
            runBar = new RunBar("Recalculate", "Run the hit again with the setup on the left", Recalculate, k_Live);
            runBar.AddButton("New Seed", "Roll with a different random seed", Reseed);
            right.Add(runBar);
            resultScroll = new ScrollView(ScrollViewMode.Vertical);
            resultScroll.AddToClassList("cg-detail__scroll");
            resultScroll.contentContainer.AddToClassList("cg-detail__content");
            right.Add(resultScroll);
            split.Add(right);
            root.Add(split);

            root.Bind(serializedWindow);
            var watcher = new VisualElement();
            watcher.AddToClassList("cg-watcher");
            root.Add(watcher);
            watcher.TrackSerializedObjectValue(serializedWindow, _ => Recalculate());
            root.schedule.Execute(Poll).Every(k_PollMs);
            Recalculate();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("cg-toolbar");
            toolbar.Add(CombatEditorStyles.Text(k_Title, "cg-toolbar__title"));
            toolbar.Add(new ToolbarButton(Explore) { text = "Explore Orders", tooltip = "Try every order of the active steps" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            ToolbarButton view = null;
            view = new ToolbarButton(() => ShowViewMenu(view.worldBound)) { text = "View" };
            toolbar.Add(view);
            toolbar.Add(StudioWindows.Menu());
            return toolbar;
        }

        private VisualElement BuildSetup()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            pane.AddToClassList("cg-setup");
            pane.Add(CombatEditorStyles.Header("Setup"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cg-detail__scroll");
            scroll.contentContainer.AddToClassList("cg-setup__content");
            pane.Add(scroll);

            scroll.Add(CombatEditorStyles.Section("Pipeline", "cg-tester-pipeline", out var pipeline));
            pipeline.Add(Field("Pipeline"));
            pipelineNote = CombatEditorStyles.Text(null, "cg-note");
            pipeline.Add(pipelineNote);
            var studio = new Button(() =>
            {
                var target = settings.EffectivePipeline;
                if(target != null) CombatStudioWindow.Open(target);
            }) { text = "Open in Combat Studio" };
            studio.AddToClassList("cg-setup__button");
            pipeline.Add(studio);

            scroll.Add(CombatEditorStyles.Section("Hit", "cg-tester-hit", out var hit));
            foreach(var name in new[] { "Amount", "DamageType", "Flags", "Cause", "UseHitbox", "HitboxMultiplier", "HitZone", "SelfHit" })
            {
                hit.Add(Field(name));
            }

            scroll.Add(CombatEditorStyles.Section("Rolls", "cg-tester-rolls", out var rolls));
            foreach(var name in new[] { "Rolls", "Seed", "Samples" })
            {
                rolls.Add(Field(name));
            }

            scroll.Add(Side("Attacker", out attackerNote));
            scroll.Add(Side("Defender", out defenderNote));
            return pane;
        }

        private VisualElement Side(string name, out Label note)
        {
            var section = CombatEditorStyles.Section(name, "cg-tester-" + name.ToLowerInvariant(), out var content);
            note = CombatEditorStyles.Text(null, "cg-note");
            content.Add(note);
            foreach(var field in new[] { "Unit", "Profile", "Faction", "Tags", "Stats", "Resources" })
            {
                content.Add(new PropertyField(serializedWindow.FindProperty($"{k_Settings}.{name}.{field}")));
            }
            return section;
        }

        private PropertyField Field(string name) => new(serializedWindow.FindProperty($"{k_Settings}.{name}"));

        private void Refresh()
        {
            serializedWindow?.Update();
            Recalculate();
        }

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(resultScroll == null || recalculateQueued || !AssetChanges.AnyCombat(guids)) return;
            recalculateQueued = true;
            resultScroll.schedule.Execute(() =>
            {
                recalculateQueued = false;
                Recalculate();
            }).ExecuteLater(60);
        }

        private void Reseed()
        {
            Undo.RecordObject(this, "New Seed");
            settings.Seed = UnityEngine.Random.Range(1, 100000);
            Refresh();
        }

        private void Poll()
        {
            if(!StudioPrefs.Get(k_Live, true)) return;
            string current = Signature();
            if(current == signature) return;
            Recalculate();
        }

        private string Signature()
        {
            var pipeline = settings.EffectivePipeline;
            return string.Join("|",
                Dirty(pipeline), Dirty(settings.DamageType),
                Dirty(settings.Attacker.Unit), Dirty(settings.Attacker.EffectiveProfile),
                Dirty(settings.Defender.Unit), Dirty(settings.Defender.EffectiveProfile),
                pipeline != null && pipeline.Steps != null ? pipeline.Steps.Length : -1);
        }

        private static int Dirty(UnityEngine.Object target) => target != null ? EditorUtility.GetDirtyCount(target) : 0;

        private void Recalculate()
        {
            if(resultScroll == null || simulation == null) return;
            signature = Signature();
            var pipeline = settings.EffectivePipeline;
            SyncOrder(pipeline);
            simulation.Load(settings);
            UpdateNotes(pipeline);
            resultScroll.Clear();

            if(pipeline == null)
            {
                var empty = new VisualElement();
                empty.AddToClassList("cg-outline-empty");
                empty.Add(CombatEditorStyles.Text("No pipeline to test", "cg-outline-empty__title"));
                empty.Add(CombatEditorStyles.Text("Pick a damage pipeline on the left, or a defender unit that has one", "cg-outline-empty__text"));
                resultScroll.Add(empty);
                runBar.Idle("Nothing to test yet. Pick a pipeline, or a defender that has one");
                return;
            }

            var steps = pipeline.Steps ?? Array.Empty<IDamageStep>();
            var run = simulation.Trace(steps, order, muted, new TesterRandom(settings.Rolls, settings.Seed));
            SampleStats sampled = null;
            if(settings.Rolls == RollMode.Random) sampled = simulation.Sample(steps, order, muted, settings.Samples, settings.Seed);

            string average = sampled != null ? $", {CombatEditorStyles.Number(sampled.Mean, Decimals)} on average over {sampled.Count} hits" : string.Empty;
            runBar.Report($"Hit {StudioPrefs.LabelOf(pipeline)} for {CombatEditorStyles.Number(settings.Amount, Decimals)}: {CombatEditorStyles.Number(run.Final, Decimals)} damage{average}");
            resultScroll.Add(ResultCard(run, sampled));
            resultScroll.Add(StepsSection(pipeline, run));
            if(StudioPrefs.Get(k_Distribution, true)) resultScroll.Add(DistributionSection(run, sampled));
            if(StudioPrefs.Get(k_Explorer, true)) resultScroll.Add(ExplorerSection(pipeline));
        }

        private void SyncOrder(DamagePipeline pipeline)
        {
            int count = pipeline != null && pipeline.Steps != null ? pipeline.Steps.Length : 0;
            bool valid = orderFor == pipeline && order.Count == count && order.Distinct().Count() == count && order.All(i => i >= 0 && i < count);
            if(valid) return;
            orderFor = pipeline;
            order.Clear();
            for(int i = 0; i < count; i++)
            {
                order.Add(i);
            }
            muted.RemoveAll(i => i >= count);
            explored = null;
            serializedWindow?.Update();
        }

        private void UpdateNotes(DamagePipeline pipeline)
        {
            if(pipelineNote != null)
            {
                string note = settings.Pipeline != null ? null
                    : pipeline != null ? $"Using the defender's pipeline: {StudioKinds.NameOf(pipeline)}"
                    : "Empty: uses the defender's pipeline when it has one";
                CombatEditorStyles.SetText(pipelineNote, note);
            }
            CombatEditorStyles.SetText(attackerNote, SideNote(settings.Attacker, simulation.Attacker));
            CombatEditorStyles.SetText(defenderNote, SideNote(settings.Defender, simulation.Defender));
        }

        private static string SideNote(SandboxSetup setup, SandboxUnit unit)
        {
            var parts = new List<string>();
            if(setup.Unit != null && !UnitLibrary.IsUnit(setup.Unit)) parts.Add("not a unit prefab");
            parts.Add(unit.Profile != null ? $"Profile: {StudioKinds.NameOf(unit.Profile)}" : "No stat profile");
            var faction = setup.EffectiveFaction;
            if(faction != null) parts.Add($"Faction: {StudioKinds.NameOf(faction)}");
            int tags = unit.Tags.All.Count();
            if(tags > 0) parts.Add(tags == 1 ? "1 tag" : $"{tags} tags");
            return string.Join(" · ", parts);
        }

        private VisualElement ResultCard(SimulationRun run, SampleStats sampled)
        {
            int decimals = Decimals;
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-result");
            BlockCategories.Apply(card, BlockCategory.DamageStep);
            card.Add(CombatEditorStyles.Text(settings.Rolls == RollMode.Random ? $"THIS HIT · SEED {settings.Seed}" : "THIS HIT", "cg-card__chip"));

            var line = new VisualElement();
            line.AddToClassList("cg-result__line");
            line.Add(CombatEditorStyles.Text(CombatEditorStyles.Number(run.Final, decimals), "cg-result__value"));
            line.Add(CombatEditorStyles.Text("damage", "cg-result__unit"));
            if(run.Crit) line.Add(CombatEditorStyles.Chip("Crit", "cg-chip--crit"));
            if(run.Result != HitResult.Hit) line.Add(CombatEditorStyles.Chip(run.Result.ToString(), "cg-chip--cancel"));
            card.Add(line);

            var details = new List<string> { $"from {CombatEditorStyles.Number(run.Requested, decimals)} requested" };
            if(run.Absorbed > 0f) details.Add($"{CombatEditorStyles.Number(run.Absorbed, decimals)} absorbed");
            if(sampled != null) details.Add($"{CombatEditorStyles.Number(sampled.Mean, decimals)} on average");
            card.Add(CombatEditorStyles.Text(string.Join(" · ", details), "cg-card__summary"));

            if(run.HasHealth && StudioPrefs.Get(k_Health, true))
            {
                var health = new VisualElement();
                health.AddToClassList("cg-health");
                float max = Mathf.Max(0.0001f, run.HealthMax);
                var track = new VisualElement();
                track.AddToClassList("cg-health__track");
                var lost = new VisualElement();
                lost.AddToClassList("cg-health__lost");
                lost.style.width = Length.Percent(Mathf.Clamp01(run.HealthBefore / max) * 100f);
                var left = new VisualElement();
                left.AddToClassList("cg-health__left");
                left.style.width = Length.Percent(Mathf.Clamp01(run.HealthAfter / max) * 100f);
                if(run.Health != null) left.style.backgroundColor = run.Health.Color;
                track.Add(lost);
                track.Add(left);
                health.Add(track);

                string label = $"{StudioKinds.NameOf(run.Health)} {CombatEditorStyles.Number(run.HealthBefore, decimals)} → {CombatEditorStyles.Number(run.HealthAfter, decimals)} of {CombatEditorStyles.Number(run.HealthMax, decimals)}";
                float average = sampled != null ? sampled.Mean : run.Final;
                if(average > 0f) label += $" · about {Mathf.CeilToInt(run.HealthBefore / average)} hits to kill";
                health.Add(CombatEditorStyles.Text(label, "cg-health__label"));
                card.Add(health);
            }
            return card;
        }

        private VisualElement StepsSection(DamagePipeline pipeline, SimulationRun run)
        {
            int decimals = Decimals;
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            section.AddToClassList("cg-trace");

            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Steps", "cg-slot-list__title"));
            bool reordered = !order.SequenceEqual(Enumerable.Range(0, order.Count));
            var reset = new Button(ResetOrder) { text = "Reset Order" };
            reset.SetEnabled(reordered || muted.Count > 0);
            reset.AddToClassList("cg-add-button");
            header.Add(reset);
            var apply = new Button(() => ApplyOrder(pipeline)) { text = "Apply Order", tooltip = "Save this order into the pipeline asset" };
            apply.SetEnabled(reordered);
            apply.AddToClassList("cg-add-button");
            header.Add(apply);
            section.Add(header);

            if(run.Steps.Count == 0)
            {
                section.Add(CombatEditorStyles.Text("This pipeline has no steps, so damage goes straight through", "cg-empty-note"));
                return section;
            }

            float scale = Mathf.Max(0.0001f, run.Requested);
            foreach(var trace in run.Steps)
            {
                scale = Mathf.Max(scale, trace.Before, trace.After);
            }

            using(var serialized = new SerializedObject(pipeline))
            {
                for(int i = 0; i < run.Steps.Count; i++)
                {
                    var trace = run.Steps[i];
                    var property = serialized.FindProperty($"Steps.Array.data[{trace.Index}]");
                    section.Add(TraceRow(pipeline, trace, property, i, scale, decimals));
                }
            }

            var final = new VisualElement();
            final.AddToClassList("cg-trace__final");
            final.Add(CombatEditorStyles.Text("Final (never below 0)", "cg-trace__final-label"));
            final.Add(CombatEditorStyles.Text(CombatEditorStyles.Number(run.Final, decimals), "cg-trace__final-value"));
            section.Add(final);
            return section;
        }

        private VisualElement TraceRow(DamagePipeline pipeline, StepTrace trace, SerializedProperty property, int position, float scale, int decimals)
        {
            var type = trace.Step?.GetType();
            var row = new VisualElement { userData = $"{k_Order}.Array.data[{position}]" };
            row.AddToClassList("cg-slot");
            row.AddToClassList("cg-trace-row");
            BlockCategories.Apply(row, BlockCategory.DamageStep);
            row.EnableInClassList("cg-trace-row--muted", trace.Muted);
            row.EnableInClassList("cg-trace-row--skipped", trace.Skipped);
            row.EnableInClassList("cg-trace-row--error", trace.Error != null);
            row.tooltip = BlockTypes.TooltipOf(type);

            var line = new VisualElement();
            line.AddToClassList("cg-trace-row__line");
            if(StudioPrefs.DragToReorder) line.Add(new SlotGrip());
            var enabled = new Toggle { value = !trace.Muted, tooltip = "Include this step" };
            enabled.AddToClassList("cg-trace-row__toggle");
            enabled.RegisterValueChangedCallback(evt => SetMuted(trace.Index, !evt.newValue));
            line.Add(enabled);
            var icon = new BlockIcon();
            icon.SetCategory(BlockCategory.DamageStep);
            line.Add(icon);
            line.Add(CombatEditorStyles.Text(type == null ? "Empty Step" : BlockTypes.DisplayName(type), "cg-slot__title"));
            line.Add(CombatEditorStyles.Text(StudioPrefs.Get(k_Summaries, true) && property != null ? BlockSummary.Of(property, true) : null, "cg-slot__summary"));

            string values = trace.Muted ? "off" : trace.Skipped ? "skipped" : $"{CombatEditorStyles.Number(trace.Before, decimals)} → {CombatEditorStyles.Number(trace.After, decimals)}";
            line.Add(CombatEditorStyles.Text(values, "cg-trace-row__values"));
            float delta = trace.Delta;
            var change = CombatEditorStyles.Text(Mathf.Abs(delta) < 0.0005f ? "±0" : (delta > 0f ? "+" : "") + CombatEditorStyles.Number(delta, decimals), "cg-trace-row__delta");
            change.EnableInClassList("cg-trace-row__delta--up", delta > 0.0005f);
            change.EnableInClassList("cg-trace-row__delta--down", delta < -0.0005f);
            CombatEditorStyles.Show(change, !trace.Muted && !trace.Skipped);
            line.Add(change);
            row.Add(line);

            var notes = new List<string>();
            if(trace.Crit) notes.Add("Critical hit");
            if(trace.Absorbed > 0f) notes.Add($"{CombatEditorStyles.Number(trace.Absorbed, decimals)} absorbed");
            if(!trace.Skipped && !trace.Muted && trace.Result != HitResult.Hit && trace.Before > 0f && trace.After <= 0f) notes.Add($"Cancelled: {trace.Result}");
            if(StudioPrefs.Get(k_Rolls, true) && !string.IsNullOrEmpty(trace.Rolls)) notes.Add(trace.Rolls);
            if(trace.Error != null) notes.Add("Error: " + trace.Error);
            if(notes.Count > 0) row.Add(CombatEditorStyles.Text(string.Join(" · ", notes), "cg-trace-row__notes"));

            if(StudioPrefs.Get(k_Bars, true))
            {
                var bar = new VisualElement();
                bar.AddToClassList("cg-trace-row__bar");
                var fill = new VisualElement();
                fill.AddToClassList("cg-trace-row__fill");
                fill.style.width = Length.Percent(Mathf.Clamp01(trace.After / scale) * 100f);
                bar.Add(fill);
                row.Add(bar);
            }

            row.AddManipulator(new SlotDragger(each => each.userData as string, () => row.parent.Children(), MoveStep));
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if(evt.clickCount == 2) CombatStudioWindow.Open(pipeline, $"Steps.Array.data[{trace.Index}]");
            });
            return row;
        }

        private VisualElement DistributionSection(SimulationRun run, SampleStats sampled)
        {
            int decimals = Decimals;
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Distribution", "cg-slot-list__title"));
            section.Add(header);

            if(sampled == null)
            {
                section.Add(CombatEditorStyles.Text("Set Rolls to Random to see how hits spread out over many samples", "cg-empty-note"));
                return section;
            }

            section.Add(CombatEditorStyles.Text($"{sampled.Count} hits with seed {settings.Seed}", "cg-note"));
            var tiles = new VisualElement();
            tiles.AddToClassList("cg-tiles");
            tiles.Add(Tile("Min", CombatEditorStyles.Number(sampled.Min, decimals)));
            tiles.Add(Tile("Average", CombatEditorStyles.Number(sampled.Mean, decimals)));
            tiles.Add(Tile("Median", CombatEditorStyles.Number(sampled.Median, decimals)));
            tiles.Add(Tile("Max", CombatEditorStyles.Number(sampled.Max, decimals)));
            tiles.Add(Tile("Crits", Percent(sampled.CritRate)));
            foreach(var pair in sampled.Results.OrderBy(p => p.Key))
            {
                if(pair.Key != HitResult.Hit) tiles.Add(Tile(pair.Key.ToString(), Percent(pair.Value / (float)sampled.Count)));
            }
            if(sampled.MeanAbsorbed > 0f) tiles.Add(Tile("Absorbed", CombatEditorStyles.Number(sampled.MeanAbsorbed, decimals)));
            section.Add(tiles);

            var chart = new HistogramElement();
            BlockCategories.Apply(chart, BlockCategory.DamageStep);
            chart.SetValues(sampled.Values, run.Final, k_Bins);
            chart.tooltip = "How often each amount came up. The line marks this hit";
            section.Add(chart);
            var axis = new VisualElement();
            axis.AddToClassList("cg-histogram__axis");
            axis.Add(CombatEditorStyles.Text(CombatEditorStyles.Number(sampled.Min, decimals), "cg-histogram__min"));
            axis.Add(CombatEditorStyles.Text(CombatEditorStyles.Number(sampled.Max, decimals), "cg-histogram__max"));
            section.Add(axis);
            return section;
        }

        private VisualElement ExplorerSection(DamagePipeline pipeline)
        {
            int decimals = Decimals;
            var steps = pipeline.Steps ?? Array.Empty<IDamageStep>();
            int active = order.Count(i => !muted.Contains(i) && steps[i] != null);

            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            section.AddToClassList("cg-explorer");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text("Order Explorer", "cg-slot-list__title"));
            var run = new Button(Explore) { text = $"Explore {PipelineSimulation.Factorial(active)} Orders" };
            run.AddToClassList("cg-add-button");
            run.SetEnabled(active > 1 && active <= PipelineSimulation.k_MaxExploreSteps);
            header.Add(run);
            section.Add(header);

            string how = settings.Rolls == RollMode.Random
                ? $"Runs every order of the active steps, {k_ExploreSamples} hits each with the same rolls, then groups orders that land on the same average."
                : "Runs every order of the active steps and groups orders that give the same result.";
            section.Add(CombatEditorStyles.Text(how, "cg-note"));
            if(active > PipelineSimulation.k_MaxExploreSteps) section.Add(CombatEditorStyles.Text($"Up to {PipelineSimulation.k_MaxExploreSteps} steps can be explored. Turn some off to compare the rest.", "cg-card__warning"));

            if(explored == null) return section;
            if(exploredKey != ExploreKey()) section.Add(CombatEditorStyles.Text("Settings changed since this ran. Explore again to update.", "cg-card__warning"));

            if(explored.Count == 1)
            {
                section.Add(CombatEditorStyles.Text($"Order doesn't matter here. Every order gives {CombatEditorStyles.Number(explored[0].Mean, decimals)}.", "cg-info__text"));
            }
            else if(explored.Count > 1)
            {
                float low = explored[0].Mean;
                float high = explored[explored.Count - 1].Mean;
                string spread = low > 0f ? $" ({Percent((high - low) / low)} more)" : string.Empty;
                section.Add(CombatEditorStyles.Text($"Order changes the result from {CombatEditorStyles.Number(low, decimals)} to {CombatEditorStyles.Number(high, decimals)}{spread}.", "cg-info__text"));
            }

            for(int i = 0; i < explored.Count; i++)
            {
                section.Add(GroupRow(explored[i], steps, i == 0, i == explored.Count - 1 && explored.Count > 1, decimals));
            }
            return section;
        }

        private VisualElement GroupRow(OrderGroup group, IDamageStep[] steps, bool lowest, bool highest, int decimals)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-slot");
            row.AddToClassList("cg-group-row");
            BlockCategories.Apply(row, BlockCategory.DamageStep);
            row.EnableInClassList("cg-group-row--current", group.Current);

            row.Add(CombatEditorStyles.Text(CombatEditorStyles.Number(group.Mean, decimals), "cg-group-row__value"));
            if(lowest) row.Add(CombatEditorStyles.Chip("Lowest"));
            if(highest) row.Add(CombatEditorStyles.Chip("Highest"));
            if(group.Current) row.Add(CombatEditorStyles.Chip("Current", "cg-chip--current"));
            var sample = group.Orders[0];
            row.Add(CombatEditorStyles.Text(string.Join(" → ", sample.Select(i => BlockTypes.DisplayName(steps[i]?.GetType()))), "cg-slot__summary"));
            row.Add(CombatEditorStyles.Text(group.Orders.Count == 1 ? "1 order" : $"{group.Orders.Count} orders", "cg-group-row__count"));
            var use = new Button(() => UseOrder(sample)) { text = "Use" };
            use.AddToClassList("cg-add-button");
            use.SetEnabled(!group.Current);
            row.Add(use);
            row.tooltip = string.Join("\n", group.Orders.Take(12).Select(each => string.Join(" → ", each.Select(i => BlockTypes.DisplayName(steps[i]?.GetType())))))
                + (group.Orders.Count > 12 ? $"\n...and {group.Orders.Count - 12} more" : string.Empty);
            return row;
        }

        private static VisualElement Tile(string label, string value)
        {
            var tile = new VisualElement();
            tile.AddToClassList("cg-tile-stat");
            tile.Add(CombatEditorStyles.Text(value, "cg-tile-stat__value"));
            tile.Add(CombatEditorStyles.Text(label, "cg-tile-stat__label"));
            return tile;
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private void MoveStep(string path, int to)
        {
            BlockEdits.MoveTo(serializedWindow, path, to);
            Refresh();
        }

        private void SetMuted(int index, bool off)
        {
            Undo.RecordObject(this, off ? "Turn Off Step" : "Turn On Step");
            if(off && !muted.Contains(index)) muted.Add(index);
            if(!off) muted.Remove(index);
            Refresh();
        }

        private void ResetOrder()
        {
            Undo.RecordObject(this, "Reset Step Order");
            for(int i = 0; i < order.Count; i++)
            {
                order[i] = i;
            }
            muted.Clear();
            Refresh();
        }

        private void UseOrder(int[] chosen)
        {
            Undo.RecordObject(this, "Use Step Order");
            var rest = order.Where(i => !chosen.Contains(i)).ToList();
            order.Clear();
            order.AddRange(chosen);
            order.AddRange(rest);
            foreach(var group in explored ?? new List<OrderGroup>())
            {
                group.Current = group.Orders.Any(each => each.SequenceEqual(chosen));
            }
            Refresh();
        }

        private void ApplyOrder(DamagePipeline pipeline)
        {
            if(pipeline == null || pipeline.Steps == null) return;
            Undo.RecordObject(pipeline, "Reorder Pipeline Steps");
            Undo.RecordObject(this, "Reorder Pipeline Steps");
            var steps = pipeline.Steps;
            pipeline.Steps = order.Select(i => steps[i]).ToArray();
            EditorUtility.SetDirty(pipeline);
            var remapped = muted.Select(i => order.IndexOf(i)).Where(i => i >= 0).ToList();
            muted.Clear();
            muted.AddRange(remapped);
            for(int i = 0; i < order.Count; i++)
            {
                order[i] = i;
            }
            explored = null;
            Refresh();
        }

        private void Explore()
        {
            var pipeline = settings.EffectivePipeline;
            if(pipeline == null || simulation == null) return;
            var steps = pipeline.Steps ?? Array.Empty<IDamageStep>();
            SyncOrder(pipeline);
            int active = order.Count(i => !muted.Contains(i) && steps[i] != null);
            if(active < 2 || active > PipelineSimulation.k_MaxExploreSteps) return;
            simulation.Load(settings);
            explored = simulation.Explore(steps, order, muted, settings.Rolls, Mathf.Min(settings.Samples, k_ExploreSamples), settings.Seed);
            exploredKey = ExploreKey();
            if(!StudioPrefs.Get(k_Explorer, true)) StudioPrefs.Set(k_Explorer, true);
            Recalculate();
        }

        private string ExploreKey() => JsonUtility.ToJson(settings) + "|" + string.Join(",", muted) + "|" + Signature();

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Show"));
            StudioPrefs.Toggle(menu, "Step Bars", k_Bars, true);
            StudioPrefs.Toggle(menu, "Step Summaries", k_Summaries, true);
            StudioPrefs.Toggle(menu, "Roll Details", k_Rolls, true);
            StudioPrefs.Toggle(menu, "Health Preview", k_Health, true);
            StudioPrefs.Toggle(menu, "Distribution", k_Distribution, true);
            StudioPrefs.Toggle(menu, "Order Explorer", k_Explorer, true);
            menu.AddSeparator(string.Empty);
            for(int i = 0; i <= 3; i++)
            {
                StudioPrefs.Choice(menu, $"Decimals/{i}", k_Decimals, i, 1);
            }
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Live Updates", k_Live, true);
            StudioPrefs.Behavior(menu);
            menu.DropDown(rect);
        }
    }
}
