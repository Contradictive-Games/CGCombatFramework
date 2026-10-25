using System;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal sealed class BlockClipboard : ScriptableObject
    {
        [SerializeReference] private object value;

        private static BlockClipboard stored;

        public static bool CanPasteInto(Type slotType)
        {
            return stored != null && stored.value != null && slotType != null && slotType.IsInstanceOfType(stored.value);
        }

        public static void Copy(object source)
        {
            if(source == null) return;
            if(stored != null) DestroyImmediate(stored);
            stored = Hold(source);
        }

        public static object Paste() => stored != null && stored.value != null ? Clone(stored.value) : null;

        public static object Clone(object source)
        {
            if(source == null) return null;
            var copy = Hold(source);
            var result = copy.value;
            DestroyImmediate(copy);
            return result;
        }

        private static BlockClipboard Hold(object source)
        {
            var holder = CreateInstance<BlockClipboard>();
            holder.value = source;
            var copy = Instantiate(holder);
            copy.hideFlags = HideFlags.HideAndDontSave;
            DestroyImmediate(holder);
            return copy;
        }
    }
}
