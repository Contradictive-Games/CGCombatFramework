using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class UnitWatcher : IDisposable
    {
        private readonly Func<IUnit, bool> filter;
        private readonly Action<IUnit> onFound;
        private readonly Action<IUnit> onLost;
        private readonly List<IUnit> tracked = new();
        private readonly GameEventHandler<UnitSpawnedEvent> spawned;
        private readonly GameEventHandler<UnitDespawnedEvent> despawned;

        public IReadOnlyList<IUnit> Tracked => tracked;

        public UnitWatcher(Func<IUnit, bool> filter, Action<IUnit> onFound, Action<IUnit> onLost = null)
        {
            this.filter = filter;
            this.onFound = onFound;
            this.onLost = onLost;
            spawned = HandleSpawned;
            despawned = HandleDespawned;
            CombatServices.Events.Subscribe(spawned);
            CombatServices.Events.Subscribe(despawned);

            var all = CombatServices.Units.All;
            for(int i = 0; i < all.Count; i++)
            {
                Consider(all[i]);
            }
        }

        public void Refresh(IUnit unit)
        {
            if(unit == null) return;
            bool passes = unit.IsSpawned && (filter == null || filter(unit));
            if(passes) Consider(unit);
            else if(tracked.Remove(unit)) onLost?.Invoke(unit);
        }

        public void Dispose()
        {
            CombatServices.Events.Unsubscribe(spawned);
            CombatServices.Events.Unsubscribe(despawned);
            tracked.Clear();
        }

        private void HandleSpawned(in UnitSpawnedEvent evt) => Consider(evt.Unit);
        private void HandleDespawned(in UnitDespawnedEvent evt)
        {
            if(tracked.Remove(evt.Unit)) onLost?.Invoke(evt.Unit);
        }

        private void Consider(IUnit unit)
        {
            if(unit == null || tracked.Contains(unit) || (filter != null && !filter(unit))) return;
            tracked.Add(unit);
            onFound?.Invoke(unit);
        }
    }
}