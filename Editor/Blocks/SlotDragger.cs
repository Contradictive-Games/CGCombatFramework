using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class SlotGrip : VisualElement
    {
        public SlotGrip()
        {
            AddToClassList("cg-grip");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if(rect.width <= 0f || rect.height <= 0f) return;
            var painter = context.painter2D;
            var color = resolvedStyle.color;
            painter.fillColor = new Color(color.r, color.g, color.b, color.a * 0.55f);
            float dot = Mathf.Max(1f, rect.width * 0.14f);
            for(int row = 0; row < 3; row++)
            {
                for(int column = 0; column < 2; column++)
                {
                    var center = new Vector2(rect.x + rect.width * (0.33f + column * 0.34f), rect.y + rect.height * (0.25f + row * 0.25f));
                    painter.BeginPath();
                    painter.Arc(center, dot, 0f, 360f);
                    painter.Fill();
                }
            }
        }
    }

    public sealed class SlotDragger : PointerManipulator
    {
        private const float k_Threshold = 4f;
        private const string k_Dragging = "cg-slot--dragging";
        private const string k_Before = "cg-slot--drop-before";
        private const string k_After = "cg-slot--drop-after";

        private readonly Func<VisualElement, string> pathOf;
        private readonly Func<IEnumerable<VisualElement>> rowsOf;
        private readonly Action<string, int> moved;
        private readonly List<VisualElement> rows = new();
        private string path;
        private string arrayPath;
        private int from;
        private Vector3 start;
        private int pointerId = -1;
        private bool dragging;
        private bool swallowClick;
        private VisualElement over;
        private bool after;

        public SlotDragger(Func<VisualElement, string> pathOf, Func<IEnumerable<VisualElement>> rowsOf, Action<string, int> moved)
        {
            this.pathOf = pathOf;
            this.rowsOf = rowsOf;
            this.moved = moved;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        public static int Destination(int from, int over, bool after)
        {
            int to = after ? over + 1 : over;
            return to > from ? to - 1 : to;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
        }

        private void OnDown(PointerDownEvent evt)
        {
            swallowClick = false;
            pointerId = -1;
            if(!CanStartManipulation(evt) || !StudioPrefs.DragToReorder) return;
            path = pathOf(target);
            if(!BlockEdits.TrySplit(path, out arrayPath, out from)) return;
            pointerId = evt.pointerId;
            start = evt.position;
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if(evt.pointerId != pointerId) return;
            if(!dragging)
            {
                if((evt.position - start).sqrMagnitude < k_Threshold * k_Threshold) return;
                if(!Begin())
                {
                    pointerId = -1;
                    return;
                }
            }
            Track(evt.position.y);
            evt.StopPropagation();
        }

        private bool Begin()
        {
            rows.Clear();
            foreach(var row in rowsOf())
            {
                if(row.panel == null || row.resolvedStyle.display == DisplayStyle.None) continue;
                if(BlockEdits.TrySplit(pathOf(row), out var array, out _) && array == arrayPath) rows.Add(row);
            }
            if(rows.Count < 2) return false;
            rows.Sort((a, b) => a.worldBound.y.CompareTo(b.worldBound.y));
            dragging = true;
            target.CapturePointer(pointerId);
            target.AddToClassList(k_Dragging);
            return true;
        }

        private void Track(float y)
        {
            over = rows[rows.Count - 1];
            after = true;
            foreach(var row in rows)
            {
                if(y < row.worldBound.center.y)
                {
                    over = row;
                    after = false;
                    break;
                }
            }
            foreach(var row in rows)
            {
                row.EnableInClassList(k_Before, row == over && !after);
                row.EnableInClassList(k_After, row == over && after);
            }
        }

        private void OnUp(PointerUpEvent evt)
        {
            if(evt.pointerId != pointerId) return;
            bool dropped = dragging;
            string dropPath = dropped && over != null ? pathOf(over) : null;
            bool dropAfter = after;
            string dragged = path;
            int index = from;
            if(dropped && target.HasPointerCapture(pointerId)) target.ReleasePointer(pointerId);
            End();
            if(!dropped) return;
            evt.StopPropagation();
            swallowClick = true;
            if(!BlockEdits.TrySplit(dropPath, out _, out int overIndex)) return;
            int to = Destination(index, overIndex, dropAfter);
            if(to != index) moved?.Invoke(dragged, to);
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if(dragging) End();
        }

        private void OnClick(ClickEvent evt)
        {
            if(!swallowClick) return;
            swallowClick = false;
            evt.StopImmediatePropagation();
        }

        private void End()
        {
            pointerId = -1;
            dragging = false;
            target.RemoveFromClassList(k_Dragging);
            foreach(var row in rows)
            {
                row.RemoveFromClassList(k_Before);
                row.RemoveFromClassList(k_After);
            }
            rows.Clear();
            over = null;
        }
    }
}
