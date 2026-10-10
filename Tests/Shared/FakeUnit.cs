using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    /// <summary>An IUnit with no Unit component. Has a StatBlock; add anything else with Add or AddUnitComponent.</summary>
    public sealed class FakeUnit : IUnit, IDisposable
    {
        private static ulong nextId = 1_000_000; // far from the registry's ids

        private readonly List<object> capabilities = new();
        private readonly List<IUnitComponent> components = new();
        private readonly GameObject gameObject;
        private IHealth health;

        public FakeUnit(string name = "Fake Unit")
        {
            gameObject = new GameObject(name);
            DisplayName = name;
            Stats = new StatBlock(null);
            capabilities.Add(Stats);
            Id = new UnitId(nextId++);
            IsSpawned = true;
        }

        public string DisplayName { get; }
        public Transform Transform => gameObject.transform;
        public Faction Faction { get; set; }
        public TagContainer Tags { get; } = new();
        public EventBus Events { get; } = new();
        public UnitId Id { get; private set; }
        public IUnit Owner { get; set; }
        public bool IsSpawned { get; private set; }
        public bool IsAlive => IsSpawned && (health == null || !health.IsDead);
        public StatBlock Stats { get; }

        public event Action<IUnit> OnSpawned;
        public event Action<IUnit, DespawnReason> OnDespawned;

        public void Add(object capability)
        {
            capabilities.Add(capability);
            if (capability is IHealth h) health = h;
        }

        /// <summary>Adds a real unit component and runs Initialize and Bind, like Unit.Awake.</summary>
        public T AddUnitComponent<T>() where T : Component, IUnitComponent
        {
            var component = gameObject.AddComponent<T>();
            Add(component);
            components.Add(component);
            component.Initialize(this);
            component.Bind();
            if (IsSpawned) component.OnSpawned(new SpawnData(Transform.position, Transform.rotation, Id));
            return component;
        }

        public void Spawn(in SpawnData data = default)
        {
            if (IsSpawned) return;
            Id = new UnitId(nextId++);
            IsSpawned = true;
            foreach (var component in components) component.OnSpawned(in data);
            OnSpawned?.Invoke(this);
        }

        public void Despawn(DespawnReason reason = DespawnReason.Recycled)
        {
            if (!IsSpawned) return;
            IsSpawned = false;
            OnDespawned?.Invoke(this, reason);
            for (int i = components.Count - 1; i >= 0; i--) components[i].OnDespawned(reason);
            Id = UnitId.None;
        }

        public bool TryGet<T>(out T capability) where T : class
        {
            for (int i = 0; i < capabilities.Count; i++)
            {
                if (capabilities[i] is T match)
                {
                    capability = match;
                    return true;
                }
            }
            capability = null;
            return false;
        }

        public void Dispose()
        {
            foreach (var component in components) component.Unbind();
            Object.DestroyImmediate(gameObject);
        }
    }
}