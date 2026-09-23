using System;
using System.Collections.Generic;

namespace CG.RGS.CombatFramework
{
    public sealed class StatusEffectsManager
    {
        private List<StatusEffect> activeEffects;

        private Unit owner;

        public event Action<StatusEffect> OnApplied;
        public event Action<StatusEffect> OnRemoved;


        public StatusEffectsManager(Unit owner)
        {
            this.owner = owner;
        }


        public void Tick(float deltaTime)
        {
            
        }
    }
}