using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public static class CombatScheduler
    {
        private struct Pending
        {
            public int Key;
            public float Due;
            public IEmission[] Emissions;
            public DeliveryContext Delivery;
            public IPayload[] Payloads;
            public EffectContext Effect;

            public IUnit Source => Emissions != null ? Delivery.Effect.SourceUnit : Effect.SourceUnit;
        }

        private static readonly List<Pending> pending = new();
        private static readonly List<Pending> cancelling = new();
        private static int nextKey;

        internal static int Count => pending.Count;

        public static int Schedule(float delay, IEmission[] emissions, in DeliveryContext ctx)
        {
            if (emissions == null) return 0;
            EnsureTicking();
            int key = ++nextKey;
            float wait = Mathf.Max(0f, delay);
            pending.Add(new
                Pending
            {
                Key = key,
                Due = CombatServices.Time + wait,
                Emissions = emissions,
                Delivery = ctx
            });
            var source = ctx.Effect.SourceUnit;
            GameEvents.Raise(source, new ScheduledEvent(source, key, wait, ctx.Effect.Cause, ctx.Origin, ctx.AimPoint));
            return key;
        }

        public static int Schedule(float delay, IPayload[] payloads, in EffectContext ctx)
        {
            if (payloads == null || payloads.Length == 0) return 0;
            EnsureTicking();
            int key = ++nextKey;
            float wait = Mathf.Max(0f, delay);
            pending.Add(new Pending { Key = key, Due = CombatServices.Time + wait, Payloads = payloads, Effect = ctx });
            var source = ctx.SourceUnit;
            GameEvents.Raise(source, new ScheduledEvent(source, key, wait, ctx.Cause, ctx.Hit.Point, ctx.Hit.Point));
            return key;
        }


        public static void Clear()
        {
            if (pending.Count == 0) return;
            cancelling.AddRange(pending);
            pending.Clear();
            for (int i = 0; i < cancelling.Count; i++)
            {
                var source = cancelling[i].Source;
                GameEvents.Raise(source, new ScheduleCancelledEvent(source, cancelling[i].Key, ScheduleCancelReason.Cleared));
            }
            cancelling.Clear();
        }

        private static void EnsureTicking() => _ = CombatServices.TickSource;

        internal static void RunDue()
        {
            float now = CombatServices.Time;
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
                var source = due.Source;
                GameEvents.Raise(source, new ScheduleResolvedEvent(source, due.Key));
                if (due.Emissions != null) due.Emissions.EmitAll(in due.Delivery);
                else Payloads.ApplyAll(due.Payloads, in due.Effect);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            pending.Clear();
            cancelling.Clear();
            nextKey = 0;
        }
    }
}
