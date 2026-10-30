using System;

namespace CG.CombatFramework
{
    public sealed class ListenerList<T> where T : class
    {
        private T[] items = Array.Empty<T>();

        public int Count => items.Length;
        public ReadOnlySpan<T> Items => items;

        public void Add(T listener)
        {
            if(listener == null || Array.IndexOf(items, listener) >= 0) return;
            var grown = new T[items.Length + 1];
            Array.Copy(items, grown, items.Length);
            grown[items.Length] = listener;
            items = grown;
        }

        public bool Remove(T listener)
        {
            int found = listener == null ? -1 : Array.IndexOf(items, listener);
            if(found < 0) return false;
            var shrunk = items.Length == 1 ? Array.Empty<T>() : new T[items.Length - 1];
            Array.Copy(items, 0, shrunk, 0, found);
            Array.Copy(items, found + 1, shrunk, found, items.Length - found - 1);
            items = shrunk;
            return true;
        }

        public void Clear() => items = Array.Empty<T>();
    }
}
