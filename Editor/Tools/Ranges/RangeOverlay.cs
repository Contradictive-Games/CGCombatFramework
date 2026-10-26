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

        public static void Reveal()
        {
            foreach(SceneView view in SceneView.sceneViews)
            {
                if(!view.TryGetOverlay(k_Id, out var overlay)) continue;
                overlay.displayed = true;
                overlay.collapsed = false;
            }
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-range-overlay");

            root.Add(Toggle("Show Ranges", RangeGizmos.k_On, false, "Draw firable ranges in the Scene view"));
            root.Add(Choice("Units", scopes, RangeGizmos.k_Scope, "Which units to draw for"));
            root.Add(Choice("Aim", aims, RangeGizmos.k_Aim, "Unit Aim uses the unit's aim provider or forward. Mouse aims where the cursor points"));

            var preview = new ObjectField("Preview") { objectType = typeof(FirableDefinition), allowSceneObjects = false, value = RangeGizmos.Preview, tooltip = "Draw this firable instead of the unit's loadout. Clear it to show the loadout" };
            preview.RegisterValueChangedCallback(evt =>
            {
                RangeGizmos.Preview = evt.newValue as FirableDefinition;
                SceneView.RepaintAll();
            });
            root.Add(preview);

            root.Add(Toggle("Labels", RangeGizmos.k_Labels, true, "Names and distances"));
            root.Add(Toggle("Spread", RangeGizmos.k_Spread, true, "Raycast and projectile spread"));
            root.Add(Toggle("Projectile Paths", RangeGizmos.k_Paths, true, "Full flight path with gravity"));
            root.Add(Toggle("Hit Test", RangeGizmos.k_Hits, true, "Raycast against the scene to show what each shot would hit first"));
            root.Add(Toggle("Fill", RangeGizmos.k_Fill, true, "Shade cones and spheres"));
            root.Add(CombatEditorStyles.Text("Select units in the scene. Their loadout's firables are drawn from where they aim", "cg-note"));
            return root;
        }

        private static Toggle Toggle(string label, string key, bool fallback, string tooltip)
        {
            var toggle = new Toggle(label) { value = StudioPrefs.Get(key, fallback), tooltip = tooltip };
            toggle.AddToClassList("cg-range-overlay__toggle");
            toggle.RegisterValueChangedCallback(evt => StudioPrefs.Set(key, evt.newValue));
            return toggle;
        }

        private static DropdownField Choice(string label, List<string> choices, string key, string tooltip)
        {
            var field = new DropdownField(label, choices, UnityEngine.Mathf.Clamp(StudioPrefs.GetInt(key, 0), 0, choices.Count - 1)) { tooltip = tooltip };
            field.RegisterValueChangedCallback(evt => StudioPrefs.SetInt(key, choices.IndexOf(evt.newValue)));
            return field;
        }
    }
}
