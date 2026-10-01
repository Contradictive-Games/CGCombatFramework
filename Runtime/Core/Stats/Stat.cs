using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public sealed class Stat : IStat
    {
        private readonly List<StatModifier> modifiers = new();
        private float baseValue;
        private float cachedValue;
        private bool isDirty;

        public StatType Type { get; }
        public float BaseValue => baseValue;

        public event Action<IReadonlyStat> OnChanged;


        public float Value
        {
            get
            {
                if (isDirty)
                {
                    cachedValue = CalculateValue();
                    isDirty = false;
                }
                return cachedValue;
            }
        }

        public Stat(StatType type, float baseValue)
        {
            Type = type;
            this.baseValue = baseValue;
        }

        public void SetBaseValue(float value)
        {
            if (baseValue.Equals(value)) return;
            baseValue = value;
            MarkDirty();
        }



        public void AddModifier(StatModifier modifier)
        {
            if (modifier == null) return;
            modifiers.Add(modifier);
            MarkDirty();
        }


        public bool TryRemoveModifier(StatModifier modifier)
        {
            if (!modifiers.Remove(modifier)) return false;
            MarkDirty();
            return true;
        }


        public int RemoveAllModifiersFromSource(object source)
        {
            int removed = 0;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if(!Equals(modifiers[i].Source, source)) continue;
                modifiers.RemoveAt(i);
                removed++;
            }
            if (removed > 0) MarkDirty();
            return removed;
        }


        public void Reset(float newBaseValue)
        {
            modifiers.Clear();
            baseValue = newBaseValue;
            MarkDirty();
        }


        private void MarkDirty()
        {
            isDirty = true;
            OnChanged?.Invoke(this);
        }


        private float CalculateValue()
        {
            float flat = baseValue;
            float percentAdd = 0f;
            for(int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                if(m.Type == StatModifierType.Flat) flat += m.Value;
                else if(m.Type == StatModifierType.PercentAdd) percentAdd += m.Value;
            }
            float result = flat * (1f + percentAdd);
            for(int i =0;i<modifiers.Count; i++)
            {
                if(modifiers[i].Type == StatModifierType.PercentMult) result *= 1f +modifiers[i].Value;
            }
            return Type != null ? Type.Clamp(result) : result;
        }

    }
}