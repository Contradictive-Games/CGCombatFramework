using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class ScrollKeeper
    {
        private const long k_WindowMs = 1000;

        private readonly ScrollView scroll;
        private Func<float> target;
        private int token;

        public ScrollKeeper(ScrollView scroll)
        {
            this.scroll = scroll;
            scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            scroll.RegisterCallback<WheelEvent>(_ => Release(), TrickleDown.TrickleDown);
            scroll.verticalScroller.RegisterCallback<PointerDownEvent>(_ => Release(), TrickleDown.TrickleDown);
        }

        public float Offset => scroll.scrollOffset.y;

        public float ViewportY(VisualElement element) => element.layout.y - scroll.scrollOffset.y;

        public void Hold(Func<float> desired)
        {
            if(desired == null) return;
            target = desired;
            int mine = ++token;
            scroll.schedule.Execute(() =>
            {
                if(mine == token) target = null;
            }).ExecuteLater(k_WindowMs);
            Apply();
        }

        public void Release() => target = null;

        public void Reveal(VisualElement element, float margin = 6f)
        {
            Release();
            if(element == null) return;
            float y = scroll.scrollOffset.y + element.worldBound.y - scroll.contentViewport.worldBound.y - margin;
            scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, Mathf.Max(0f, y));
        }

        private void Apply()
        {
            if(target == null) return;
            float y = target();
            if(float.IsNaN(y)) return;
            scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, Mathf.Max(0f, y));
        }
    }
}
