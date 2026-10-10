using System.Collections.Generic;

namespace CG.CombatFramework.Tests
{
    /// <summary>Ticks only when told to. Deterministic time for tests. Respects pause like CombatTicker.</summary>
    public sealed class ManualTickSource : ITickSource
    {
        private readonly List<ITickable> tickables = new();
        private readonly List<ITickable> buffer = new();

        public int Count => tickables.Count;

        public void Register(ITickable tickable)
        {
            if (!tickables.Contains(tickable)) tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable) => tickables.Remove(tickable);

        public void Step(float deltaTime)
        {
            if (CombatServices.IsPaused) return;
            buffer.Clear();
            for (int i = 0; i < tickables.Count; i++) buffer.Add(tickables[i]);
            for (int i = 0; i < buffer.Count; i++)
                if (tickables.Contains(buffer[i])) buffer[i].Tick(deltaTime);
        }

        /// <summary>Steps until the total reaches the given seconds.</summary>
        public void Run(float seconds, float deltaTime = 1f / 60f)
        {
            int steps = UnityEngine.Mathf.CeilToInt(seconds / deltaTime - 0.0001f);
            for (int i = 0; i < steps; i++) Step(deltaTime);
        }
    }
}