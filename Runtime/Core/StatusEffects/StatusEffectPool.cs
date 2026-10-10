using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    internal static class StatusEffectPool
    {
        private static readonly Stack<StatusEffectInstance> free = new();

        internal static int FreeCount => free.Count;
        
        internal static StatusEffectInstance Rent() => free.Count > 0 ? free.Pop() : new StatusEffectInstance();

        internal static void Return(StatusEffectInstance instance)
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