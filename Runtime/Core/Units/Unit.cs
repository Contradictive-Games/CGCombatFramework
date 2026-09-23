using System;
using UnityEngine;

namespace CG.RGS.CombatFramework
{
    public class Unit : MonoBehaviour, IEffectable, IDamageable, ITickable
    {
        public ResourceSet Resources { get; private set; }
        public StatBlock Stats { get; private set; }
        public StatusEffectsManager EffectsManager { get; private set; }

        public Resource Health { get; }
        public bool IsDead => Health.CurrentValue <= 0f;
        public event Action<Unit> Died;


        public void ApplyEffect(StatusEffectContext ctx)
        {
            if(EffectsManager == null)
            {
                
            }
        }

        public void Damage(DamageContext ctx)
        {
            throw new System.NotImplementedException();
        }



        public void Tick(float deltaTime)
        {
            Resources.Tick(deltaTime);
            EffectsManager.Tick(deltaTime);
        }
        
    }
}