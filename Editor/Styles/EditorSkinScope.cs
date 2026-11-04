using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal readonly struct EditorSkinScope : IDisposable
    {
        private static readonly FieldInfo current = Find();

        private readonly GUISkin previous;
        private readonly bool active;

        private EditorSkinScope(GUISkin previous)
        {
            this.previous = previous;
            active = true;
        }

        public static EditorSkinScope Begin()
        {
            if(current == null) return default;
            var scope = new EditorSkinScope((GUISkin)current.GetValue(null));
            current.SetValue(null, Skin());
            return scope;
        }

        public static GUISkin Skin() => EditorGUIUtility.GetBuiltinSkin(EditorGUIUtility.isProSkin ? EditorSkin.Scene : EditorSkin.Inspector);

        internal static GUISkin Current => (GUISkin)current?.GetValue(null);

        public void Dispose()
        {
            if(active) current.SetValue(null, previous);
        }

        private static FieldInfo Find()
        {
            var field = typeof(GUISkin).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(GUISkin) ? field : null;
        }
    }
}
