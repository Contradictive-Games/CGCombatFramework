using System;

namespace CG.CombatFramework
{
    public interface IReadonlyStat
    {
        public StatType Type { get; }
        public float BaseValue { get; }

        public float Value { get; }
        
        public event Action<IReadonlyStat> OnChanged;
    }
}