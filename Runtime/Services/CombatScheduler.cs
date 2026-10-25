using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public static class CombatScheduler
    {
        private struct Pending
        {
            public float Due;
            public IEmission[] Emissions;
            public DeliveryContext Delivery;
            public IPayload[] Payloads;
            public EffectContext Effect;
        }

        private static readonly List<Pending> pending = new();
        private static readonly Runner runner = new();
        private static ITickSource registeredWith;
        private static float now;

        internal static int Count => pending.Count;

        public static void Schedule(float delay, IEmission[] emissions, in DeliveryContext ctx)
        {
            if (emissions == null) return;
            EnsureTicking();
            pending.Add(new
                Pending
            {
                Due = now + Mathf.Max(0f, delay),
                Emissions = emissions,
                Delivery = ctx
            });
        }

        public static void Schedule(float delay, IPayload[] payloads, in EffectContext ctx)
        {
            if (payloads == null || payloads.Length == 0) return;
            EnsureTicking();
            pending.Add(new Pending { Due = now + Mathf.Max(0f, delay), Payloads = payloads, Effect = ctx });
        }


        public static void Clear() => pending.Clear();

        private static void EnsureTicking()
        {
            var source = CombatServices.TickSource;
            if (ReferenceEquals(source, registeredWith)) return;
            registeredWith?.Unregister(runner);
            source.Register(runner);
            registeredWith = source;
        }

        private sealed class Runner : ITickable
        {
            public void Tick(float deltaTime)
            {
                now += deltaTime;
                for (int i = 0; i < pending.Count;)
                {
                    if (pending[i].Due > now)
                    {
                        i++;
                        continue;
                    }
                    var due = pending[i];
                    pending[i] = pending[pending.Count - 1];
                    pending.RemoveAt(pending.Count - 1);
                    if (due.Emissions != null) due.Emissions.EmitAll(in due.Delivery);
                    else Payloads.ApplyAll(due.Payloads, in due.Effect);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            pending.Clear();
            registeredWith = null;
            now = 0f;
        }
    }
}
