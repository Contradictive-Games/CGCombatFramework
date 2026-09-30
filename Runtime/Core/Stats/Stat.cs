using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    /// <summary>
    /// A stat that uses lazy recalculation
    /// </summary>
    public sealed class Stat : IStat
    {
        private readonly List<StatModifier> modifiers = new();
        private float baseValue;

        public StatType Type { get; }
        public float Value { get; private set; }
        public float MinValue { get; private set; }
        public float MaxValue { get; private set; }

        public event Action<IReadonlyStat> OnChanged;

        private bool isDirty;


        public Stat(StatType type, float baseValue)
        {
            Type = type;
            this.baseValue = baseValue;
            MinValue = float.MinValue;
            MaxValue = float.MaxValue;
            Value = CalculateValue();
        }

        public Stat(StatType type, float baseValue, float minValue, float maxValue)
        {
            Type = type;
            this.baseValue = baseValue;
            MinValue = minValue;
            MaxValue = maxValue;
            Value = CalculateValue();
        }
        public Stat(StatEntry entry)
        {
            Type = entry.Type;
            baseValue = entry.BaseValue;
            MinValue = entry.MinValue;
            MaxValue = entry.MaxValue;
            Value = CalculateValue();
        }


        public float BaseValue
        {
            get
            {
                if(isDirty) baseValue = CalculateValue();
                return baseValue;
            }
        }


        public void AddModifier(StatModifier modifier)
        {
            if(modifier == null) return;
            modifiers.Add(modifier);
            isDirty = true;
        }


        public bool TryRemoveModifier(StatModifier modifier)
        {
            if(!modifiers.Remove(modifier)) return false;
            isDirty = true;
            return true;
        }


        public int RemoveAllModifiersFromSource(object source)
        {
            int removed = modifiers.RemoveAll(m => Equals(m.Source, source));
            if(removed > 0) isDirty = true;
            return removed;
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
                else if(m.Type == StatModifierType.PercentAdd) percentAdd *= m.Value;
            }
            result = flat * (1f + percentAdd);
            for(int i = 0; i < modifiers.Count; i++)
            {
                if(modifiers[i].Type == StatModifierType.PercentMult) result *= 1f + modifiers[i].Value;
            }

            float clamped = Type != null ? Clamp(result) : result;
            if(!old.Equals(clamped)) OnChanged?.Invoke(this);
            isDirty = false;
            return clamped;
        }


        private float Clamp(float value)
        {
            return Mathf.Clamp(value, MinValue, MaxValue);
        }
    }
}