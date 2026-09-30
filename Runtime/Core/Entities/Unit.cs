using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    [AddComponentMenu("CG Combat Framework/Unit")]
    public class Unit : MonoBehaviour, IUnit, ITickable
    {
        [SerializeField] private string displayName;
        [SerializeField] private Faction faction;
        [SerializeField] private TagContainer startingTags;

        private readonly Dictionary<Type, object> lookup = new();
        private readonly List<object> registered = new();
        private readonly List<IUnitComponent> components = new();
        private readonly List<ITickable> tickables = new();
        private ITickSource tickSource;
        private bool initialized;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Transform Transform => transform;
        public Faction Faction => faction;
        public TagContainer Tags => startingTags;
        public IUnit Owner { get; private set; }
        
        //temp
        public bool IsAlive => false;

        public event Action<Unit> Initiailized;

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
                component.Bind();
            }

            initialized = true;
            Initiailized?.Invoke(this);
        }


        private void OnEnable()
        {
            tickSource = CombatRuntime.TickSource;
            tickSource.Register(this);
        }
        private void OnDisable()
        {
            tickSource?.Unregister(this);
            tickSource = null;
        }


        private void OnDestroy()
        {
            foreach(var component in components)
            {
                component.Unbind();
            }
        }


        public void AddUnitComponent(object component)
        {
            Register(component);
            if(initialized && component is IUnitComponent c)
            {
                c.Initialize(this);
                c.Bind();
            }
        }


        public void SetOwner(IUnit owner) => Owner = owner;


        public bool TryGet<T>(out T component) where T : class
        {
            if(lookup.TryGetValue(typeof(T), out var cached))
            {
                component = (T)cached;
                return true;
            }

            foreach(var candidate in registered)
            {
                if(candidate is T match)
                {
                    lookup[typeof(T)] = match;
                    component = match;
                    return true;
                }
            }

            if(TryGetComponent(out component))
            {
                lookup[typeof(T)] = component;
                return true;
            }

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
            if(component is IUnitComponent c) components.Add(c);
            if(component is ITickable t) tickables.Add(t);
        }

    }
}
