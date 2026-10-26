using System;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    public sealed class RunBar : VisualElement
    {
        private const long k_FlashMs = 650;

        private readonly string liveKey;
        private readonly Label text;
        private readonly Button live;
        private readonly VisualElement buttons;

        public RunBar(string action, string tooltip, Action run, string liveKey)
        {
            this.liveKey = liveKey;
            AddToClassList("cg-run-bar");

            var dot = new VisualElement();
            dot.AddToClassList("cg-run-bar__dot");
            Add(dot);
            text = CombatEditorStyles.Text(null, "cg-run-bar__text");
            Add(text);

            live = new Button(() => StudioPrefs.Set(liveKey, !StudioPrefs.Get(liveKey, true)));
            live.AddToClassList("cg-run-bar__live");
            Add(live);

            buttons = new VisualElement();
            buttons.AddToClassList("cg-run-bar__buttons");
            Add(buttons);
            AddButton(action, tooltip, run, true);
            RefreshLive();
        }

        public void AddButton(string label, string tooltip, Action run, bool primary = false)
        {
            var button = new Button(run) { text = label, tooltip = tooltip };
            button.AddToClassList("cg-run-bar__button");
            if(primary) button.AddToClassList("cg-run-bar__button--primary");
            buttons.Add(button);
        }

        public void Report(string message)
        {
            text.text = $"{message}  ·  {DateTime.Now:HH:mm:ss}";
            EnableInClassList("cg-run-bar--idle", false);
            RefreshLive();
            AddToClassList("cg-run-bar--fresh");
            schedule.Execute(() => RemoveFromClassList("cg-run-bar--fresh")).ExecuteLater(k_FlashMs);
        }

        public void Idle(string message)
        {
            text.text = message;
            EnableInClassList("cg-run-bar--idle", true);
            RefreshLive();
        }

        public void RefreshLive()
        {
            bool on = StudioPrefs.Get(liveKey, true);
            live.text = on ? "Live" : "Paused";
            live.tooltip = on ? "Changing any setting or asset runs it again by itself. Click to stop that" : "It only runs when you press the button. Click to run again by itself on every change";
            EnableInClassList("cg-run-bar--live", on);
        }
    }
}
