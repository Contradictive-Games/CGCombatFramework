using System;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal sealed class Debouncer
    {
        public const long k_SearchMs = 150;

        private readonly VisualElement owner;
        private readonly long delayMs;
        private readonly Action run;
        private IVisualElementScheduledItem item;

        public Debouncer(VisualElement owner, long delayMs, Action run)
        {
            this.owner = owner;
            this.delayMs = delayMs;
            this.run = run;
        }

        public void Poke()
        {
            if(item == null)
            {
                item = owner.schedule.Execute(run);
                item.ExecuteLater(delayMs);
                return;
            }
            item.ExecuteLater(delayMs);
        }
    }
}
