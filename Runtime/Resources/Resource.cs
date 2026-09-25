using System;

namespace CG.RGS.CombatFramework
{
    public sealed class Resource : IResource
    {
        public ResourceType ResourceType { get; private set; }
        public float CurrentValue { get; private set; }
        public Stat Max;
        public float MaxValue => Max.Value;

        public event Action<IResource, float> OnChange;
        public event Action<IResource> OnDeplete;

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
