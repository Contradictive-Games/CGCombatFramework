using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum MaxChangePolicy { KeepPercent, KeepCurrent, GainTheIncrease }

    public sealed class Resource : IResource, ITickable
    {
        private readonly IReadOnlyList<IRegenRule> regen;
        private readonly MaxChangePolicy maxPolicy;
        private IReadonlyStat maxStat;
        private float baseMax;
        private float lastMax;
        private float timeSinceLoss = float.MaxValue;
        private float timeSinceGain = float.MaxValue;

        public ResourceType Type { get; }
        public float Current { get; private set; }
        public float Max => maxStat != null ? Mathf.Max(0f, maxStat.Value) : baseMax;
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsDepleted => Current <= 0f;
        public bool IsFull => Current >= Max;
        public bool RegenPaused { get; set; }

        public event Action<ResourceChange> OnChanged;
        public event Action<ResourceChange> OnDepleted;

        public Resource(ResourceType type, float baseMax, IReadOnlyList<IRegenRule> regen = null, MaxChangePolicy policy = MaxChangePolicy.KeepPercent)
        {
            Type = type;
            this.baseMax = Mathf.Max(0f, baseMax);
            this.regen = regen ?? Array.Empty<IRegenRule>();
            maxPolicy = policy;
            lastMax = Max;
        }

        public float BaseMax
        {
            get => baseMax;
            set
            {
                baseMax = Mathf.Max(0f, value);
                HandleMaxChanged(null);
            }
        }


        public void BindMaxStat(IReadonlyStat stat)
        {
            UnbindMaxStat();
            maxStat = stat;
            if(maxStat != null) maxStat.OnChanged += HandleMaxChanged;
            HandleMaxChanged(null);
        }

        public void UnbindMaxStat()
        {
            if(maxStat != null) maxStat.OnChanged -= HandleMaxChanged;
            maxStat = null;
        }

        public float Add(float amount, ChangeReason reason, in EffectContext ctx = default)
        {
            return amount <= 0f ? 0f : Apply(Mathf.Min(Current + amount, Max), reason, in ctx);
        }

        public float Remove(float amount, ChangeReason reason, in EffectContext ctx = default)
        {
            return amount <= 0f ? 0f : -Apply(Mathf.Max(Current - amount, 0f), reason, in ctx);
        }

        public bool TrySpend(float amount, in EffectContext ctx = default)
        {
            if(amount < 0f || Current < amount) return false;
            Remove(amount, ChangeReason.Spend, in ctx);
            return true;
        }

        public void Set(float value, ChangeReason reason = ChangeReason.Set, in EffectContext ctx = default)
        {
            Apply(Mathf.Clamp(value, 0f, Max), reason, in ctx);
        }

        public void Tick(float deltaTime)
        {
            timeSinceLoss += deltaTime;
            timeSinceGain += deltaTime;
            if(RegenPaused || regen.Count == 0) return;
            var state = new RegenState(Current, Max, timeSinceLoss, timeSinceGain);
            float delta = 0f;
            for(int i = 0; i < regen.Count; i++)
            {
                if(regen[i] != null) delta += regen[i].Evaluate(in state, deltaTime);
            }
            if(delta > 0f) Add(delta, ChangeReason.Regen);
            else if(delta < 0f) Remove(-delta, ChangeReason.Regen);
        }


        private float Apply(float newValue, ChangeReason reason, in EffectContext ctx)
        {
            float old = Current;
            if(Mathf.Approximately(old, newValue)) return 0f;
            Current = newValue;
            if(reason != ChangeReason.Regen)
            {
                if(newValue < old) timeSinceLoss = 0f;
                else timeSinceGain = 0f;
            }

            var change = new ResourceChange(this, old, newValue, Max, reason, in ctx);
            OnChanged?.Invoke(change);
            if(old > 0f && newValue <= 0f) OnDepleted?.Invoke(change);
            return newValue - old;
        }


        private void HandleMaxChanged(IReadonlyStat _)
        {
            float newMax = Max;
            float oldMax = lastMax;
            lastMax = newMax;
            if(Mathf.Approximately(newMax, oldMax)) return;

            float old = Current;
            Current = maxPolicy switch
            {
                MaxChangePolicy.KeepCurrent => Mathf.Max(Current, newMax),
                MaxChangePolicy.GainTheIncrease => Mathf.Clamp(Current + Mathf.Max(0f, newMax - oldMax), 0f, newMax),
                _ => oldMax > 0f ? Current / oldMax * newMax : newMax
            };
            OnChanged?.Invoke(new ResourceChange(this, old, Current, newMax, ChangeReason.MaxChanged, default));
        }
    }
}