using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    public sealed class CombatDoctorWindow : EditorWindow
    {
        public const string k_Title = "Combat Doctor";
        private const string k_Errors = "Doctor.Errors";
        private const string k_Warnings = "Doctor.Warnings";
        private const string k_Infos = "Doctor.Infos";
        private const string k_ByRule = "Doctor.ByRule";
        private const string k_Paths = "Doctor.Paths";
        private const string k_AutoScan = "Doctor.AutoScan";
        private const int k_MaxFixes = 500;

        [SerializeField] private List<Object> focus = new();

        private readonly List<Finding> findings = new();
        private ScrollView results;
        private Label status;
        private ToolbarButton fixAll;
        private ToolbarToggle errors;
        private ToolbarToggle warnings;
        private ToolbarToggle infos;
        private ToolbarSearchField search;
        private bool scanQueued;

        [MenuItem(CombatTools.k_Menu + k_Title, false, 20)]
        public static void Open()
        {
            var window = GetWindow<CombatDoctorWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.focus.Clear();
            window.Show();
            window.Scan();
        }

        public static void Open(Object asset)
        {
            var window = GetWindow<CombatDoctorWindow>();
            window.titleContent = new GUIContent(k_Title);
            window.focus.Clear();
            if(asset != null) window.focus.Add(asset);
            window.Show();
            window.Scan();
        }

        public static bool Covers(Object asset) => asset is GameDefinition || asset is DamagePipeline || UnitLibrary.IsUnit(asset);

        private void OnEnable()
        {
            StudioPrefs.Changed += Render;
            AssetChanges.Changed += OnAssetsChanged;
            AssetChanges.CreatedOrDeleted += OnAssetsChanged;
        }

        private void OnDisable()
        {
            StudioPrefs.Changed -= Render;
            AssetChanges.Changed -= OnAssetsChanged;
            AssetChanges.CreatedOrDeleted -= OnAssetsChanged;
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent(k_Title);
            var root = rootVisualElement;
            CombatTools.Apply(root, "cg-doctor");

            var toolbar = CombatTools.Toolbar(k_Title);
            toolbar.Add(new ToolbarButton(Scan) { text = "Scan", tooltip = "Check every combat asset again" });
            fixAll = new ToolbarButton(FixAllSafe) { tooltip = "Run every fix that only removes empty or duplicate entries" };
            toolbar.Add(fixAll);
            toolbar.Add(new ToolbarButton(CopyReport) { text = "Copy Report", tooltip = "Copy the findings as text" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            errors = Filter(k_Errors, "Errors");
            warnings = Filter(k_Warnings, "Warnings");
            infos = Filter(k_Infos, "Info");
            toolbar.Add(errors);
            toolbar.Add(warnings);
            toolbar.Add(infos);
            ToolbarButton rules = null;
            rules = new ToolbarButton(() => ShowRulesMenu(rules.worldBound)) { text = "Rules" };
            toolbar.Add(rules);
            search = new ToolbarSearchField { tooltip = "Filter findings by asset or message" };
            search.AddToClassList("cg-toolbar__search");
            search.RegisterValueChangedCallback(_ => Render());
            toolbar.Add(search);
            CombatTools.Finish(toolbar, ShowViewMenu);
            root.Add(toolbar);

            results = new ScrollView(ScrollViewMode.Vertical);
            results.AddToClassList("cg-detail__scroll");
            results.contentContainer.AddToClassList("cg-detail__content");
            root.Add(results);
            status = CombatEditorStyles.Text(null, "cg-db-status");
            root.Add(status);
            Scan();
        }

        private ToolbarToggle Filter(string key, string label)
        {
            var toggle = new ToolbarToggle { value = StudioPrefs.Get(key, true), text = label };
            toggle.AddToClassList("cg-doctor__filter");
            toggle.RegisterValueChangedCallback(evt => StudioPrefs.Set(key, evt.newValue));
            return toggle;
        }

        private void OnAssetsChanged(IReadOnlyCollection<string> guids)
        {
            if(results == null || scanQueued || !StudioPrefs.Get(k_AutoScan, true) || !AssetChanges.AnyCombat(guids)) return;
            scanQueued = true;
            results.schedule.Execute(() =>
            {
                scanQueued = false;
                Scan();
            }).ExecuteLater(1000);
        }

        private void Scan()
        {
            if(results == null) return;
            var watch = Stopwatch.StartNew();
            findings.Clear();
            var scan = new DoctorScan(focus);
            foreach(var rule in DoctorRules.All)
            {
                if(!rule.Enabled) continue;
                try
                {
                    rule.Check(scan, findings);
                }
                catch(System.Exception exception)
                {
                    findings.Add(new Finding(rule, Severity.Info, null, null, null, $"This check failed to run: {exception.Message}"));
                }
            }
            watch.Stop();
            int checkedCount = scan.Assets.Count + scan.Units.Count;
            status.text = $"Checked {checkedCount} assets in {watch.ElapsedMilliseconds} ms" + (focus.Count > 0 ? $" · only {string.Join(", ", focus.Where(f => f != null).Select(StudioKinds.NameOf))}" : string.Empty);
            Render();
        }

        private void Render()
        {
            if(results == null) return;
            errors.SetValueWithoutNotify(StudioPrefs.Get(k_Errors, true));
            warnings.SetValueWithoutNotify(StudioPrefs.Get(k_Warnings, true));
            infos.SetValueWithoutNotify(StudioPrefs.Get(k_Infos, true));
            errors.text = $"Errors {Count(Severity.Error)}";
            warnings.text = $"Warnings {Count(Severity.Warning)}";
            infos.text = $"Info {Count(Severity.Info)}";
            int safe = findings.Count(f => f.Safe && f.Fix != null);
            fixAll.text = safe > 0 ? $"Fix All Safe ({safe})" : "Fix All Safe";
            fixAll.SetEnabled(safe > 0);

            results.Clear();
            if(focus.Count > 0)
            {
                var banner = new VisualElement();
                banner.AddToClassList("cg-doctor__focus");
                banner.Add(CombatEditorStyles.Text($"Only checking {string.Join(", ", focus.Where(f => f != null).Select(StudioKinds.NameOf))}", "cg-doctor__focus-text"));
                banner.Add(CombatTools.Small("Check Everything", () =>
                {
                    focus.Clear();
                    Scan();
                }));
                results.Add(banner);
            }

            var shown = Visible().ToList();
            if(shown.Count == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList("cg-outline-empty");
                empty.Add(CombatEditorStyles.Text(findings.Count == 0 ? "All clear" : "Nothing matches these filters", "cg-outline-empty__title"));
                empty.Add(CombatEditorStyles.Text(findings.Count == 0 ? "No problems found in the checked assets" : "Turn on more severities or clear the search", "cg-outline-empty__text"));
                results.Add(empty);
                return;
            }

            bool byRule = StudioPrefs.Get(k_ByRule, false);
            var groups = byRule
                ? shown.GroupBy(f => (object)f.Rule).OrderBy(g => System.Array.IndexOf(DoctorRules.All, (DoctorRule)g.Key))
                : shown.GroupBy(f => (object)f.Asset).OrderBy(g => g.Min(f => (int)f.Severity)).ThenBy(g => g.Key is Object o ? StudioKinds.NameOf(o) : string.Empty);
            foreach(var group in groups)
            {
                results.Add(Group(group.Key, group.ToList(), byRule));
            }
        }

        private int Count(Severity severity) => findings.Count(f => f.Severity == severity);

        private IEnumerable<Finding> Visible()
        {
            var terms = StudioIndex.Terms(search != null ? search.value : null);
            foreach(var finding in findings)
            {
                if(finding.Severity == Severity.Error && !StudioPrefs.Get(k_Errors, true)) continue;
                if(finding.Severity == Severity.Warning && !StudioPrefs.Get(k_Warnings, true)) continue;
                if(finding.Severity == Severity.Info && !StudioPrefs.Get(k_Infos, true)) continue;
                if(terms.Length > 0 && !StudioIndex.Matches(SearchText(finding), terms)) continue;
                yield return finding;
            }
        }

        private static string SearchText(Finding finding)
        {
            return string.Join(" ", finding.Asset != null ? StudioKinds.NameOf(finding.Asset) : string.Empty, finding.Message, finding.Where, finding.Rule.Title).ToLowerInvariant();
        }

        private VisualElement Group(object key, List<Finding> items, bool byRule)
        {
            var group = new VisualElement();
            group.AddToClassList("cg-card");
            group.AddToClassList("cg-doctor__group");
            string title;
            string subtitle;
            if(byRule)
            {
                var rule = (DoctorRule)key;
                title = rule.Title;
                subtitle = rule.Description;
                group.AddToClassList("cg-kind-definition");
            }
            else
            {
                var asset = key as Object;
                title = asset != null ? StudioPrefs.LabelOf(asset) : "Project";
                subtitle = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
                group.AddToClassList(DatabaseCatalog.StyleOf(asset));
            }

            var header = new VisualElement();
            header.AddToClassList("cg-doctor__header");
            var icon = new BlockIcon();
            icon.SetStyle(byRule ? "cg-kind-definition" : DatabaseCatalog.StyleOf(key as Object));
            header.Add(icon);
            header.Add(CombatEditorStyles.Text(title, "cg-doctor__title"));
            header.Add(CombatEditorStyles.Chip(items.Count == 1 ? "1 finding" : $"{items.Count} findings"));
            if(!byRule && key is Object target)
            {
                header.Add(CombatTools.Small("Open", () => CombatTools.Jump(target)));
                header.Add(CombatTools.Small("Ping", () => EditorGUIUtility.PingObject(target)));
            }
            group.Add(header);
            if(!string.IsNullOrEmpty(subtitle)) group.Add(CombatEditorStyles.Text(subtitle, "cg-card__summary"));

            foreach(var finding in items.OrderBy(f => (int)f.Severity))
            {
                group.Add(Row(finding, byRule));
            }
            return group;
        }

        private VisualElement Row(Finding finding, bool byRule)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-doctor__row");
            row.AddToClassList("cg-doctor__row--" + finding.Severity.ToString().ToLowerInvariant());
            row.Add(new Image { image = CombatTools.SeverityIcon((int)finding.Severity), scaleMode = ScaleMode.ScaleToFit });

            var texts = new VisualElement();
            texts.AddToClassList("cg-doctor__texts");
            string prefix = byRule && finding.Asset != null ? StudioPrefs.LabelOf(finding.Asset) + ": " : string.Empty;
            texts.Add(CombatEditorStyles.Text(prefix + finding.Message, "cg-doctor__message"));
            string meta = StudioPrefs.Get(k_Paths, true) && !string.IsNullOrEmpty(finding.Where) ? finding.Where : null;
            if(!byRule) meta = meta == null ? finding.Rule.Title : $"{meta}  ·  {finding.Rule.Title}";
            if(meta != null) texts.Add(CombatEditorStyles.Text(meta, "cg-doctor__where"));
            row.Add(texts);

            if(finding.Fix != null)
            {
                var fix = finding.Fix;
                var button = CombatTools.Small(finding.FixLabel, () =>
                {
                    fix();
                    AssetDatabase.SaveAssets();
                    Scan();
                }, finding.Safe ? "Safe: only removes empty or duplicate entries" : "Changes the asset. Undo works for most fixes");
                button.AddToClassList(finding.Safe ? "cg-doctor__fix--safe" : "cg-doctor__fix");
                row.Add(button);
            }
            if(finding.Asset != null)
            {
                var asset = finding.Asset;
                string path = finding.Path;
                row.Add(CombatTools.Small("Go", () => CombatTools.Jump(asset, path), "Open where this is"));
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if(evt.clickCount == 2) CombatTools.Jump(asset, path);
                });
            }
            return row;
        }

        private void FixAllSafe()
        {
            int fixes = 0;
            while(fixes < k_MaxFixes)
            {
                var next = findings.FirstOrDefault(f => f.Safe && f.Fix != null);
                if(next == null) break;
                next.Fix();
                fixes++;
                Scan();
            }
            AssetDatabase.SaveAssets();
            Scan();
            ShowNotification(new GUIContent(fixes == 1 ? "Fixed 1 finding" : $"Fixed {fixes} findings"));
        }

        private void CopyReport()
        {
            var text = new StringBuilder();
            text.AppendLine($"{k_Title} report");
            foreach(var group in findings.GroupBy(f => f.Asset))
            {
                text.AppendLine();
                text.AppendLine(group.Key != null ? $"{StudioKinds.NameOf(group.Key)} ({AssetDatabase.GetAssetPath(group.Key)})" : "Project");
                foreach(var finding in group.OrderBy(f => (int)f.Severity))
                {
                    string where = string.IsNullOrEmpty(finding.Where) ? string.Empty : $" [{finding.Where}]";
                    text.AppendLine($"  {finding.Severity}: {finding.Message}{where}");
                }
            }
            EditorGUIUtility.systemCopyBuffer = text.ToString();
            ShowNotification(new GUIContent("Report copied"));
        }

        private void ShowRulesMenu(Rect rect)
        {
            var menu = new GenericMenu();
            foreach(var rule in DoctorRules.All)
            {
                var each = rule;
                menu.AddItem(new GUIContent(each.Title), each.Enabled, () =>
                {
                    StudioPrefs.Store(each.PrefKey, !each.Enabled);
                    Scan();
                });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Turn All On"), false, () =>
            {
                foreach(var rule in DoctorRules.All)
                {
                    StudioPrefs.Store(rule.PrefKey, true);
                }
                Scan();
            });
            menu.AddItem(new GUIContent("Reset to Defaults"), false, () =>
            {
                foreach(var rule in DoctorRules.All)
                {
                    StudioPrefs.Store(rule.PrefKey, rule.DefaultOn);
                }
                Scan();
            });
            menu.DropDown(rect);
        }

        private void ShowViewMenu(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent("Group By"));
            menu.AddItem(new GUIContent("Asset"), !StudioPrefs.Get(k_ByRule, false), () => StudioPrefs.Set(k_ByRule, false));
            menu.AddItem(new GUIContent("Rule"), StudioPrefs.Get(k_ByRule, false), () => StudioPrefs.Set(k_ByRule, true));
            menu.AddSeparator(string.Empty);
            StudioPrefs.Toggle(menu, "Show Where", k_Paths, true);
            StudioPrefs.Toggle(menu, "Rescan on Changes", k_AutoScan, true);
            menu.AddSeparator(string.Empty);
            StudioPrefs.Labels(menu);
            menu.DropDown(rect);
        }
    }
}
