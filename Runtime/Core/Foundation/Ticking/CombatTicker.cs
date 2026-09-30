using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu("Contradictive Games/CGCombatFramework/Combat/Combat Ticker")]
    public sealed class CombatTicker : MonoBehaviour, ITickSource
    {
        private float interval = 0f;
        private bool useUnscaledTime = false;

        private readonly HashSet<ITickable> tickables = new();
        private readonly HashSet<ITickable> pendingAdds = new();
        private readonly HashSet<ITickable> pendingRemoves = new();

        private bool ticking;
        private float accumulator;

        public void Register(ITickable tickable)
        {
            if (ticking)
            {
                pendingAdds.Add(tickable);
                return;
            }
            if(!tickables.Contains(tickable)) tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable)
        {
            if(ticking)
            {
                pendingRemoves.Add(tickable);
                return;
            }
            tickables.Remove(tickable);
        }


        private void Update()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if(interval <= 0f)
            {
                Step(deltaTime);
                return;
            }

            accumulator += deltaTime;
            while(accumulator >= interval)
            {
                accumulator -= interval;
                Step(interval);
            }
        }


        private void Step(float deltaTime)
        {
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
