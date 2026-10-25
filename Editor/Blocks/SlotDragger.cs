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
        private const string k_Row = "cg-slot";

        private readonly VisualElement row;
        private readonly VisualElement list;
        private readonly int index;
        private readonly Action<int, int> moved;
        private readonly List<VisualElement> rows = new();
        private int pointerId = -1;
        private int insertAt = -1;

        public SlotDragger(VisualElement row, VisualElement list, int index, Action<int, int> moved)
        {
            this.row = row;
            this.list = list;
            this.index = index;
            this.moved = moved;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        }

        private void OnDown(PointerDownEvent evt)
        {
            if(!CanStartManipulation(evt)) return;
            pointerId = evt.pointerId;
            insertAt = -1;
            rows.Clear();
            foreach(var child in list.Children())
            {
                if(child.ClassListContains(k_Row)) rows.Add(child);
            }
            target.CapturePointer(pointerId);
            row.AddToClassList("cg-slot--dragging");
            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if(evt.pointerId != pointerId || !target.HasPointerCapture(pointerId)) return;
            insertAt = rows.Count;
            for(int i = 0; i < rows.Count; i++)
            {
                if(evt.position.y < rows[i].worldBound.center.y)
                {
                    insertAt = i;
                    break;
                }
            }
            for(int i = 0; i < rows.Count; i++)
            {
                rows[i].EnableInClassList("cg-slot--drop-before", i == insertAt);
                rows[i].EnableInClassList("cg-slot--drop-after", i == rows.Count - 1 && insertAt == rows.Count);
            }
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if(evt.pointerId != pointerId) return;
            int destination = insertAt < 0 ? index : insertAt > index ? insertAt - 1 : insertAt;
            target.ReleasePointer(pointerId);
            Clear();
            evt.StopPropagation();
            if(destination != index) moved?.Invoke(index, destination);
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt) => Clear();

        private void Clear()
        {
            pointerId = -1;
            row.RemoveFromClassList("cg-slot--dragging");
            foreach(var each in rows)
            {
                each.RemoveFromClassList("cg-slot--drop-before");
                each.RemoveFromClassList("cg-slot--drop-after");
            }
        }
    }
}
