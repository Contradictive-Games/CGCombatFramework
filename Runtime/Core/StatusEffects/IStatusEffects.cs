using System.Collections.Generic;

namespace CG.CombatFramework
{
    public interface IStatusEffects
    {
        public IReadOnlyList<StatusEffectInstance> Active { get; }

        public StatusEffectHandle Apply(StatusEffectDefinition definition, IUnit source, int stacks = 1);
        
        public bool Remove(StatusEffectHandle handle, EffectRemoveReason reason = EffectRemoveReason.Dispelled);
        public int RemoveAll(StatusEffectDefinition definition, EffectRemoveReason reason = EffectRemoveReason.Dispelled);
        public int Dispel(CombatTag category, int max = int.MaxValue);
        
        public bool Has(StatusEffectDefinition definition);
        public int GetStacks(StatusEffectDefinition definition);
    }
}