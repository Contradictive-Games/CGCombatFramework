using System;
using System.Text;
using System.Threading;
using UnityEngine;

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

    internal static class EventSlots
    {
        private static int count;

        internal static int Next() => Interlocked.Increment(ref count) - 1;
    }

    internal static class EventSlot<T> where T : struct, ICombatEvent
    {
        internal static readonly int Index = EventSlots.Next();
    }

    public sealed class EventHandlerException : Exception
    {
        public Type EventType { get; }

        public EventHandlerException(Type eventType, string handler, Exception inner)
            : base($"[EventBus] A {eventType.Name} handler ({handler}) threw. The other handlers still ran", inner)
        {
            EventType = eventType;
        }
    }

    internal abstract class HandlerList
    {
        internal abstract int Count { get; }
        internal abstract Type EventType { get; }
        internal abstract string Newest { get; }

        internal static string Describe(Delegate handler) => $"{handler.Method.DeclaringType?.Name}.{handler.Method.Name}";
    }

    internal sealed class HandlerList<T> : HandlerList where T : struct, ICombatEvent
    {
        internal GameEventHandler<T>[] Handlers = Array.Empty<GameEventHandler<T>>();

        internal override int Count => Handlers.Length;
        internal override Type EventType => typeof(T);
        internal override string Newest => Handlers.Length > 0 ? Describe(Handlers[Handlers.Length - 1]) : null;
    }

    public sealed class EventBus
    {
        private HandlerList[] slots = Array.Empty<HandlerList>();
        private int[] lastLife;
        private bool lived;

        public EventBus(bool forwardsToGlobal = true) => ForwardsToGlobal = forwardsToGlobal;

        public bool ForwardsToGlobal { get; }
        public int SubscriberCount { get; private set; }

        public void Subscribe<T>(GameEventHandler<T> handler) where T : struct, ICombatEvent
        {
            if(handler == null) return;
            var list = ListFor<T>();
            var old = list.Handlers;
            var grown = new GameEventHandler<T>[old.Length + 1];
            Array.Copy(old, grown, old.Length);
            grown[old.Length] = handler;
            list.Handlers = grown;
            SubscriberCount++;
        }

        public void Unsubscribe<T>(GameEventHandler<T> handler) where T : struct, ICombatEvent
        {
            if(handler == null) return;
            int index = EventSlot<T>.Index;
            if(index >= slots.Length || !(slots[index] is HandlerList<T> list)) return;
            var old = list.Handlers;
            int found = Array.LastIndexOf(old, handler);
            if(found < 0) return;
            var shrunk = old.Length == 1 ? Array.Empty<GameEventHandler<T>>() : new GameEventHandler<T>[old.Length - 1];
            Array.Copy(old, 0, shrunk, 0, found);
            Array.Copy(old, found + 1, shrunk, found, old.Length - found - 1);
            list.Handlers = shrunk;
            SubscriberCount--;
        }

        public void Raise<T>(in T evt) where T : struct, ICombatEvent
        {
            int index = EventSlot<T>.Index;
            var current = slots;
            if(index >= current.Length || !(current[index] is HandlerList<T> list)) return;
            var handlers = list.Handlers;
            for(int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](in evt);
                }
                catch(Exception exception)
                {
                    Debug.LogException(new EventHandlerException(typeof(T), HandlerList.Describe(handlers[i]), exception));
                }
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal void EndLife(IUnit owner, UnityEngine.Object context)
        {
            string grown = GrowthSinceLastLife();
            if(grown == null) return;
            Debug.LogWarning($"[EventBus] {owner?.DisplayName} ended a life with more handlers on its bus than its last life did: {grown}. Something subscribes to this unit every life and never unsubscribes. Subscribe in Bind or OnSpawned, and unsubscribe in Unbind or OnDespawned", context);
        }

        internal string GrowthSinceLastLife()
        {
            var current = slots;
            if(current.Length == 0)
            {
                lived = true;
                return null;
            }
            if(lastLife == null || lastLife.Length < current.Length)
            {
                var resized = new int[current.Length];
                if(lastLife != null) Array.Copy(lastLife, resized, lastLife.Length);
                lastLife = resized;
            }

            StringBuilder text = null;
            for(int i = 0; i < current.Length; i++)
            {
                int count = current[i] != null ? current[i].Count : 0;
                if(lived && count > lastLife[i])
                {
                    text = text == null ? new StringBuilder() : text.Append(", ");
                    text.Append(current[i].EventType.Name).Append(' ').Append(lastLife[i]).Append(" → ").Append(count);
                    text.Append(" (newest: ").Append(current[i].Newest).Append(')');
                }
                lastLife[i] = count;
            }
            lived = true;
            return text?.ToString();
        }

        internal void Clear()
        {
            slots = Array.Empty<HandlerList>();
            SubscriberCount = 0;
            lastLife = null;
            lived = false;
        }

        private HandlerList<T> ListFor<T>() where T : struct, ICombatEvent
        {
            int index = EventSlot<T>.Index;
            if(index < slots.Length && slots[index] is HandlerList<T> existing) return existing;
            if(index >= slots.Length)
            {
                var grown = new HandlerList[Mathf.Max(index + 1, slots.Length * 2)];
                Array.Copy(slots, grown, slots.Length);
                slots = grown;
            }
            var list = new HandlerList<T>();
            slots[index] = list;
            return list;
        }
    }
}