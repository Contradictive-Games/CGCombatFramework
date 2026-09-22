using System;
using UnityEngine;

namespace CG.RGS.CombatFramework
{
    public class Resource
    {
        public ResourceType Type;
        public float CurrentValue { get; private set; }
        public Stat MaxValue;

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
