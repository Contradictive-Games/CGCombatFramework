using System;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("Stat Modifiers")]
    [BlockInfo("Adds stat modifiers to the host while the effect is active, optionally multiplied by stacks.", Example = "War Cry: +20% Damage Multiplier for 8s")]
    public sealed class StatModifiersBehavior : IStatusEffectBehavior
    {
        [Serializable]
        internal struct Entry
        {
            public StatType Stat;
            public float Value;
            public StatModifierType Type;
            public bool PerStack;
        }

        [SerializeField] private Entry[] modifiers = Array.Empty<Entry>();

        public void OnApplied(StatusEffectInstance effect) => AddAll(effect);

        public void OnStacksChanged(StatusEffectInstance effect, int previousStacks)
        {
            RemoveAll(effect);
            AddAll(effect);
        }

        public void OnRemoved(StatusEffectInstance effect, EffectRemoveReason reason) => RemoveAll(effect);

        private void AddAll(StatusEffectInstance effect)
        {
            if(!effect.Host.TryGet<IStatProvider>(out var stats)) return;
            for(int i = 0; i < modifiers.Length; i++)
            {
                var entry = modifiers[i];
                if(entry.Stat == null) continue;
                float value = entry.PerStack ? entry.Value  * effect.Stacks : entry.Value;
                stats.GetOrCreate(entry.Stat).AddModifier(new StatModifier(value, entry.Type, (effect, this)));
            }
        }

        private void RemoveAll(StatusEffectInstance effect)
        {
            if(effect.Host.TryGet<IStatProvider>(out var stats)) stats.RemoveAllModifiersFromSource((effect, this));
        }
    }
}
