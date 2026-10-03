using System;
using System.Collections.Generic;

namespace CG.CombatFramework
{
    public static class GameEvents
    {
        public static void Raise<T>(IUnit unit, in T evt) where T : struct
        {
            unit?.Events.Raise(in evt);
            CombatServices.Events.Raise(in evt);
        }
    }
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> handlers = new();

        public void Subscribe<T>(GameEventHandler<T> handler) where T : struct
        {
            if(handler == null) return;
            handlers.TryGetValue(typeof(T), out var existing);
            handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(GameEventHandler<T> handler) where T : struct
        {
            if(handler == null || !handlers.TryGetValue(typeof(T), out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if(remaining == null) handlers.Remove(typeof(T));
            else handlers[typeof(T)] = remaining;
        }


        public void Raise<T>(in T evt) where T : struct
        {
            if(handlers.TryGetValue(typeof(T), out var handler)) ((GameEventHandler<T>)handler)(in evt);
        }

        public void Clear() => handlers.Clear();
    }
}