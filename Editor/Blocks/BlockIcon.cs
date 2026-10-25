using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class BlockIcon : VisualElement
    {
        private static readonly CustomStyleProperty<Color> accentProperty = new("--cg-accent");
        private static readonly CustomStyleProperty<string> glyphProperty = new("--cg-glyph");

        private Color color = new(0.55f, 0.58f, 0.62f);
        private string glyph = string.Empty;
        private string forcedGlyph;

        public BlockIcon()
        {
            AddToClassList("cg-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        public void SetCategory(BlockCategory category)
        {
            forcedGlyph = null;
            BlockCategories.Apply(this, category);
            MarkDirtyRepaint();
        }

        public void SetGlyph(string name, BlockCategory category)
        {
            BlockCategories.Apply(this, category);
            forcedGlyph = name;
            MarkDirtyRepaint();
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if(evt.customStyle.TryGetValue(accentProperty, out Color accent)) color = accent;
            if(evt.customStyle.TryGetValue(glyphProperty, out string name)) glyph = name.Trim('"', '\'');
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            float size = Mathf.Min(rect.width, rect.height);
            if(size <= 0f) return;

            var painter = context.painter2D;
            var center = rect.center;
            float radius = size * 0.5f;
            painter.fillColor = color;
            painter.strokeColor = color;
            painter.lineWidth = Mathf.Max(1f, size * 0.13f);
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            Vector2 At(float x, float y) => center + new Vector2(x, y) * radius;

            void Shape(params float[] points)
            {
                painter.BeginPath();
                painter.MoveTo(At(points[0], points[1]));
                for(int i = 2; i < points.Length; i += 2)
                {
                    painter.LineTo(At(points[i], points[i + 1]));
                }
                painter.ClosePath();
                painter.Fill();
            }

            void Line(float x1, float y1, float x2, float y2)
            {
                painter.BeginPath();
                painter.MoveTo(At(x1, y1));
                painter.LineTo(At(x2, y2));
                painter.Stroke();
            }

            void Circle(float scale, bool fill)
            {
                painter.BeginPath();
                painter.Arc(center, radius * scale, 0f, 360f);
                if(fill) painter.Fill();
                else painter.Stroke();
            }

            switch(forcedGlyph ?? glyph)
            {
                case "payload":
                    Shape(0f, -1f, 0.28f, -0.28f, 1f, 0f, 0.28f, 0.28f, 0f, 1f, -0.28f, 0.28f, -1f, 0f, -0.28f, -0.28f);
                    break;
                case "emission":
                    Shape(-0.6f, -0.85f, 0.85f, 0f, -0.6f, 0.85f);
                    break;
                case "query":
                    Circle(0.55f, false);
                    Line(0f, -1f, 0f, -0.3f);
                    Line(0f, 1f, 0f, 0.3f);
                    Line(-1f, 0f, -0.3f, 0f);
                    Line(1f, 0f, 0.3f, 0f);
                    break;
                case "condition":
                    painter.BeginPath();
                    painter.MoveTo(At(0f, -0.85f));
                    painter.LineTo(At(0.85f, 0f));
                    painter.LineTo(At(0f, 0.85f));
                    painter.LineTo(At(-0.85f, 0f));
                    painter.ClosePath();
                    painter.Stroke();
                    break;
                case "scaler":
                    Shape(-0.85f, 0.85f, -0.85f, 0.25f, -0.4f, 0.25f, -0.4f, 0.85f);
                    Shape(-0.22f, 0.85f, -0.22f, -0.25f, 0.22f, -0.25f, 0.22f, 0.85f);
                    Shape(0.4f, 0.85f, 0.4f, -0.85f, 0.85f, -0.85f, 0.85f, 0.85f);
                    break;
                case "gate":
                    Shape(-0.7f, -0.05f, 0.7f, -0.05f, 0.7f, 0.9f, -0.7f, 0.9f);
                    painter.BeginPath();
                    painter.Arc(At(0f, -0.05f), radius * 0.42f, 180f, 360f);
                    painter.Stroke();
                    break;
                case "firemode":
                    Shape(0.2f, -1f, -0.65f, 0.15f, -0.05f, 0.15f, -0.25f, 1f, 0.65f, -0.2f, 0.05f, -0.2f);
                    break;
                case "damagestep":
                    Shape(0f, -0.95f, 0.8f, -0.62f, 0.72f, 0.12f, 0f, 0.95f, -0.72f, 0.12f, -0.8f, -0.62f);
                    break;
                case "regen":
                    painter.lineWidth = Mathf.Max(1.5f, size * 0.26f);
                    Line(0f, -0.72f, 0f, 0.72f);
                    Line(-0.72f, 0f, 0.72f, 0f);
                    break;
                case "behavior":
                    Shape(0f, -0.95f, 0.82f, -0.48f, 0.82f, 0.48f, 0f, 0.95f, -0.82f, 0.48f, -0.82f, -0.48f);
                    break;
                case "trigger":
                    Circle(0.75f, false);
                    Circle(0.3f, true);
                    break;
                default:
                    Circle(0.42f, true);
                    break;
            }
        }
    }
}
