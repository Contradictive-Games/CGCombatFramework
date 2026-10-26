using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public abstract class CombatInspector : UnityEditor.Editor
    {
        public const string k_Enabled = "Inspector.Enhanced";
        private const string k_Menu = StudioWindows.k_Menu + "Enhanced Inspectors";
        private const long k_LiveMs = 200;

        private VisualElement live;

        protected virtual string Style => "cg-kind-definition";
        protected virtual string About => null;
        protected bool Single => targets.Length == 1;
        protected GameObject Owner => (target as Component) != null ? ((Component)target).gameObject : null;
        protected bool IsAsset => Owner != null && EditorUtility.IsPersistent(Owner);
        protected bool IsLive => Application.isPlaying && Owner != null && !IsAsset;

        [MenuItem(k_Menu, false, 41)]
        private static void ToggleEnhanced()
        {
            StudioPrefs.Set(k_Enabled, !StudioPrefs.Get(k_Enabled, true));
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        [MenuItem(k_Menu, true)]
        private static bool ValidateEnhanced()
        {
            Menu.SetChecked(k_Menu, StudioPrefs.Get(k_Enabled, true));
            return true;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            if(!StudioPrefs.Get(k_Enabled, true))
            {
                InspectorElement.FillDefaultInspector(root, serializedObject, this);
                return root;
            }

            CombatEditorStyles.Apply(root);
            root.AddToClassList("cg-inspector");
            root.AddToClassList(Style);
            if(Single) Header(root);
            if(!string.IsNullOrEmpty(About)) root.Add(CombatEditorStyles.Text(About, "cg-inspector__about"));
            var fields = new VisualElement();
            fields.AddToClassList("cg-inspector__fields");
            InspectorElement.FillDefaultInspector(fields, serializedObject, this);
            root.Add(fields);
            if(Single) Extras(root);

            if(IsLive && Single)
            {
                live = new VisualElement();
                live.AddToClassList("cg-inspector__live");
                live.Add(CombatEditorStyles.Text("LIVE", "cg-card__chip"));
                root.Add(live);
                Live(live);
                root.schedule.Execute(() =>
                {
                    if(target != null && live.panel != null) Refresh();
                }).Every(k_LiveMs);
            }
            return root;
        }

        protected virtual void Header(VisualElement root) { }
        protected virtual void Extras(VisualElement root) { }
        protected virtual void Live(VisualElement root) { }
        protected virtual void Refresh() { }

        protected static Button Small(string text, Action run, string tooltip = null)
        {
            var button = new Button(run) { text = text, tooltip = tooltip };
            button.AddToClassList("cg-card__button");
            return button;
        }

        protected static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-chips");
            foreach(var child in children)
            {
                if(child != null) row.Add(child);
            }
            return row;
        }

        protected static VisualElement Bar(out VisualElement fill, out Label value, string name, Color color)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-inspector__bar-row");
            row.Add(CombatEditorStyles.Text(name, "cg-inspector__bar-name"));
            var bar = new VisualElement();
            bar.AddToClassList("cg-inspector__bar");
            fill = new VisualElement();
            fill.AddToClassList("cg-inspector__fill");
            fill.style.backgroundColor = color;
            bar.Add(fill);
            row.Add(bar);
            value = CombatEditorStyles.Text(null, "cg-inspector__bar-value");
            row.Add(value);
            return row;
        }

        protected static void SetBar(VisualElement fill, Label value, IReadonlyResource resource)
        {
            if(resource == null) return;
            fill.style.width = Length.Percent(Mathf.Clamp01(resource.Normalized) * 100f);
            value.text = $"{resource.Current:0.#} / {resource.Max:0.#}";
        }

        protected void SelectOwner()
        {
            var owner = Owner;
            if(owner != null && Array.IndexOf(Selection.gameObjects, owner) < 0) Selection.activeGameObject = owner;
        }

        protected static void FollowRanges(VisualElement element, Action sync)
        {
            element.RegisterCallback<AttachToPanelEvent>(_ => RangeGizmos.Changed += sync);
            element.RegisterCallback<DetachFromPanelEvent>(_ => RangeGizmos.Changed -= sync);
            sync();
        }

        protected static GameObject PrefabOf(GameObject gameObject)
        {
            if(gameObject == null) return null;
            if(EditorUtility.IsPersistent(gameObject)) return gameObject.transform.root.gameObject;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject.transform.root.gameObject);
            return source != null ? source : null;
        }

        protected static string Description(Type type)
        {
            foreach(var info in UnitParts.All)
            {
                if(info.Type == type) return info.Description;
            }
            return null;
        }
    }
}
