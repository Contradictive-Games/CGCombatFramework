using System;

namespace CG.RGS.CombatFramework
{
    public sealed class Resource
    {
        public ResourceType Type;
        public float CurrentValue { get; private set; }
        public Stat Max;
        public float MaxValue => Max.MaxValue;

        public event Action<Resource, float> OnChange;
        public event Action<Resource> OnDeplete;

        private float tickTimer;
        
        public void Tick(float deltaTime)
        {
            
        }

        public bool TrySpend(float amount, bool percent = false)
        {
            return true;
        }


        public void Restore(float amount)
        {
            
        }

        public void Reduce(float amount)
        {
            
        }

        public void Set(float value)
        {
            
        }
         
    }
}
