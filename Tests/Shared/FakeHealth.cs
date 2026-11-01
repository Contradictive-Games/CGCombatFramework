using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    /// <summary>Records every request, subtracts from a plain float, and dies at zero. No pipeline. Raises and notifies like HealthComponent.</summary>
    public sealed class FakeHealth : IHealth
    {
        public readonly List<DamageRequest> Received = new();
        public float Current;
        public IUnit Owner;

        private readonly ListenerList<IHealthListener> listeners = new();

        public FakeHealth(float current = 100f) => Current = current;

        public IReadonlyResource Resource => null;
        public bool IsDead { get; private set; }
        public DeathInfo? LastDeath { get; private set; }

        public void AddListener(IHealthListener listener) => listeners.Add(listener);
        public void RemoveListener(IHealthListener listener) => listeners.Remove(listener);

        public DamageResult ApplyDamage(in DamageRequest request)
        {
            if (IsDead) return DamageResult.Ignored(in request);
            Received.Add(request);
            float dealt = Mathf.Min(Current, request.Amount);
            Current -= dealt;
            bool killing = Current <= 0f;
            var result = new DamageResult(in request, HitResult.Hit, dealt, 0f, request.Amount - dealt, false, killing);
            var source = request.Context.SourceUnit;
            GameEvents.Raise(Owner, new DamageReceivedEvent(Owner, source, in result));
            GameEvents.Raise(source, new DamageDealtEvent(source, Owner, in result));
            if (killing) Kill(in request.Context);
            return result;
        }

        public HealResult ApplyHeal(float amount, in EffectContext ctx)
        {
            if (IsDead) return HealResult.Ignored();
            Current += amount;
            var result = new HealResult(amount, 0f, false, in ctx);
            var source = ctx.SourceUnit;
            GameEvents.Raise(Owner, new HealReceivedEvent(Owner, source, in result));
            GameEvents.Raise(source, new HealDealtEvent(source, Owner, in result));
            return result;
        }

        public void Kill(in EffectContext ctx)
        {
            if (IsDead) return;
            IsDead = true;
            Current = 0f;
            var info = new DeathInfo(Owner, ctx.SourceUnit, in ctx, null);
            LastDeath = info;
            var died = new DiedEvent(in info);
            GameEvents.Raise(Owner, in died);
            var items = listeners.Items;
            for (int i = 0; i < items.Length; i++)
            {
                items[i].OnDied(in died);
            }
            if (info.Killer != null && !ReferenceEquals(info.Killer, Owner)) GameEvents.Raise(info.Killer, new KilledUnitEvent(in info));
        }

        public void Revive(float normalized = 1f, in EffectContext ctx = default)
        {
            if (!IsDead) return;
            IsDead = false;
            LastDeath = null;
            Current = 100f * normalized;
            var revived = new RevivedEvent(Owner, ctx.SourceUnit, Current);
            GameEvents.Raise(Owner, in revived);
            var items = listeners.Items;
            for (int i = 0; i < items.Length; i++)
            {
                items[i].OnRevived(in revived);
            }
        }
    }
}