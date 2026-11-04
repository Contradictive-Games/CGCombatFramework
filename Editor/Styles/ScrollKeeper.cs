using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class ScrollKeeper
    {
        private const long k_WindowMs = 1000;

        private readonly ScrollView scroll;
        private Vector2? pending;
        private int token;

        public ScrollKeeper(ScrollView scroll)
        {
            this.scroll = scroll;
            scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            scroll.RegisterCallback<WheelEvent>(_ => pending = null, TrickleDown.TrickleDown);
            scroll.verticalScroller.RegisterCallback<PointerDownEvent>(_ => pending = null, TrickleDown.TrickleDown);
        }

        public Vector2 Capture() => pending ?? scroll.scrollOffset;

        public void Keep(Vector2 offset)
        {
            if(offset.y <= 0f) return;
            pending = offset;
            int mine = ++token;
            scroll.schedule.Execute(() =>
            {
                if(mine == token) pending = null;
            }).ExecuteLater(k_WindowMs);
            Apply();
        }

        private void Apply()
        {
            if(pending == null) return;
            var offset = pending.Value;
            scroll.scrollOffset = offset;
            if(scroll.verticalScroller.highValue >= offset.y - 0.5f) pending = null;
        }
    }
}
