using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true), CanEditMultipleObjects]
    internal sealed class UnitPartInspector : CombatInspector
    {
        protected override bool HasLive => false;

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.delayCall += RebuildStale;

        private static void RebuildStale()
        {
            var tracker = ActiveEditorTracker.sharedTracker;
            foreach(var editor in tracker.activeEditors)
            {
                if(editor == null || editor.GetType().Name != "GenericInspector" || !(editor.target is MonoBehaviour)) continue;
                tracker.ForceRebuild();
                return;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            if(Part == null)
            {
                var plain = new VisualElement();
                InspectorElement.FillDefaultInspector(plain, serializedObject, this);
                return plain;
            }
            var root = base.CreateInspectorGUI();
            var color = Part.Look.Color;
            if(color.HasValue)
            {
                var about = root.Q(className: "cg-inspector__about");
                if(about != null) Looks.Accent(about, color);
            }
            return root;
        }
    }
}
