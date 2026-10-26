using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public sealed class CombatMonitorWindow : EditorWindow
    {
        public const string k_Title = "Combat Monitor";
        private const string k_Kinds = CombatLog.k_Kinds;
        private const string k_Stopped = "stopped";
        private const string k_SelectedOnly = "Monitor.SelectedOnly";
        private const string k_Dead = "Monitor.Dead";
        private const string k_Stats = "Monitor.Stats";
        private const string k_Loadout = "Monitor.Loadout";
        private const string k_Actions = "Monitor.Actions";
        private const string k_Newest = "Monitor.Newest";
        private const long k_RefreshMs = 200;
        private const float k_DpsWindow = 5f;

        private readonly CombatLog log = CombatLog.Shared;
        private readonly List<IUnit> units = new();
        private readonly List<IUnit> chosen = new();
        private readonly List<LogEntry> shown = new();
        private readonly object pauseKey = new();
        private ToolbarSearchField search;
        private ToolbarToggle pauseLog;
        private ToolbarToggle pauseCombat;
        private ListView unitList;
        private ListView logList;
        private VisualElement detail;
        private VisualElement placeholder;
        private VisualElement body;
        private IUnit selected;
        private bool syncing;
        private string detailFor;
        private string effectsSignature;
        private string tagsSignature;
        private int logVersion = -1;
        private float damageAmount = 10f;
        private DamageType damageType;
        private float healAmount = 25f;
        private StatusEffectDefinition effect;
        private int effectStacks = 1;
        private CombatTag tag;
        private Unit attacker;
        private readonly List<(VisualElement fill, Label label, IReadonlyResource resource)> bars = new();
        private readonly List<(VisualElement fill, Label stacks, StatusEffectInstance instance)> effectRows = new();
        private readonly List<(Label value, IStat stat)> statRows = new();
        private readonly List<(Label value, Firable firable)> loadoutRows = new();
        private Label stateLabel;
        private Label dpsLabel;
        private Label groupLabel;

        [MenuItem(CombatTools.k_Menu + k_Title, false, 22)]
        public static void Open()
        {
            var window = GetWindow<CombatMonitorWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            StudioPrefs.Changed += OnPrefsChanged;
            log.AddViewer();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            StudioPrefs.Changed -= OnPrefsChanged;
            log.RemoveViewer();
            if(EditorApplication.isPlaying) CombatServices.Unpause(pauseKey);
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatTools.Apply(root, "cg-monitor");

            var toolbar = CombatTools.Toolbar(k_Title);
            pauseLog = new ToolbarToggle { text = "Pause Log", tooltip = "Stop adding new entries", value = log.Paused };
            pauseLog.RegisterValueChangedCallback(evt => log.Paused = evt.newValue);
            toolbar.Add(pauseLog);
            toolbar.Add(new ToolbarButton(() =>
            {
                log.Clear();
                RefreshLog();
            }) { text = "Clear Log" });
            toolbar.Add(new ToolbarButton(CopyLog) { text = "Copy Log" });
            pauseCombat = new ToolbarToggle { text = "Pause Combat", tooltip = "Pause combat timers and firing through CombatServices" };
            pauseCombat.RegisterValueChangedCallback(evt =>
            {
                if(!EditorApplication.isPlaying) return;
                if(evt.newValue) CombatServices.Pause(pauseKey);
                else CombatServices.Unpause(pauseKey);
            });
            toolbar.Add(pauseCombat);
            toolbar.Add(new ToolbarSpacer { flex = true });
            search = new ToolbarSearchField { tooltip = "Filter units by name, faction or tag" };
            search.AddToClassList("cg-toolbar__search");
            search.RegisterValueChangedCallback(_ => RefreshUnits());
            toolbar.Add(search);
            CombatTools.Finish(toolbar, ShowViewMenu);
            root.Add(toolbar);

            placeholder = new VisualElement();
            placeholder.AddToClassList("cg-outline-empty");
            placeholder.Add(CombatEditorStyles.Text("Not playing", "cg-outline-empty__title"));
            placeholder.Add(CombatEditorStyles.Text("Enter play mode to watch units, status effects and every hit live", "cg-outline-empty__text"));
            var play = new Button(EditorApplication.EnterPlaymode) { text = "Enter Play Mode" };
            play.AddToClassList("cg-outline-empty__create");
            placeholder.Add(play);
            root.Add(placeholder);

            body = new TwoPaneSplitView(0, 240f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "cg-monitor-split" };
            body.AddToClassList("cg-studio__split");
            body.Add(BuildUnits());
            var right = new TwoPaneSplitView(1, 220f, TwoPaneSplitViewOrientation.Vertical) { viewDataKey = "cg-monitor-right" };
            var detailScroll = new ScrollView(ScrollViewMode.Vertical);
            detailScroll.AddToClassList("cg-detail__scroll");
            detailScroll.contentContainer.AddToClassList("cg-detail__content");
            detail = detailScroll.contentContainer;
            right.Add(detailScroll);
            right.Add(BuildLog());
            body.Add(right);
            root.Add(body);

            root.schedule.Execute(Refresh).Every(k_RefreshMs);
            Refresh();
        }

        private VisualElement BuildUnits()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            var header = CombatEditorStyles.Header("Live Units");
            header.Add(CombatEditorStyles.DropButton("Select", ShowSelectMenu, "Pick a group of units. Ctrl and Shift click work too"));
            pane.Add(header);
            unitList = new ListView
            {
                fixedItemHeight = 34f,
                selectionType = SelectionType.Multiple,
                itemsSource = units,
                makeItem = MakeUnitRow,
                bindItem = BindUnitRow
            };
            unitList.AddToClassList("cg-asset-list");
            unitList.selectionChanged += selection =>
            {
                if(syncing) return;
                Choose(selection.OfType<IUnit>().ToList(), true);
            };
            pane.Add(unitList);
            return pane;
        }

        private VisualElement BuildLog()
        {
            var pane = new VisualElement();
            pane.AddToClassList("cg-pane");
            var header = CombatEditorStyles.Header("Combat Log");
            header.Add(CombatEditorStyles.DropButton("Show", ShowLogMenu, "Which events to list"));
            pane.Add(header);
            logList = new ListView
            {
                fixedItemHeight = 18f,
                selectionType = SelectionType.Single,
                itemsSource = shown,
                makeItem = () =>
                {
                    var row = new VisualElement();
                    row.AddToClassList("cg-log-row");
                    row.Add(CombatEditorStyles.Text(null, "cg-log-row__time"));
                    row.Add(CombatEditorStyles.Text(null, "cg-log-row__text"));
                    return row;
                },
                bindItem = (row, index) =>
                {
                    var entry = shown[index];
                    foreach(LogKind kind in Enum.GetValues(typeof(LogKind)))
                    {
                        row.EnableInClassList("cg-log-row--" + kind.ToString().ToLowerInvariant(), kind == entry.Kind);
                    }
                    row.EnableInClassList("cg-log-row--crit", entry.Crit);
                    ((Label)row[0]).text = $"{entry.Time:0.00}";
                    ((Label)row[1]).text = entry.Text;
                }
            };
            logList.AddToClassList("cg-monitor__log");
            logList.selectionChanged += selection =>
            {
                if(!(selection.FirstOrDefault() is LogEntry entry)) return;
                var unit = entry.Target ?? entry.Source;
                if(EditorApplication.isPlaying && unit != null && unit.IsSpawned) Select(unit);
            };
            pane.Add(logList);
            return pane;
        }

        private VisualElement MakeUnitRow()
        {
            var row = new VisualElement();
            row.AddToClassList("cg-monitor__unit");
            var swatch = new VisualElement();
            swatch.AddToClassList("cg-monitor__swatch");
            row.Add(swatch);
            var texts = new VisualElement();
            texts.AddToClassList("cg-asset-row__texts");
            texts.Add(CombatEditorStyles.Text(null, "cg-asset-row__name"));
            var bar = new VisualElement();
            bar.AddToClassList("cg-monitor__bar");
            var fill = new VisualElement();
            fill.AddToClassList("cg-monitor__fill");
            bar.Add(fill);
            texts.Add(bar);
            row.Add(texts);
            row.Add(CombatEditorStyles.Text(null, "cg-monitor__effects"));
            return row;
        }

        private void BindUnitRow(VisualElement row, int index)
        {
            var unit = units[index];
            var faction = unit.Faction;
            row[0].style.backgroundColor = faction != null ? faction.Color : new Color(0.5f, 0.5f, 0.5f);
            row.EnableInClassList("cg-monitor__unit--dead", !unit.IsAlive);
            ((Label)row[1][0]).text = unit.IsAlive ? unit.DisplayName : $"{unit.DisplayName} (dead)";
            var fill = row[1][1][0];
            float normalized = unit.TryGet<IHealth>(out var health) && health.Resource != null ? health.Resource.Normalized : 0f;
            fill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
            if(health?.Resource?.Type != null) fill.style.backgroundColor = health.Resource.Type.Color;
            int count = unit.TryGet<IStatusEffects>(out var effects) ? effects.Active.Count : 0;
            CombatEditorStyles.SetText(row[2], count > 0 ? count.ToString() : null);
            row[2].tooltip = count > 0 ? string.Join("\n", effects.Active.Select(e => $"{StudioKinds.NameOf(e.Definition)} ×{e.Stacks}")) : null;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change == PlayModeStateChange.ExitingPlayMode)
            {
                units.Clear();
                unitList?.Rebuild();
                selected = null;
                chosen.Clear();
                detailFor = null;
                if(pauseCombat != null) pauseCombat.SetValueWithoutNotify(false);
            }
            Refresh();
        }

        private void OnPrefsChanged()
        {
            logVersion = -1;
            detailFor = null;
            Refresh();
        }

        private void Refresh()
        {
            if(body == null) return;
            bool playing = EditorApplication.isPlaying;
            bool history = !playing && log.Entries.Count > 0;
            CombatEditorStyles.Show(placeholder, !playing && !history);
            CombatEditorStyles.Show(body, playing || history);
            if(!playing)
            {
                if(!history) return;
                RefreshDetail();
                RefreshLog();
                return;
            }
            log.Attach();
            Prune();
            RefreshUnits();
            RefreshDetail();
            RefreshLog();
        }

        private void RefreshUnits()
        {
            if(!EditorApplication.isPlaying || unitList == null) return;
            var terms = StudioIndex.Terms(search != null ? search.value : null);
            var before = units.ToList();
            units.Clear();
            foreach(var unit in CombatServices.Units.All)
            {
                if(unit == null) continue;
                if(!unit.IsAlive && !StudioPrefs.Get(k_Dead, true)) continue;
                if(terms.Length > 0 && !StudioIndex.Matches(SearchText(unit), terms)) continue;
                units.Add(unit);
            }
            syncing = true;
            if(before.SequenceEqual(units)) unitList.RefreshItems();
            else unitList.Rebuild();
            syncing = false;
            SyncSelection();
        }

        private void SyncSelection()
        {
            if(unitList == null) return;
            syncing = true;
            unitList.SetSelectionWithoutNotify(chosen.Select(u => units.IndexOf(u)).Where(i => i >= 0).ToList());
            syncing = false;
        }

        private static bool Present(IUnit unit) => unit != null && unit.IsSpawned && CombatServices.Units.All.Contains(unit);

        private void Prune()
        {
            chosen.RemoveAll(unit => !Present(unit));
            if(selected != null && !Present(selected) && chosen.Count > 0) selected = chosen[chosen.Count - 1];
        }

        private void Choose(List<IUnit> picked, bool focusNewest)
        {
            var added = picked.Where(u => !chosen.Contains(u)).ToList();
            chosen.Clear();
            chosen.AddRange(picked);
            if(chosen.Count == 0) selected = null;
            else if(focusNewest && added.Count > 0) selected = added[added.Count - 1];
            else if(selected == null || !chosen.Contains(selected)) selected = chosen[0];
            detailFor = null;
            RefreshDetail();
            RefreshLog();
        }

        private void FocusOn(IUnit unit)
        {
            selected = unit;
            detailFor = null;
            RefreshDetail();
        }

        private List<IUnit> Targets => chosen.Count > 0 ? chosen.ToList() : selected != null ? new List<IUnit> { selected } : new List<IUnit>();

        private void ForEach<T>(Action<IUnit, T> action) where T : class
        {
            foreach(var unit in Targets)
            {
                if(Present(unit) && unit.TryGet<T>(out var part)) Run(() => action(unit, part));
            }
        }

        private void ShowSelectMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("All"), false, () => Pick(units));
            menu.AddItem(new GUIContent("Alive"), false, () => Pick(units.Where(u => u.IsAlive)));
            menu.AddItem(new GUIContent("Dead"), false, () => Pick(units.Where(u => !u.IsAlive)));
            foreach(var faction in units.Select(u => u.Faction).Distinct().ToList())
            {
                var each = faction;
                menu.AddItem(new GUIContent("By Faction/" + (each != null ? StudioKinds.NameOf(each) : "No Faction")), false, () => Pick(units.Where(u => u.Faction == each)));
            }
            if(selected != null && Present(selected) && selected.Faction != null)
            {
                var faction = selected.Faction;
                menu.AddItem(new GUIContent($"Same Faction as {selected.DisplayName}"), false, () => Pick(units.Where(u => u.Faction == faction)));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("None"), false, () => Pick(Array.Empty<IUnit>()));
            menu.DropDown(rect);
        }

        private void Pick(IEnumerable<IUnit> picked)
        {
            Choose(picked.ToList(), false);
            SyncSelection();
        }

        private static string SearchText(IUnit unit)
        {
            var parts = new List<string> { unit.DisplayName };
            if(unit.Faction != null) parts.Add(StudioKinds.NameOf(unit.Faction));
            parts.AddRange(unit.Tags.All.Select(t => StudioKinds.NameOf(t)));
            return string.Join(" ", parts).ToLowerInvariant();
        }

        private void Select(IUnit unit)
        {
            Choose(new List<IUnit> { unit }, true);
            SyncSelection();
        }

        private void RefreshDetail()
        {
            if(detail == null) return;
            if(!EditorApplication.isPlaying)
            {
                if(detailFor == k_Stopped) return;
                detailFor = k_Stopped;
                ResetDetail();
                detail.Add(CombatEditorStyles.Text("Not playing. The log below is from the last play session", "cg-placeholder"));
                var play = new Button(EditorApplication.EnterPlaymode) { text = "Enter Play Mode" };
                play.AddToClassList("cg-outline-empty__create");
                detail.Add(play);
                return;
            }
            bool gone = selected != null && !Present(selected);
            string key = selected == null ? "none" : gone ? "gone" : $"{selected.Id}|{string.Join(",", chosen.Select(u => u.Id))}";
            if(key != detailFor)
            {
                detailFor = key;
                BuildDetail(gone);
                return;
            }
            if(selected == null || gone) return;
            UpdateDetail();
        }

        private void ResetDetail()
        {
            detail.Clear();
            bars.Clear();
            effectRows.Clear();
            statRows.Clear();
            loadoutRows.Clear();
            effectsSignature = null;
            tagsSignature = null;
            groupLabel = null;
        }

        private void BuildDetail(bool gone)
        {
            ResetDetail();
            if(selected == null)
            {
                detail.Add(CombatEditorStyles.Text("Pick a unit to see it live", "cg-placeholder"));
                return;
            }
            if(gone)
            {
                detail.Add(CombatEditorStyles.Text($"{selected.DisplayName} has despawned", "cg-placeholder"));
                return;
            }

            var unit = selected;
            if(chosen.Count > 1) detail.Add(GroupCard());
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-kind-unit");
            if(unit.Faction != null) card.style.borderLeftColor = unit.Faction.Color;
            card.Add(CombatEditorStyles.Text($"{(chosen.Count > 1 ? "Focused · " : string.Empty)}{unit.Id} · {(unit.Faction != null ? StudioKinds.NameOf(unit.Faction) : "No faction")}".ToUpperInvariant(), "cg-card__chip"));
            var titleRow = new VisualElement();
            titleRow.AddToClassList("cg-card__title-row");
            titleRow.Add(CombatEditorStyles.Text(unit.DisplayName, "cg-card__title"));
            card.Add(titleRow);
            stateLabel = CombatEditorStyles.Text(null, "cg-card__summary");
            card.Add(stateLabel);
            dpsLabel = CombatEditorStyles.Text(null, "cg-card__summary");
            card.Add(dpsLabel);
            var actions = new VisualElement();
            actions.AddToClassList("cg-chips");
            actions.Add(CombatTools.Small("Select", () =>
            {
                if(unit is Component component) Selection.activeGameObject = component.gameObject;
            }));
            actions.Add(CombatTools.Small("Frame", () =>
            {
                if(!(unit is Component component)) return;
                Selection.activeGameObject = component.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }));
            if(unit.TryGet<IHealth>(out var health))
            {
                actions.Add(CombatTools.Small("Kill", () => Run(() => health.Kill(new EffectContext(Attacker, null, unit)))));
                actions.Add(CombatTools.Small("Heal Full", () => Run(() => health.ApplyHeal(health.Resource != null ? health.Resource.Max : 0f, new EffectContext(Attacker, null, unit)))));
                actions.Add(CombatTools.Small("Revive", () => Run(() => health.Revive())));
            }
            card.Add(actions);
            detail.Add(card);

            if(unit.TryGet<IResourceProvider>(out var provider))
            {
                var section = Section("Resources");
                foreach(var resource in provider.All)
                {
                    var row = new VisualElement();
                    row.AddToClassList("cg-monitor__resource");
                    var name = CombatEditorStyles.Text(StudioKinds.NameOf(resource.Type), "cg-monitor__resource-name");
                    row.Add(name);
                    var bar = new VisualElement();
                    bar.AddToClassList("cg-monitor__bar");
                    bar.AddToClassList("cg-monitor__bar--large");
                    var fill = new VisualElement();
                    fill.AddToClassList("cg-monitor__fill");
                    if(resource.Type != null) fill.style.backgroundColor = resource.Type.Color;
                    bar.Add(fill);
                    row.Add(bar);
                    var value = CombatEditorStyles.Text(null, "cg-monitor__resource-value");
                    row.Add(value);
                    section.Add(row);
                    bars.Add((fill, value, resource));
                }
                detail.Add(section);
            }

            var effects = Section("Status Effects");
            effects.name = "effects";
            detail.Add(effects);
            var tags = Section("Tags");
            tags.name = "tags";
            detail.Add(tags);

            if(StudioPrefs.Get(k_Stats, true) && unit is Component host && unit.TryGet<IStatProvider>(out var stats))
            {
                var profile = UnitLibrary.Read<UnitStatProfile>(host.GetComponent<StatsComponent>(), "profile");
                if(profile != null)
                {
                    var section = Section("Stats");
                    foreach(var entry in profile.Stats ?? Array.Empty<StatEntry>())
                    {
                        if(entry.Type == null || !stats.TryGetStat(entry.Type, out var stat)) continue;
                        var row = new VisualElement();
                        row.AddToClassList("cg-monitor__stat");
                        row.Add(CombatEditorStyles.Text(StudioKinds.NameOf(entry.Type), "cg-monitor__stat-name"));
                        var value = CombatEditorStyles.Text(null, "cg-monitor__stat-value");
                        row.Add(value);
                        section.Add(row);
                        statRows.Add((value, stat));
                    }
                    detail.Add(section);
                }
            }

            if(StudioPrefs.Get(k_Loadout, true) && unit.TryGet<FirableLoadout>(out var loadout) && loadout.Equipped.Count > 0)
            {
                var section = Section("Loadout");
                foreach(var pair in loadout.Equipped)
                {
                    var row = new VisualElement();
                    row.AddToClassList("cg-monitor__stat");
                    row.Add(CombatEditorStyles.Text($"{StudioKinds.NameOf(pair.Key)}: {StudioKinds.NameOf(pair.Value.Definition)}", "cg-monitor__stat-name"));
                    var value = CombatEditorStyles.Text(null, "cg-monitor__stat-value");
                    row.Add(value);
                    section.Add(row);
                    loadoutRows.Add((value, pair.Value));
                }
                detail.Add(section);
            }

            if(StudioPrefs.Get(k_Actions, true)) detail.Add(DebugSection());
            UpdateDetail();
        }

        private VisualElement GroupCard()
        {
            var card = new VisualElement();
            card.AddToClassList("cg-card");
            card.AddToClassList("cg-kind-unit");
            card.Add(CombatEditorStyles.Text($"{chosen.Count} units selected".ToUpperInvariant(), "cg-card__chip"));
            groupLabel = CombatEditorStyles.Text(null, "cg-card__summary");
            card.Add(groupLabel);
            var members = new VisualElement();
            members.AddToClassList("cg-chips");
            foreach(var member in chosen)
            {
                var each = member;
                var chip = new Button(() => FocusOn(each)) { text = each.DisplayName, tooltip = "Show this unit below" };
                chip.AddToClassList("cg-chip");
                chip.AddToClassList("cg-monitor__member");
                chip.EnableInClassList("cg-monitor__member--focused", ReferenceEquals(each, selected));
                chip.EnableInClassList("cg-monitor__member--dead", !each.IsAlive);
                if(each.Faction != null) chip.style.borderLeftColor = each.Faction.Color;
                members.Add(chip);
            }
            card.Add(members);
            var actions = new VisualElement();
            actions.AddToClassList("cg-chips");
            actions.Add(CombatTools.Small("Select in Scene", () => Selection.objects = chosen.OfType<Component>().Select(c => (Object)c.gameObject).ToArray()));
            actions.Add(CombatTools.Small("Kill All", () => ForEach<IHealth>((each, health) => health.Kill(new EffectContext(Attacker, null, each)))));
            actions.Add(CombatTools.Small("Heal All", () => ForEach<IHealth>((each, health) => health.ApplyHeal(health.Resource != null ? health.Resource.Max : 0f, new EffectContext(Attacker, null, each)))));
            actions.Add(CombatTools.Small("Revive All", () => ForEach<IHealth>((_, health) => health.Revive())));
            actions.Add(CombatTools.Small("Clear", () => Pick(Array.Empty<IUnit>())));
            card.Add(actions);
            return card;
        }

        private VisualElement DebugSection()
        {
            var section = Section(chosen.Count > 1 ? $"Debug · acting on {chosen.Count} units" : "Debug");
            if(chosen.Count > 1) section[0].tooltip = string.Join("\n", chosen.Select(u => u.DisplayName));
            var attackerField = new ObjectField("Acting As") { objectType = typeof(Unit), allowSceneObjects = true, value = attacker, tooltip = "Optional unit that gets the credit, so faction rules and on-hit triggers apply" };
            attackerField.RegisterValueChangedCallback(evt => attacker = evt.newValue as Unit);
            section.Add(attackerField);

            var damage = new VisualElement();
            damage.AddToClassList("cg-field-row");
            var amount = new FloatField("Damage") { value = damageAmount };
            amount.RegisterValueChangedCallback(evt => damageAmount = evt.newValue);
            damage.Add(amount);
            var type = new ObjectField { objectType = typeof(DamageType), allowSceneObjects = false, value = damageType };
            type.RegisterValueChangedCallback(evt => damageType = evt.newValue as DamageType);
            damage.Add(type);
            damage.Add(CombatTools.Small("Deal", () => ForEach<IHealth>((each, health) =>
            {
                var context = new EffectContext(Attacker, null, each);
                health.ApplyDamage(new DamageRequest(damageAmount, damageType, in context, DamageFlags.None));
            })));
            section.Add(damage);

            var heal = new VisualElement();
            heal.AddToClassList("cg-field-row");
            var healField = new FloatField("Heal") { value = healAmount };
            healField.RegisterValueChangedCallback(evt => healAmount = evt.newValue);
            heal.Add(healField);
            heal.Add(CombatTools.Small("Heal", () => ForEach<IHealth>((each, health) => health.ApplyHeal(healAmount, new EffectContext(Attacker, null, each)))));
            section.Add(heal);

            var apply = new VisualElement();
            apply.AddToClassList("cg-field-row");
            var effectField = new ObjectField("Effect") { objectType = typeof(StatusEffectDefinition), allowSceneObjects = false, value = effect };
            effectField.RegisterValueChangedCallback(evt => effect = evt.newValue as StatusEffectDefinition);
            apply.Add(effectField);
            var stacks = new IntegerField { value = effectStacks };
            stacks.AddToClassList("cg-monitor__stacks");
            stacks.RegisterValueChangedCallback(evt => effectStacks = Mathf.Max(1, evt.newValue));
            apply.Add(stacks);
            apply.Add(CombatTools.Small("Apply", () =>
            {
                if(effect != null) ForEach<IStatusEffects>((each, effects) => effects.Apply(effect, Attacker ?? each, effectStacks));
            }));
            section.Add(apply);

            var tagRow = new VisualElement();
            tagRow.AddToClassList("cg-field-row");
            var tagField = new ObjectField("Tag") { objectType = typeof(CombatTag), allowSceneObjects = false, value = tag };
            tagField.RegisterValueChangedCallback(evt => tag = evt.newValue as CombatTag);
            tagRow.Add(tagField);
            tagRow.Add(CombatTools.Small("Add", () => EachTag(tags => tags.Add(tag))));
            tagRow.Add(CombatTools.Small("Remove", () => EachTag(tags => tags.Remove(tag))));
            section.Add(tagRow);
            return section;
        }

        private void EachTag(Action<TagContainer> action)
        {
            if(tag == null) return;
            foreach(var unit in Targets)
            {
                if(Present(unit)) Run(() => action(unit.Tags));
            }
        }

        private IUnit Attacker => attacker != null && attacker.IsSpawned ? attacker : null;

        private void UpdateGroup()
        {
            if(groupLabel == null) return;
            int alive = chosen.Count(u => u.IsAlive);
            float current = 0f;
            float max = 0f;
            foreach(var member in chosen)
            {
                if(!member.TryGet<IHealth>(out var health) || health.Resource == null) continue;
                current += health.Resource.Current;
                max += health.Resource.Max;
            }
            float dealt = chosen.Sum(u => log.Sum(u, true, k_DpsWindow)) / k_DpsWindow;
            float took = chosen.Sum(u => log.Sum(u, false, k_DpsWindow)) / k_DpsWindow;
            groupLabel.text = $"{alive} alive · {chosen.Count - alive} dead · {current:0.#}/{max:0.#} health · together dealt {dealt:0.#}/s · took {took:0.#}/s";
        }

        private void UpdateDetail()
        {
            UpdateGroup();
            var unit = selected;
            var health = unit.TryGet<IHealth>(out var found) ? found : null;
            var position = unit.Transform != null ? unit.Transform.position : Vector3.zero;
            stateLabel.text = $"{(unit.IsAlive ? "Alive" : "Dead")} · at ({position.x:0.0}, {position.y:0.0}, {position.z:0.0})" + (health?.Resource != null ? $" · {health.Resource.Current:0.#}/{health.Resource.Max:0.#} {StudioKinds.NameOf(health.Resource.Type)}" : string.Empty);
            dpsLabel.text = $"Last {k_DpsWindow:0}s: dealt {log.Sum(unit, true, k_DpsWindow) / k_DpsWindow:0.#}/s · took {log.Sum(unit, false, k_DpsWindow) / k_DpsWindow:0.#}/s";

            foreach(var (fill, label, resource) in bars)
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(resource.Normalized) * 100f);
                label.text = $"{resource.Current:0.#} / {resource.Max:0.#}";
            }
            foreach(var (value, stat) in statRows)
            {
                value.text = Mathf.Approximately(stat.Value, stat.BaseValue) ? $"{stat.Value:0.##}" : $"{stat.BaseValue:0.##} → {stat.Value:0.##}";
                value.EnableInClassList("cg-monitor__stat-value--changed", !Mathf.Approximately(stat.Value, stat.BaseValue));
            }
            foreach(var (value, firable) in loadoutRows)
            {
                value.text = FirableState(firable);
            }

            var effectsSection = detail.Q("effects");
            var effects = unit.TryGet<IStatusEffects>(out var foundEffects) ? foundEffects.Active : null;
            string signature = effects == null ? "none" : string.Join("|", effects.Select(e => $"{e.GetHashCode()}:{(e.Definition != null ? e.Definition.GetHashCode() : 0)}"));
            if(effectsSection != null && signature != effectsSignature)
            {
                effectsSignature = signature;
                effectRows.Clear();
                while(effectsSection.childCount > 1)
                {
                    effectsSection.RemoveAt(1);
                }
                if(effects == null || effects.Count == 0) effectsSection.Add(CombatEditorStyles.Text("None", "cg-empty-note"));
                else
                {
                    foreach(var instance in effects)
                    {
                        effectsSection.Add(EffectRow(unit, foundEffects, instance));
                    }
                }
            }
            foreach(var (fill, stacks, instance) in effectRows)
            {
                fill.style.width = Length.Percent(instance.Remaining01 * 100f);
                stacks.text = instance.Definition != null && instance.Definition.IsPermanent ? $"×{instance.Stacks} · permanent" : $"×{instance.Stacks} · {instance.Remaining:0.0}s";
            }

            var tagsSection = detail.Q("tags");
            var tags = unit.Tags.All.ToList();
            string tagSignature = string.Join("|", tags.Select(t => t.GetHashCode()));
            if(tagsSection == null || tagSignature == tagsSignature) return;
            tagsSignature = tagSignature;
            while(tagsSection.childCount > 1)
            {
                tagsSection.RemoveAt(1);
            }
            var chips = new VisualElement();
            chips.AddToClassList("cg-chips");
            if(tags.Count == 0) chips.Add(CombatEditorStyles.Text("None", "cg-empty-note"));
            foreach(var each in tags)
            {
                var chip = new Button(() => unit.Tags.Remove(each)) { text = $"{StudioKinds.NameOf(each)} ×", tooltip = "Remove this tag" };
                chip.AddToClassList("cg-chip");
                chip.AddToClassList("cg-monitor__tag");
                chips.Add(chip);
            }
            tagsSection.Add(chips);
        }

        private VisualElement EffectRow(IUnit unit, IStatusEffects effects, StatusEffectInstance instance)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-monitor__effect");
            var top = new VisualElement();
            top.AddToClassList("cg-monitor__effect-top");
            var icon = new BlockIcon();
            icon.SetStyle("cg-kind-effect");
            top.Add(icon);
            top.Add(CombatEditorStyles.Text(StudioKinds.NameOf(instance.Definition), "cg-slot__title"));
            top.Add(CombatEditorStyles.Text($"from {CombatLog.NameOf(instance.SourceUnit)}", "cg-slot__summary"));
            var stacks = CombatEditorStyles.Text(null, "cg-monitor__effect-stacks");
            top.Add(stacks);
            var definition = instance.Definition;
            top.Add(CombatTools.Small("Open", () => CombatTools.Jump(definition)));
            top.Add(CombatTools.Small("Remove", () => Run(() => effects.Remove(new StatusEffectHandle(instance)))));
            row.Add(top);
            var bar = new VisualElement();
            bar.AddToClassList("cg-monitor__bar");
            var fill = new VisualElement();
            fill.AddToClassList("cg-monitor__fill");
            fill.AddToClassList("cg-monitor__fill--effect");
            bar.Add(fill);
            row.Add(bar);
            effectRows.Add((fill, stacks, instance));
            return row;
        }

        internal static string FirableState(Firable firable)
        {
            var parts = new List<string> { firable.Phase.ToString() };
            foreach(var gate in firable.Gates)
            {
                switch(gate)
                {
                    case IReloadable ammo:
                        parts.Add(ammo.IsReloading ? $"reloading {ammo.ReloadProgress01:P0}" : $"{ammo.Current}/{ammo.Capacity}");
                        break;
                    case ICooldown cooldown when cooldown.Remaining > 0f:
                        parts.Add($"cooldown {cooldown.Remaining:0.0}s");
                        break;
                    case IChargeCounter charges:
                        parts.Add($"{charges.Charges}/{charges.MaxCharges} charges");
                        break;
                }
            }
            if(!firable.IsReady) parts.Add("not ready");
            return string.Join(" · ", parts);
        }

        private VisualElement Section(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("cg-slot-list");
            var header = new VisualElement();
            header.AddToClassList("cg-slot-list__header");
            header.Add(CombatEditorStyles.Text(title, "cg-slot-list__title"));
            section.Add(header);
            return section;
        }

        private void RefreshLog()
        {
            if(logList == null || log.Version == logVersion && !StudioPrefs.Get(k_SelectedOnly, false)) return;
            logVersion = log.Version;
            var targets = Targets;
            bool selectedOnly = StudioPrefs.Get(k_SelectedOnly, false) && targets.Count > 0;
            var entries = log.Entries.Where(e => StudioPrefs.Get(k_Kinds + e.Kind, CombatLog.ShownByDefault(e.Kind)) && (!selectedOnly || targets.Contains(e.Target) || targets.Contains(e.Source)));
            if(StudioPrefs.Get(k_Newest, true)) entries = entries.Reverse();
            shown.Clear();
            shown.AddRange(entries);
            logList.RefreshItems();
            if(!StudioPrefs.Get(k_Newest, true) && shown.Count > 0) logList.ScrollToItem(shown.Count - 1);
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void CopyLog()
        {
            var text = new StringBuilder();
            foreach(var entry in log.Entries)
            {
                text.AppendLine($"{entry.Time:0.00}  {entry.Kind,-14} {entry.Text}");
            }
            EditorGUIUtility.systemCopyBuffer = text.ToString();
            ShowNotification(new GUIContent("Log copied"));
        }

        private void ShowLogMenu(Rect rect)
        {
            var menu = new GenericMenu();
            foreach(LogKind kind in Enum.GetValues(typeof(LogKind)))
            {
                StudioPrefs.Toggle(menu, ObjectNames.NicifyVariableName(kind.ToString()), k_Kinds + kind, CombatLog.ShownByDefault(kind));
            }
            foreach(int every in new[] { 250, 500, 1000, 2000 })
            {
                StudioPrefs.Choice(menu, $"Regen Tick Every/{every / 1000f:0.##}s", CombatLog.k_Step, every, 1000);
            }
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Selected Units Only", k_SelectedOnly, false);
            StudioPrefs.Toggle(menu, "Newest First", k_Newest, true);
            menu.DropDown(rect);
        }

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            StudioPrefs.Toggle(menu, "Dead Units", k_Dead, true);
            StudioPrefs.Toggle(menu, "Stats", k_Stats, true);
            StudioPrefs.Toggle(menu, "Loadout", k_Loadout, true);
            StudioPrefs.Toggle(menu, "Debug Actions", k_Actions, true);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Record While Closed", CombatLog.k_Background, true);
            foreach(int size in new[] { 1000, 2000, 5000, 10000 })
            {
                StudioPrefs.Choice(menu, $"History Size/{size} entries", CombatLog.k_Limit, size, 2000);
            }
            menu.AddSeparator(string.Empty);
            menu.AddDisabledItem(new GUIContent("Combat Log"));
            foreach(LogKind kind in Enum.GetValues(typeof(LogKind)))
            {
                StudioPrefs.Toggle(menu, "Combat Log/" + ObjectNames.NicifyVariableName(kind.ToString()), k_Kinds + kind, CombatLog.ShownByDefault(kind));
            }
            menu.DropDown(rect);
        }
    }
}
