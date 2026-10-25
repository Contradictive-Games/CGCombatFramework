using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class HistogramElement : VisualElement
    {
        private static readonly CustomStyleProperty<Color> accentProperty = new("--cg-accent");

        private float[] values = System.Array.Empty<float>();
        private float marker = float.NaN;
        private int bins = 24;
        private Color color = new(0.3f, 0.55f, 0.96f);

        public HistogramElement()
        {
            AddToClassList("cg-histogram");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                if(evt.customStyle.TryGetValue(accentProperty, out Color accent)) color = accent;
                MarkDirtyRepaint();
            });
        }

        public void SetValues(float[] sorted, float highlight, int binCount)
        {
            values = sorted ?? System.Array.Empty<float>();
            marker = highlight;
            bins = Mathf.Max(1, binCount);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if(values.Length == 0 || rect.width <= 0f || rect.height <= 0f) return;

            float min = values[0];
            float max = values[values.Length - 1];
            float span = max - min;
            int count = span <= Mathf.Epsilon ? 1 : bins;
            var heights = new int[count];
            int tallest = 0;
            foreach(float value in values)
            {
                int bin = count == 1 ? 0 : Mathf.Min(count - 1, (int)((value - min) / span * count));
                heights[bin]++;
                if(heights[bin] > tallest) tallest = heights[bin];
            }

            var painter = context.painter2D;
            float gap = count > 1 ? Mathf.Min(2f, rect.width / count * 0.2f) : 0f;
            float width = rect.width / count;
            painter.fillColor = new Color(color.r, color.g, color.b, 0.85f);
            for(int i = 0; i < count; i++)
            {
                if(heights[i] == 0) continue;
                float height = Mathf.Max(1f, rect.height * heights[i] / tallest);
                float x = rect.x + i * width;
                Box(painter, new Rect(x + gap * 0.5f, rect.yMax - height, width - gap, height));
            }

            var baseline = resolvedStyle.color;
            painter.strokeColor = new Color(baseline.r, baseline.g, baseline.b, 0.35f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.x, rect.yMax - 0.5f));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - 0.5f));
            painter.Stroke();

            if(float.IsNaN(marker) || span <= Mathf.Epsilon) return;
            float at = rect.x + Mathf.Clamp01((marker - min) / span) * rect.width;
            painter.strokeColor = new Color(baseline.r, baseline.g, baseline.b, 0.9f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(at, rect.y));
            painter.LineTo(new Vector2(at, rect.yMax));
            painter.Stroke();
        }

        private static void Box(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
