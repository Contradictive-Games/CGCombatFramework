using System;

namespace CG.RGS.CombatFramework
{
    public sealed class Resource
    {
        public ResourceType Type;
        public float CurrentValue { get; private set; }
        public Stat Max;
        public float MaxValue => Max.MaxValue;

        public event Action<Resource> OnChange;
        public event Action<Resource> OnDeplete;

        public bool TrySpend(float amount, bool percent = false)
        {
            return true;
        }


        public void Restore(float amount)
        {
            
        }
         
    }
}
