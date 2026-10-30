using System.Collections.Generic;

namespace CG.CombatFramework
{
    public sealed class Stat : IStat
    {
        private readonly List<StatModifier> modifiers = new();
        private readonly ListenerList<IStatListener> listeners = new();
        private readonly IUnit owner;
        private float baseValue;
        private float cachedValue;
        private bool isDirty;

        public StatType Type { get; }
        public float BaseValue => baseValue;


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

        public Stat(StatType type, float baseValue, IUnit owner = null)
        {
            Type = type;
            this.baseValue = baseValue;
            this.owner = owner;
            cachedValue = CalculateValue();
        }

        public void AddListener(IStatListener listener)
        {
            if (isDirty)
            {
                cachedValue = CalculateValue();
                isDirty = false;
            }
            listeners.Add(listener);
        }

        public void RemoveListener(IStatListener listener) => listeners.Remove(listener);

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
            if (owner == null && listeners.Count == 0)
            {
                isDirty = true;
                return;
            }
            float previous = cachedValue;
            float current = cachedValue = CalculateValue();
            if (current == previous) return;

            var evt = new StatChangedEvent(owner, this, previous, current);
            var items = listeners.Items;
            for (int i = 0; i < items.Length; i++)
            {
                items[i].OnStatChanged(in evt);
            }
            if (owner != null) GameEvents.Raise(owner, in evt);
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