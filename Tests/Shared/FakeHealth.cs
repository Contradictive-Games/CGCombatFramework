using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    /// <summary>Records every request, subtracts from a plain float, and dies at zero. No pipeline.</summary>
    public sealed class FakeHealth : IHealth
    {
        public readonly List<DamageRequest> Received = new();
        public float Current;
        public IUnit Owner;

        public FakeHealth(float current = 100f) => Current = current;

        public IReadonlyResource Resource => null;
        public bool IsDead { get; private set; }

        public event Action<DamageResult> OnDamaged;
        public event Action<HealResult> OnHealed;
        public event Action<DeathInfo> OnDied;
        public event Action OnRevived;

        public DamageResult ApplyDamage(in DamageRequest request)
        {
            if (IsDead) return DamageResult.Ignored(in request);
            Received.Add(request);
            float dealt = Mathf.Min(Current, request.Amount);
            Current -= dealt;
            bool killing = Current <= 0f;
            var result = new DamageResult(in request, HitResult.Hit, dealt, 0f, request.Amount - dealt, false, killing);
            OnDamaged?.Invoke(result);
            if (killing) Kill(in request.Context);
            return result;
        }

        public HealResult ApplyHeal(float amount, in EffectContext ctx)
        {
            if (IsDead) return HealResult.Ignored();
            Current += amount;
            var result = new HealResult(amount, 0f, false, in ctx);
            OnHealed?.Invoke(result);
            return result;
        }

        public void Kill(in EffectContext ctx)
        {
            if (IsDead) return;
            IsDead = true;
            Current = 0f;
            OnDied?.Invoke(new DeathInfo(Owner, ctx.SourceUnit, in ctx, null));
        }

        public void Revive(float normalized = 1f)
        {
            IsDead = false;
            Current = 100f * normalized;
            OnRevived?.Invoke();
        }
    }
}