using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Unit")]
    public sealed class Unit : MonoBehaviour, IUnit, ITickable
    {
        [SerializeField] private string displayName;
        [SerializeField] private Faction faction;
        [SerializeField] private CombatTag[] startingTags;
        [SerializeField, Tooltip("Scene placed units that will spawn in Start")]
        private bool spawnOnStart = true;

        private readonly Dictionary<Type, object> lookup = new();
        private readonly List<object> registered = new();
        private readonly List<IUnitComponent> components = new();
        private readonly List<ITickable> tickables = new();
        private readonly HashSet<Type> misses = new();
        private ITickSource tickSource;
        private Faction factionOverride;
        private IHealth health;
        private UnitHandle owner;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Transform Transform => transform;
        public Faction Faction => factionOverride != null ? factionOverride : faction;
        public TagContainer Tags { get; } = new();
        public EventBus Events { get; } = new();
        public IUnit Owner => owner.Get();

        public UnitId Id { get; private set; }
        public bool IsSpawned { get; private set; }
        public bool IsAlive => IsSpawned && (health == null || !health.IsDead);

        public event Action<IUnit> OnSpawned;
        public event Action<IUnit, DespawnReason> OnDespawned;

        private void Awake()
        {
            var foundComponents = new List<IUnitComponent>();
            GetComponents(foundComponents);
            foreach(var component in foundComponents)
            {
                Register(component);
            }

            foreach(var component in components)
            {
                component.Initialize(this);
            }
            foreach(var component in components)
            {
                component.Bind();
            }

            foreach(var tag in startingTags)
            {
                Tags.Add(tag);
            }

            TryGet(out health);

        }


        private void Start()
        {
            if(spawnOnStart && !IsSpawned) Spawn(new SpawnData(transform.position, transform.rotation, Id, Owner, faction));
        }

        private void OnDestroy()
        {
            if(IsSpawned) Despawn(DespawnReason.Unloaded);
            foreach(var component in components)
            {
                component.Unbind();
            }
        }

        public void Spawn(in SpawnData data)
        {
            if (IsSpawned)
            {
                Debug.LogWarning($"[Unit]: {DisplayName} is already spawned as {Id}", this);
                return;
            }

            transform.SetPositionAndRotation(data.Position, data.Rotation);
            Id = CombatServices.Units.AssignId(data.Id);
            owner = new UnitHandle(data.Owner);
            factionOverride = data.Faction;

            Tags.Clear();
            foreach(var tag in startingTags)
            {
                if(tag != null) Tags.Add(tag);
            }

            if(!gameObject.activeSelf) gameObject.SetActive(true);
            IsSpawned = true;
            foreach(var component in components) component.OnSpawned(in data);

            tickSource = CombatServices.TickSource;
            tickSource.Register(this);
            CombatServices.Units.Add(this);
            OnSpawned?.Invoke(this);
            GameEvents.Raise(this, new UnitSpawnedEvent(this, data));
        }

        public void Despawn(DespawnReason reason)
        {
            if(!IsSpawned) return;
            IsSpawned = false;

            tickSource?.Unregister(this);
            tickSource = null;

            OnDespawned?.Invoke(this, reason);
            GameEvents.Raise(this, new UnitDespawnedEvent(this, reason));

            for(int i = components.Count - 1; i >= 0; i--)
            {
                components[i].OnDespawned(reason);
            }

            CombatServices.Units.Remove(this);

            Id = UnitId.None;
            owner = default;
            factionOverride = null;
        }


        public void AddUnitComponent(object component)
        {
            if(component == null || registered.Contains(component)) return;
            Register(component);
            if(component is not IUnitComponent c) return;
            c.Initialize(this);
            c.Bind();
            if(IsSpawned) c.OnSpawned(new SpawnData(transform.position, transform.rotation, Id, Owner, Faction));
        }


        public bool TryGet<T>(out T component) where T : class
        {
            var type = typeof(T);
            if(lookup.TryGetValue(type, out var cached))
            {
                component = (T)cached;
                return true;
            }
            if (misses.Contains(type))
            {
                component = null;
                return false;
            }

            foreach(var candidate in registered)
            {
                if(candidate is T match)
                {
                    lookup[type] = match;
                    component = match;
                    return true;
                }
            }

            if(TryGetComponent(out component))
            {
                lookup[type] = component;
                return true;
            }

            misses.Add(type);
            component = null;
            return false;
        }


        public void Tick(float deltaTime)
        {
            for(int i = 0; i < tickables.Count; i++)
            {
                tickables[i].Tick(deltaTime);
            }
        }


        private void Register(object component)
        {
            if(component == null || registered.Contains(component)) return;
            registered.Add(component);
            lookup[component.GetType()] = component;
            misses.Clear();
            if(component is IUnitComponent c) components.Add(c);
            if(component is ITickable t) tickables.Add(t);
        }

    }
}
