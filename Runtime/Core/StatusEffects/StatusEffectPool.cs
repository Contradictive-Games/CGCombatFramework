using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public static class StatusEffectPool
    {
        private static readonly Stack<StatusEffectInstance> free = new();

        public static int FreeCount => free.Count;
        
        public static StatusEffectInstance Rent() => free.Count > 0 ? free.Pop() : new StatusEffectInstance();

        public static void Return(StatusEffectInstance instance)
        {
            instance.Clear();
            free.Push(instance);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            free.Clear();
        }
    }
}