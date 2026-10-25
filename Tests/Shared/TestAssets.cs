using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Tests
{
    /// <summary>Creates throwaway assets and destroys them all on Dispose.</summary>
    public sealed class TestAssets : IDisposable
    {
        private readonly List<Object> created = new();

        public T Make<T>(string name = null) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name ?? typeof(T).Name;
            created.Add(asset);
            return asset;
        }

        public StatType Stat(string name, float defaultValue = 0f)
        {
            var stat = Make<StatType>(name);
            stat.DisplayName = name;
            stat.DefaultValue = defaultValue;
            return stat;
        }

        public CombatTag Tag(string name, CombatTag parent = null)
        {
            var tag = Make<CombatTag>(name);
            tag.DisplayName = name;
            tag.Parent = parent;
            return tag;
        }

        public StatusEffectDefinition Effect(string name, float duration = 4f, float tickInterval = 0f,
            EffectStackingMode stacking = EffectStackingMode.Refresh, int maxStacks = 1, params IStatusEffectBehavior[] behaviors)
        {
            var effect = Make<StatusEffectDefinition>(name);
            effect.DisplayName = name;
            effect.Duration = duration;
            effect.TickInterval = tickInterval;
            effect.Stacking = stacking;
            effect.MaxStacks = maxStacks;
            effect.Behaviors = behaviors;
            return effect;
        }

        public void Track(Object obj) => created.Add(obj);

        public void Dispose()
        {
            foreach (var obj in created)
                if (obj != null) Object.DestroyImmediate(obj);
            created.Clear();
        }

        /// <summary>
        /// Sets a private [SerializeField] the way the inspector would. Test-only:
        /// the runtime code stays free of setters that exist just for tests.
        /// </summary>
        public static void Set(object target, string field, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }
    }
}
