using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class UnitRegistry
    {
        private readonly Dictionary<UnitId, IUnit> byId = new();
        private readonly List<IUnit> all = new();
        private ulong nextId = 1;

        public IReadOnlyList<IUnit> All => all;
        public int Count => all.Count;

        internal UnitId AssignId(UnitId requested)
        {
            return requested.IsValid ? requested : new UnitId(nextId++);
        }

        public bool TryGet(UnitId id, out IUnit unit) => byId.TryGetValue(id, out unit);

        internal void Add(IUnit unit)
        {
            if(!byId.TryAdd(unit.Id, unit)) throw new InvalidOperationException($"{unit.Id} is already registered to {byId[unit.Id].DisplayName}");
            all.Add(unit);
        }
        internal void Remove(IUnit unit)
        {
            if(byId.Remove(unit.Id)) all.Remove(unit);
        }
    }
}