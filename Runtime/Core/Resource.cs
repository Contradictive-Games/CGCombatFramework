using System;
using UnityEngine;

namespace CG.RGS.CombatFramework
{
    public class Resource
    {
        public event Action OnTick;
        public event Action OnDeplete;

        public string Name { get; protected set; }
        public Color Color { get; protected set; }

        public int MaxValue { get; protected set; }
        public int CurrentValue { get; protected set; }

        public float CurrentPercent => CurrentValue / MaxValue;
        public string CurrentPercentString => (CurrentValue / MaxValue * 100f).ToString("F0");

        public bool RegenerationEnabled { get; private set; }
        public float RegenerationSpeed { get; private set; }
        public int RegenerationAmount { get; private set; }

        private float lastTimeTicked;
        private float updateTimer;

        public virtual void TickResource(float deltaTime)
        {
            if(!RegenerationEnabled) return;

            lastTimeTicked = deltaTime;
            
        }


        public virtual void Decrement(int amount)
        {
            
        }

        public virtual void Increment(int amount)
        {
            
        }


        public void SetMax(int value)
        {
            
        }

        public void SetCurrent(int value)
        {
            
        }

        public void SetAll(int max, int cur)
        {
            
        }

         
    }
}
