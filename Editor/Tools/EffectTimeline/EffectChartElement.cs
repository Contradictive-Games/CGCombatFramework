using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class EffectChartElement : VisualElement
    {
        private static readonly CustomStyleProperty<Color> accentProperty = new("--cg-accent");
        private static readonly Color healthColor = new(0.25f, 0.73f, 0.31f);
        private static readonly Color damageColor = new(0.9f, 0.33f, 0.29f);
        private static readonly Color healColor = new(0.18f, 0.66f, 0.56f);

        private EffectReport report;
        private Color accent = new(0.43f, 0.48f, 0.95f);

        public EffectChartElement()
        {
            AddToClassList("cg-effect-chart");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if(evt.customStyle.TryGetValue(accentProperty, out Color value)) accent = value;
                MarkDirtyRepaint();
            });
        }

        public void SetReport(EffectReport value)
        {
            report = value;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if(report == null || report.Duration <= 0f || rect.width <= 0f || rect.height <= 0f) return;
            var painter = context.painter2D;
            var text = resolvedStyle.color;
            float X(float time) => rect.x + Mathf.Clamp01(time / report.Duration) * rect.width;

            painter.lineWidth = 1f;
            painter.strokeColor = new Color(text.r, text.g, text.b, 0.3f);
            Line(painter, new Vector2(rect.x, rect.yMax - 0.5f), new Vector2(rect.xMax, rect.yMax - 0.5f));

            painter.strokeColor = new Color(accent.r, accent.g, accent.b, 0.35f);
            foreach(var each in report.Events.Where(e => e.Kind == EffectEventKind.Applied || e.Kind == EffectEventKind.Stacks))
            {
                float x = X(each.Time);
                Line(painter, new Vector2(x, rect.y), new Vector2(x, rect.yMax));
            }

            float biggest = report.Events.Where(e => e.Kind == EffectEventKind.Damage || e.Kind == EffectEventKind.Heal).Select(e => e.Amount).DefaultIfEmpty(0f).Max();
            if(biggest > 0f)
            {
                float lane = rect.height * 0.3f;
                foreach(var each in report.Events)
                {
                    if(each.OnSource || (each.Kind != EffectEventKind.Damage && each.Kind != EffectEventKind.Heal)) continue;
                    float x = X(each.Time);
                    float height = Mathf.Max(2f, lane * each.Amount / biggest);
                    painter.fillColor = each.Kind == EffectEventKind.Damage ? damageColor : healColor;
                    Box(painter, new Rect(x - 1.5f, rect.yMax - height, 3f, height));
                }
            }

            if(report.HealthMax > 0f && report.Health.Count > 1)
            {
                painter.strokeColor = healthColor;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                for(int i = 0; i < report.Health.Count; i++)
                {
                    var point = new Vector2(X(report.Health[i].x), rect.yMax - Mathf.Clamp01(report.Health[i].y / report.HealthMax) * (rect.height - 4f));
                    if(i == 0) painter.MoveTo(point);
                    else painter.LineTo(point);
                }
                painter.Stroke();
            }

            if(report.MaxStacks > 0)
            {
                painter.strokeColor = accent;
                painter.lineWidth = 2f;
                painter.BeginPath();
                float Y(float stacks) => rect.yMax - stacks / report.MaxStacks * (rect.height * 0.85f);
                float last = 0f;
                painter.MoveTo(new Vector2(rect.x, Y(0f)));
                foreach(var point in report.Stacks)
                {
                    painter.LineTo(new Vector2(X(point.x), Y(last)));
                    painter.LineTo(new Vector2(X(point.x), Y(point.y)));
                    last = point.y;
                }
                painter.LineTo(new Vector2(rect.xMax, Y(last)));
                painter.Stroke();
            }

            if(report.EndedAt >= 0f)
            {
                painter.strokeColor = new Color(text.r, text.g, text.b, 0.7f);
                painter.lineWidth = 1f;
                float x = X(report.EndedAt);
                for(float y = rect.y; y < rect.yMax; y += 6f)
                {
                    Line(painter, new Vector2(x, y), new Vector2(x, Mathf.Min(y + 3f, rect.yMax)));
                }
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
