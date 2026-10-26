using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    [Overlay(typeof(SceneView), k_Id, "Combat Ranges", false)]
    internal sealed class RangeOverlay : Overlay
    {
        public const string k_Id = "cg-combat-ranges";

        private static readonly List<string> scopes = new() { "Selected Units", "All Units" };
        private static readonly List<string> aims = new() { "Unit Aim", "Mouse" };

        private bool alive;
        private bool synced;

        public override void OnCreated()
        {
            alive = true;
            displayedChanged += OnDisplayedChanged;
            RangeGizmos.Changed += Follow;
            EditorApplication.delayCall += Follow;
        }

        public override void OnWillBeDestroyed()
        {
            alive = false;
            displayedChanged -= OnDisplayedChanged;
            RangeGizmos.Changed -= Follow;
        }

        private void OnDisplayedChanged(bool shown)
        {
            if(synced) RangeGizmos.SetOn(shown);
        }

        private void Follow()
        {
            if(!alive) return;
            if(displayed != RangeGizmos.On) displayed = RangeGizmos.On;
            synced = true;
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-range-overlay");

            var header = new VisualElement();
            header.AddToClassList("cg-range-overlay__header");
            var status = CombatEditorStyles.Text(null, "cg-range-overlay__status");
            header.Add(status);
            var showAll = CombatTools.Small("Show All", () => RangeGizmos.ShowAll(), "Unhide every firable on every unit and clear every Solo");
            header.Add(showAll);
            header.Add(CombatTools.Small("Turn Off", () => RangeGizmos.SetOn(false), "Stop drawing ranges. Turn them back on from Tools > CG Combat Framework > Range Gizmos or a unit's inspector"));
            root.Add(header);

            var scope = Choice("Units", scopes, RangeGizmos.k_Scope, "Which units to draw for");
            root.Add(scope);
            var aim = Choice("Aim", aims, RangeGizmos.k_Aim, "Unit Aim uses the unit's aim provider or forward. Mouse aims where the cursor points");
            root.Add(aim);

            var soloField = new ObjectField("Solo") { objectType = typeof(FirableDefinition), allowSceneObjects = false, tooltip = "Draw only this firable from each unit, even if it isn't equipped. Clear it to show the whole loadout" };
            soloField.RegisterValueChangedCallback(evt => RangeGizmos.SetSolo(evt.newValue as FirableDefinition));
            root.Add(soloField);

            var toggles = new List<(Toggle toggle, string key, bool fallback)>
            {
                (Toggle("Labels", RangeGizmos.k_Labels, true, "Names and distances"), RangeGizmos.k_Labels, true),
                (Toggle("Spread", RangeGizmos.k_Spread, true, "Raycast and projectile spread"), RangeGizmos.k_Spread, true),
                (Toggle("Projectile Paths", RangeGizmos.k_Paths, true, "Full flight path with gravity"), RangeGizmos.k_Paths, true),
                (Toggle("Hit Test", RangeGizmos.k_Hits, true, "Raycast against the scene to show what each shot would hit first"), RangeGizmos.k_Hits, true),
                (Toggle("Fill", RangeGizmos.k_Fill, true, "Shade cones and spheres"), RangeGizmos.k_Fill, true)
            };
            foreach(var (toggle, _, _) in toggles)
            {
                root.Add(toggle);
            }
            root.Add(CombatEditorStyles.Text("Select units in the scene. Hide or solo single firables from a unit's Firable Loadout inspector", "cg-note"));

            void Sync()
            {
                var only = RangeGizmos.Solo;
                status.text = RangeGizmos.Summary();
                CombatEditorStyles.Show(showAll, RangeGizmos.Filtered);
                scope.SetValueWithoutNotify(scopes[UnityEngine.Mathf.Clamp(StudioPrefs.GetInt(RangeGizmos.k_Scope, 0), 0, scopes.Count - 1)]);
                aim.SetValueWithoutNotify(aims[UnityEngine.Mathf.Clamp(StudioPrefs.GetInt(RangeGizmos.k_Aim, 0), 0, aims.Count - 1)]);
                soloField.SetValueWithoutNotify(only);
                foreach(var (toggle, key, fallback) in toggles)
                {
                    toggle.SetValueWithoutNotify(StudioPrefs.Get(key, fallback));
                }
            }

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                RangeGizmos.Changed += Sync;
                Selection.selectionChanged += Sync;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                RangeGizmos.Changed -= Sync;
                Selection.selectionChanged -= Sync;
            });
            Sync();
            return root;
        }

        private static Toggle Toggle(string label, string key, bool fallback, string tooltip)
        {
            var toggle = new Toggle(label) { value = StudioPrefs.Get(key, fallback), tooltip = tooltip };
            toggle.AddToClassList("cg-range-overlay__toggle");
            toggle.RegisterValueChangedCallback(evt => RangeGizmos.SetOption(key, evt.newValue));
            return toggle;
        }

        private static DropdownField Choice(string label, List<string> choices, string key, string tooltip)
        {
            var field = new DropdownField(label, choices, UnityEngine.Mathf.Clamp(StudioPrefs.GetInt(key, 0), 0, choices.Count - 1)) { tooltip = tooltip };
            field.RegisterValueChangedCallback(evt => RangeGizmos.SetChoice(key, choices.IndexOf(evt.newValue)));
            return field;
        }
    }
}
