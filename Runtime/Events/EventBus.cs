using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public static class GameEvents
    {
        public static void Raise<T>(IUnit unit, in T evt) where T : struct, ICombatEvent
        {
            var bus = unit?.Events;
            if(bus != null)
            {
                bus.Raise(in evt);
                if(!bus.ForwardsToGlobal) return;
            }
            CombatServices.Events.Raise(in evt);
        }

        public static void RaiseGlobal<T>(in T evt) where T : struct, ICombatEvent => CombatServices.Events.Raise(in evt);
    }
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> handlers = new();

        public EventBus(bool forwardsToGlobal = true) => ForwardsToGlobal = forwardsToGlobal;

        public bool ForwardsToGlobal { get; }

        public void Subscribe<T>(GameEventHandler<T> handler) where T : struct, ICombatEvent
        {
            if(handler == null) return;
            handlers.TryGetValue(typeof(T), out var existing);
            handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(GameEventHandler<T> handler) where T : struct, ICombatEvent
        {
            if(handler == null || !handlers.TryGetValue(typeof(T), out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if(remaining == null) handlers.Remove(typeof(T));
            else handlers[typeof(T)] = remaining;
        }


        public void Raise<T>(in T evt) where T : struct, ICombatEvent
        {
            if(handlers.TryGetValue(typeof(T), out var handler)) ((GameEventHandler<T>)handler)(in evt);
        }

        internal void Clear() => handlers.Clear();
    }
}