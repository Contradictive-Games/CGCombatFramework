using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public sealed class Stat : IStat
    {
        private readonly List<StatModifier> modifiers = new();
        private float baseValue;

        public StatType Type { get; }
        public float BaseValue => baseValue;
        public float MinValue { get; private set; }
        public float MaxValue { get; private set; }

        public event Action<IReadonlyStat> OnChanged;

        private bool isDirty;


        public Stat(StatType type, float baseValue)
        {
            Type = type;
            this.baseValue = baseValue;
            MinValue = type.MinValue;
            MaxValue = type.MaxValue;
        }
        
        private float cachedValue;
        public float Value
        {
            get
            {
                if(isDirty) {
                    cachedValue = CalculateValue();
                    isDirty = false;
                }
                return cachedValue;
            }
        }


        public void AddModifier(StatModifier modifier)
        {
            if(modifier == null) return;
            modifiers.Add(modifier);
            MarkDirty();
        }


        public bool TryRemoveModifier(StatModifier modifier)
        {
            if(!modifiers.Remove(modifier)) return false;
            MarkDirty();
            return true;
        }


        public int RemoveAllModifiersFromSource(object source)
        {
            int removed = modifiers.RemoveAll(m => Equals(m.Source, source));
            if(removed > 0) MarkDirty();
            return removed;
        }


        private void MarkDirty()
        {
            isDirty = true;
            OnChanged?.Invoke(this);
        }


        private float CalculateValue()
        {
            float old = Value;

            float flat = baseValue;
            float percentAdd = 0f;
            float result;

            for(int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                if(m.Type == StatModifierType.Flat) flat += m.Value;
                else if(m.Type == StatModifierType.PercentAdd) percentAdd += m.Value;
            }
            result = flat * (1f + percentAdd);
            for(int i = 0; i < modifiers.Count; i++)
            {
                if(modifiers[i].Type == StatModifierType.PercentMult) result *= 1f + modifiers[i].Value;
            }

            float clamped = Clamp(result);
            if(!old.Equals(clamped)) OnChanged?.Invoke(this);
            return clamped;
        }


        private float Clamp(float value)
        {
            return Mathf.Clamp(value, MinValue, MaxValue);
        }
    }
}