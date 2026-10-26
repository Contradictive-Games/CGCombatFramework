using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class FireTimelineElement : VisualElement
    {
        private static readonly CustomStyleProperty<Color> accentProperty = new("--cg-accent");
        private static readonly Color health = new(0.9f, 0.33f, 0.29f);

        private FireReport report;
        private Color accent = new(0.22f, 0.77f, 0.81f);

        public FireTimelineElement()
        {
            AddToClassList("cg-fire-timeline");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if(evt.customStyle.TryGetValue(accentProperty, out Color value)) accent = value;
                MarkDirtyRepaint();
            });
        }

        public void SetReport(FireReport value)
        {
            report = value;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if(report == null || rect.width <= 0f || rect.height <= 0f || report.Duration <= 0f) return;
            var painter = context.painter2D;
            var text = resolvedStyle.color;
            float top = Mathf.Max(report.Total, report.Health > 0f ? report.Health : 0f, 1f) * 1.08f;

            float X(float time) => rect.x + Mathf.Clamp01(time / report.Duration) * rect.width;
            float Y(float value) => rect.yMax - Mathf.Clamp01(value / top) * (rect.height - 6f);

            foreach(var span in report.Spans)
            {
                painter.fillColor = span.Kind == "Reload" ? new Color(text.r, text.g, text.b, 0.12f) : new Color(accent.r, accent.g, accent.b, 0.12f);
                Box(painter, new Rect(X(span.Start), rect.y, Mathf.Max(1f, X(span.End) - X(span.Start)), rect.height));
            }

            painter.strokeColor = new Color(text.r, text.g, text.b, 0.3f);
            painter.lineWidth = 1f;
            Line(painter, new Vector2(rect.x, rect.yMax - 0.5f), new Vector2(rect.xMax, rect.yMax - 0.5f));

            if(report.Health > 0f)
            {
                painter.strokeColor = new Color(health.r, health.g, health.b, 0.8f);
                float y = Y(report.Health);
                for(float x = rect.x; x < rect.xMax; x += 8f)
                {
                    Line(painter, new Vector2(x, y), new Vector2(Mathf.Min(x + 4f, rect.xMax), y));
                }
            }

            painter.strokeColor = new Color(text.r, text.g, text.b, 0.45f);
            foreach(var shot in report.Shots)
            {
                float x = X(shot.Time);
                Line(painter, new Vector2(x, rect.yMax), new Vector2(x, rect.yMax - 4f));
            }

            if(report.Curve.Count > 1)
            {
                painter.strokeColor = accent;
                painter.lineWidth = 2f;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(new Vector2(X(report.Curve[0].x), Y(report.Curve[0].y)));
                for(int i = 1; i < report.Curve.Count; i++)
                {
                    painter.LineTo(new Vector2(X(report.Curve[i].x), Y(report.Curve[i - 1].y)));
                    painter.LineTo(new Vector2(X(report.Curve[i].x), Y(report.Curve[i].y)));
                }
                painter.LineTo(new Vector2(rect.xMax, Y(report.Curve[report.Curve.Count - 1].y)));
                painter.Stroke();
            }

            if(report.TimeToKill > 0f && !report.TimeToKillEstimated)
            {
                painter.strokeColor = health;
                painter.lineWidth = 1.5f;
                float x = X(report.TimeToKill);
                Line(painter, new Vector2(x, rect.y), new Vector2(x, rect.yMax));
            }
        }

        private static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static void Box(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
