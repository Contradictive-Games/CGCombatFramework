using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Combat Ticker")]
    public sealed class CombatTicker : MonoBehaviour, ITickSource
    {
        [Header("Settings")]
        [Tooltip("Seconds between each tick")]
        [SerializeField, Min(0f)] private float interval = 0f;
        [SerializeField] private bool useUnscaledTime = false;
        [Tooltip("How many 'catc-up' ticks we will do in one frame after any sort of delay (like lag)")]
        [SerializeField, Min(1)] private int maxTickStepsPerFrame = 5;

        private readonly HashSet<ITickable> tickables = new();
        private readonly HashSet<ITickable> pendingAdds = new();
        private readonly HashSet<ITickable> pendingRemoves = new();

        private bool ticking;
        private float accumulator;

        public float Interval
        {
            get => interval;
            set => interval = Mathf.Max(0f, value);
        }
        public bool UseUnscaledTime
        {
            get => useUnscaledTime;
            set => useUnscaledTime = value;
        }

        public void Register(ITickable tickable)
        {
            if(!ticking)
            {
                tickables.Add(tickable);
                return;
            }
            pendingRemoves.Remove(tickable);
            pendingAdds.Add(tickable);
        }

        public void Unregister(ITickable tickable)
        {
            if(!ticking)
            {
                tickables.Remove(tickable);
                return;
            }
            pendingAdds.Remove(tickable);
            pendingRemoves.Add(tickable);
        }


        private void Update()
        {
            if(CombatServices.IsPaused) return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if(interval <= 0f)
            {
                Step(deltaTime);
                return;
            }

            accumulator = Mathf.Min(accumulator + deltaTime, interval * maxTickStepsPerFrame);
            while(accumulator >= interval)
            {
                accumulator -= interval;
                Step(interval);
            }
        }


        private void Step(float deltaTime)
        {
            using var sample = CombatProfiler.Tick.Auto();
            
            ticking = true;
            try
            {
                foreach(var tickable in tickables)
                {
                    tickable.Tick(deltaTime);
                }
            }
            finally
            {
                ticking = false;
                foreach(var pendingRemove in pendingRemoves)
                {
                    tickables.Remove(pendingRemove);
                }
                foreach(var pendingAdd in pendingAdds)
                {
                    if(!tickables.Contains(pendingAdd)) tickables.Add(pendingAdd);
                }

                pendingAdds.Clear();
                pendingRemoves.Clear();
            }
        }
    }
}
