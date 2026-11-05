using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [DefinitionInfo("Units", Group = "Combat", Description = "Unit prefabs: anything that spawns, takes hits or fires.", Color = "#58A6FF", Icon = "unit", Order = -120)]
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Unit")]
    public sealed class Unit : MonoBehaviour, IUnit, ITickable
    {
        [SerializeField] private string displayName;
        [SerializeField] private Faction faction;
        [SerializeField] private CombatTag[] startingTags;
        [SerializeField, Tooltip("Scene placed units that will spawn in Start")]
        private bool spawnOnStart = true;
        [Header("Arrival")]
        [SerializeField, Tooltip("Held while the unit arrives, and passed to its arrival sequences to pick visuals. A spawn that passes its own arrival tags replaces these")]
        private CombatTag[] arrivalTags = Array.Empty<CombatTag>();
        [SerializeField, Tooltip("Scene placed units play their arrival when they spawn in Start")]
        private bool arriveOnStart;

        private static readonly object notFound = new();

        private readonly List<object> registered = new();
        private readonly List<IUnitComponent> components = new();
        private readonly List<ITickable> tickables = new();
        private readonly List<IArrivalSequence> arrivals = new();
        private readonly List<IDepartureSequence> departures = new();
        private readonly List<CombatTag> heldArrivalTags = new();
        private object[] found = Array.Empty<object>();
        private ITickSource tickSource;
        private Faction factionOverride;
        private IHealth health;
        private UnitHandle owner;
        private TagContainer tags;
        private Action<Unit> releaseHandler;
        private float arrivalLeft;
        private float departureLeft;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Transform Transform => transform;
        public Faction Faction => factionOverride != null ? factionOverride : faction;
        public TagContainer Tags => tags ??= new TagContainer(this);
        public EventBus Events { get; } = new();
        public IUnit Owner => owner.Get();

        public UnitId Id { get; private set; }
        public bool IsSpawned { get; private set; }
        public bool IsArriving { get; private set; }
        public bool IsDeparting { get; private set; }
        public bool IsAlive => IsSpawned && (health == null || !health.IsDead);

        private void Awake()
        {
            var foundComponents = new List<IUnitComponent>();
            GetComponents(foundComponents);
            foreach(var component in foundComponents)
            {
                Register(component);
            }
            GetComponentsInChildren(true, arrivals);
            GetComponentsInChildren(true, departures);

            foreach(var component in components)
            {
                component.Initialize(this);
            }
            foreach(var component in components)
            {
                component.Bind();
            }

            TryGet(out health);

        }


        private void Start()
        {
            if(spawnOnStart && !IsSpawned) Spawn(new SpawnData(transform.position, transform.rotation, Id, Owner, faction), arriveOnStart);
        }

        private void OnDestroy()
        {
            releaseHandler = null;
            if(IsDeparting) FinishDeparture();
            else if(IsSpawned) Despawn(DespawnReason.Unloaded);
            foreach(var component in components)
            {
                component.Unbind();
            }
        }

        public void Spawn(in SpawnData data) => Spawn(in data, true);

        private void Spawn(in SpawnData data, bool arrive)
        {
            if(IsDeparting)
            {
                releaseHandler = null;
                FinishDeparture();
            }
            if (IsSpawned)
            {
                Debug.LogWarning($"[Unit]: {DisplayName} is already spawned as {Id}", this);
                return;
            }

            transform.SetPositionAndRotation(data.Position, data.Rotation);
            Id = CombatServices.Units.AssignId(data.Id);
            owner = new UnitHandle(data.Owner);
            factionOverride = data.Faction;

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
            GameEvents.Raise(this, new UnitSpawnedEvent(this, data));

            if(arrive) BeginArrival(data.ArrivalTags != null && data.ArrivalTags.Count > 0 ? data.ArrivalTags : arrivalTags);
            else GameEvents.Raise(this, new UnitArrivedEvent(this));
        }

        public void Despawn(DespawnReason reason) => Despawn(reason, null);

        public void Despawn(DespawnReason reason, Action<Unit> onReleased)
        {
            if(!IsSpawned)
            {
                if(IsDeparting) releaseHandler = onReleased ?? releaseHandler;
                else onReleased?.Invoke(this);
                return;
            }
            if(IsArriving) StopArrival();
            IsSpawned = false;

            GameEvents.Raise(this, new UnitDespawnedEvent(this, reason));

            for(int i = components.Count - 1; i >= 0; i--)
            {
                components[i].OnDespawned(reason);
            }

            CombatServices.Units.Remove(this);

            Id = UnitId.None;
            owner = default;
            factionOverride = null;
            releaseHandler = onReleased;

            if(reason == DespawnReason.SessionEnd || reason == DespawnReason.Unloaded)
            {
                Release();
                return;
            }

            float duration = 0f;
            for(int i = 0; i < departures.Count; i++)
            {
                duration = Mathf.Max(duration, departures[i].Begin(this, reason));
            }
            if(duration <= 0f)
            {
                Release();
                return;
            }
            IsDeparting = true;
            departureLeft = duration;
            GameEvents.Raise(this, new UnitDepartingEvent(this, reason, duration));
        }

        public void FinishDeparture()
        {
            if(!IsDeparting) return;
            for(int i = 0; i < departures.Count; i++)
            {
                departures[i].Stop();
            }
            Release();
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
            int index = CapabilitySlot<T>.Index;
            var cache = found;
            if(index < cache.Length)
            {
                var cached = cache[index];
                if(cached != null)
                {
                    component = ReferenceEquals(cached, notFound) ? null : (T)cached;
                    return component != null;
                }
            }
            return Resolve(index, out component);
        }


        public void Tick(float deltaTime)
        {
            if(IsArriving)
            {
                arrivalLeft -= deltaTime;
                if(arrivalLeft <= 0f) EndArrival();
            }
            if(IsDeparting)
            {
                departureLeft -= deltaTime;
                if(departureLeft <= 0f) Release();
                return;
            }
            for(int i = 0; i < tickables.Count && IsSpawned; i++)
            {
                tickables[i].Tick(deltaTime);
            }
        }


        private void BeginArrival(IReadOnlyList<CombatTag> chosen)
        {
            heldArrivalTags.Clear();
            for(int i = 0; chosen != null && i < chosen.Count; i++)
            {
                if(chosen[i] != null) heldArrivalTags.Add(chosen[i]);
            }

            float duration = 0f;
            for(int i = 0; i < arrivals.Count; i++)
            {
                duration = Mathf.Max(duration, arrivals[i].Begin(this, heldArrivalTags));
            }
            if(duration <= 0f)
            {
                heldArrivalTags.Clear();
                GameEvents.Raise(this, new UnitArrivedEvent(this));
                return;
            }

            for(int i = 0; i < heldArrivalTags.Count; i++)
            {
                Tags.Add(heldArrivalTags[i]);
            }
            IsArriving = true;
            arrivalLeft = duration;
            GameEvents.Raise(this, new UnitArrivingEvent(this, heldArrivalTags, duration));
        }

        private void EndArrival()
        {
            ReleaseArrivalTags();
            GameEvents.Raise(this, new UnitArrivedEvent(this));
        }

        private void StopArrival()
        {
            for(int i = 0; i < arrivals.Count; i++)
            {
                arrivals[i].Stop();
            }
            ReleaseArrivalTags();
        }

        private void ReleaseArrivalTags()
        {
            IsArriving = false;
            for(int i = 0; i < heldArrivalTags.Count; i++)
            {
                Tags.Remove(heldArrivalTags[i]);
            }
            heldArrivalTags.Clear();
        }

        private void Release()
        {
            IsDeparting = false;
            tickSource?.Unregister(this);
            tickSource = null;

            Tags.Clear();
            GameEvents.Raise(this, new UnitReleasedEvent(this));
            for(int i = components.Count - 1; i >= 0; i--)
            {
                components[i].OnReleased();
            }
            Events.EndLife(this, this);

            var handler = releaseHandler;
            releaseHandler = null;
            handler?.Invoke(this);
        }

        private bool Resolve<T>(int index, out T component) where T : class
        {
            component = null;
            for(int i = 0; i < registered.Count; i++)
            {
                if(registered[i] is T match)
                {
                    component = match;
                    break;
                }
            }
            if(component == null) TryGetComponent(out component);

            if(index >= found.Length)
            {
                var grown = new object[Mathf.Max(index + 1, found.Length * 2)];
                Array.Copy(found, grown, found.Length);
                found = grown;
            }
            found[index] = (object)component ?? notFound;
            return component != null;
        }

        private void Register(object component)
        {
            if(component == null || registered.Contains(component)) return;
            registered.Add(component);
            Array.Clear(found, 0, found.Length);
            if(component is IUnitComponent c) components.Add(c);
            if(component is ITickable t) tickables.Add(t);
        }

    }
}
