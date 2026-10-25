using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [Serializable, TypeMenu("While Condition => effect")]
    [BlockInfo("Keeps another effect on the host while its conditions pass (checked when applied and every tick) and removes it when they stop.", Example = "Berserk: while Health is below 30%, apply Frenzy")]
    public sealed class WhileBehavior : IStatusEffectBehavior
    {
        [SerializeReference, SelectType, Tooltip("Checked on the host (source && target are both host)")] 
        private ICondition[] conditions = Array.Empty<ICondition>();
        [SerializeField, Tooltip("Applied while conditions pass")]
        private StatusEffectDefinition effect;
        
        private Dictionary<StatusEffectInstance, StatusEffectHandle> granted;
        public Dictionary<StatusEffectInstance, StatusEffectHandle> Granted => granted ??= new();

        public void OnApplied(StatusEffectInstance owner) => Check(owner);
        public void OnTick(StatusEffectInstance owner) => Check(owner);
        public void OnRemoved(StatusEffectInstance owner, EffectRemoveReason reason) => Revoke(owner);

        private void Check(StatusEffectInstance owner)
        {
            if(effect == null) return;
            var host = owner.Host;
            var ctx = new EffectContext(host, owner.Definition, host, default, owner.Power, owner.Stacks);
            bool want = conditions.All(in ctx);
            bool has = Granted.TryGetValue(owner, out var handle) && handle.IsValid;

            if(want && !has && host.TryGet<IStatusEffects>(out var effects))
            {
                Granted[owner] = effects.Apply(effect, owner.SourceUnit ?? host, 1, owner.Power);
            }
            else if(!want && has)
            {
                Revoke(owner);
            }
        }

        private void Revoke(StatusEffectInstance owner)
        {
            if(Granted.Remove(owner, out var handle) && owner.Host.TryGet<IStatusEffects>(out var effects))
            {
                effects.Remove(handle, EffectRemoveReason.Expired);
            }
        }
    }
}
