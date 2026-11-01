using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework
{
    public static class CombatServices
    {
        private static ITickSource tickSource;
        private static IGameRandom random;
        private static UnitRegistry units;
        private static IUnitSpawner spawner;
        private static Transform poolRoot;
        private static EventBus events;
        private static readonly HashSet<object> pauseKeys = new();
        private static readonly Clock clock = new();
        private static float time;

        public static ITickSource TickSource
        {
            get
            {
                if(tickSource is Object unityObject && unityObject == null) tickSource = null;

                if(tickSource == null)
                {
                    var go = new GameObject($"[{Globals.k_PACKAGE_NAME} Ticker]");
                    Object.DontDestroyOnLoad(go);
                    tickSource = go.AddComponent<CombatTicker>();
                    tickSource.Register(clock);
                }
                return tickSource;
            }
            set
            {
                if(ReferenceEquals(tickSource, value)) return;
                bool destroyed = tickSource is Object unityObject && unityObject == null;
                if(!destroyed) tickSource?.Unregister(clock);
                tickSource = value;
                tickSource?.Register(clock);
            }
        }

        public static float Time => time;

        public static IGameRandom Random
        {
            get => random ??= new UnityGameRandom();
            set => random = value;
        }

        public static UnitRegistry Units => units ??= new UnitRegistry();
        public static IUnitSpawner Spawner
        {
            get => spawner ??= new PooledUnitSpawner(PoolRoot);
            set => spawner = value;
        }
        private static Transform PoolRoot
        {
            get
            {
                if(poolRoot == null)
                {
                    var go = new GameObject($"[{Globals.k_PACKAGE_NAME} Pools]");
                    Object.DontDestroyOnLoad(go);
                    poolRoot = go.transform;
                }
                return poolRoot;
            }
        }

        public static EventBus Events => events ??= new EventBus(false);

        public static bool IsPaused => pauseKeys.Count > 0;

        public static void Pause(object key)
        {
            if(key == null) throw new ArgumentNullException(nameof(key));
            bool wasPaused = IsPaused;
            pauseKeys.Add(key);
            if(!wasPaused) RaisePauseChanged(true);
        }

        public static void Unpause(object key)
        {
            if(key == null || !pauseKeys.Remove(key)) return;
            if(!IsPaused) RaisePauseChanged(false);
        }

        public static void UnpauseAll()
        {
            if(pauseKeys.Count == 0) return;
            pauseKeys.Clear();
            RaisePauseChanged(false);
        }

        private static void RaisePauseChanged(bool paused) => GameEvents.RaiseGlobal(new PauseChangedEvent(paused));


        public static void EndSession(DespawnReason reason = DespawnReason.SessionEnd)
        {
            GameEvents.RaiseGlobal(new SessionEndedEvent(reason));
            CombatScheduler.Clear();
            ProjectilePool.ReleaseAll();
            Spawner.DespawnAll(reason);
            var leftovers = new List<IUnit>(Units.All);
            foreach(var unit in leftovers)
            {
                Spawner.Despawn(unit, reason);
                if(unit is Unit u && u != null && u.IsDeparting) u.FinishDeparture();
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            tickSource = null;
            random = null;
            units = null;
            spawner = null;
            poolRoot = null;
            events = null;
            pauseKeys.Clear();
            time = 0f;
        }

        private sealed class Clock : ITickable
        {
            public void Tick(float deltaTime)
            {
                time += deltaTime;
                CombatScheduler.RunDue();
            }
        }
    }

}
