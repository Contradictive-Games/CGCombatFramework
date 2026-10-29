using System;
using UnityEngine;

namespace CG.CombatFramework
{
    public interface IUnit
    {
        public string DisplayName { get; }
        public Transform Transform { get; }
        public Faction Faction { get; }
        public TagContainer Tags { get; }
        public EventBus Events { get; }


        public UnitId Id { get; }
        public IUnit Owner { get; }
        public bool IsAlive { get; }

        public bool TryGet<T>(out T capability) where T : class;

        public bool IsSpawned { get; }
        public event Action<IUnit> OnSpawned;
        public event Action<IUnit, DespawnReason> OnDespawned;
    }
}
