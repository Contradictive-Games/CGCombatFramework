using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class OutlineGuides
    {
        private const string k_Item = "unity-tree-view__item";
        private const string k_Indents = "unity-tree-view__item-indents";
        private const string k_Toggle = "unity-tree-view__item-toggle";
        private const float k_Alpha = 0.28f;

        private readonly struct Guide
        {
            public readonly bool[] Levels;
            public readonly bool Leaf;

            public Guide(bool[] levels, bool leaf)
            {
                Levels = levels;
                Leaf = leaf;
            }
        }

        private readonly Dictionary<VisualElement, Guide> guides = new();

        public bool Enabled { get; set; } = true;

        public void Bind(VisualElement row, bool[] levels, bool leaf)
        {
            var indents = IndentsOf(row);
            if(indents == null) return;
            if(!guides.ContainsKey(indents)) indents.generateVisualContent += context => Draw(indents, context);
            guides[indents] = new Guide(levels, leaf);
            indents.MarkDirtyRepaint();
        }

        public void Refresh()
        {
            foreach(var indents in guides.Keys)
            {
                indents.MarkDirtyRepaint();
            }
        }

        private static VisualElement IndentsOf(VisualElement row)
        {
            for(var each = row.parent; each != null; each = each.parent)
            {
                if(each.ClassListContains(k_Item)) return each.Q(className: k_Indents);
            }
            return null;
        }

        private void Draw(VisualElement indents, MeshGenerationContext context)
        {
            if(!Enabled || !guides.TryGetValue(indents, out var guide)) return;
            int depth = guide.Levels.Length;
            var rect = indents.contentRect;
            if(depth == 0 || rect.width <= 0f) return;

            var item = indents.parent;
            float top = item != null ? -indents.layout.y : 0f;
            float bottom = item != null ? top + item.layout.height : rect.height;
            float middle = Mathf.Floor((top + bottom) * 0.5f) + 0.5f;
            float unit = rect.width / depth;
            var toggle = item?.Q(className: k_Toggle);
            float offset = toggle != null && toggle.layout.width > 0f ? toggle.layout.center.x - indents.layout.xMax : unit * 0.5f;

            var painter = context.painter2D;
            var color = indents.resolvedStyle.color;
            painter.strokeColor = new Color(color.r, color.g, color.b, k_Alpha);
            painter.lineWidth = 1f;

            void Line(float x1, float y1, float x2, float y2)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x1, y1));
                painter.LineTo(new Vector2(x2, y2));
                painter.Stroke();
            }

            for(int i = 0; i < depth; i++)
            {
                float x = Mathf.Floor(i * unit + offset) + 0.5f;
                if(i < depth - 1)
                {
                    if(guide.Levels[i]) Line(x, top, x, bottom);
                    continue;
                }
                Line(x, top, x, guide.Levels[i] ? bottom : middle);
                Line(x, middle, guide.Leaf ? rect.width + offset : rect.width + 1f, middle);
            }
        }
    }
}
