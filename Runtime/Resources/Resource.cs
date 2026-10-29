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
        private readonly IUnit owner;
        private IReadonlyStat maxStat;
        private float baseMax;
        private float lastMax;
        private float timeSinceLoss = float.MaxValue;
        private float timeSinceGain = float.MaxValue;
        private bool regenerating;

        public ResourceType Type { get; }
        public float Current { get; private set; }
        public float Max => maxStat != null ? Mathf.Max(0f, maxStat.Value) : baseMax;
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsDepleted => Current <= 0f;
        public bool IsFull => Current >= Max;
        public bool RegenPaused { get; set; }

        public event Action<ResourceChange> OnChanged;
        public event Action<ResourceChange> OnDepleted;

        public Resource(ResourceType type, float baseMax, IRegenRule[] regen = null, MaxChangePolicy policy = MaxChangePolicy.KeepPercent, IUnit owner = null)
        {
            Type = type;
            this.baseMax = Mathf.Max(0f, baseMax);
            this.regen = regen ?? Array.Empty<IRegenRule>();
            maxPolicy = policy;
            this.owner = owner;
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

        public void RestartLossTimer() => timeSinceLoss = 0f;

        public void Tick(float deltaTime)
        {
            timeSinceLoss += deltaTime;
            timeSinceGain += deltaTime;
            float applied = 0f;
            if(!RegenPaused && regen.Count > 0)
            {
                var state = new RegenState(Current, Max, timeSinceLoss, timeSinceGain);
                float delta = 0f;
                for(int i = 0; i < regen.Count; i++)
                {
                    if(regen[i] != null) delta += regen[i].Evaluate(in state, deltaTime);
                }
                if(delta > 0f) applied = Add(delta, ChangeReason.Regen);
                else if(delta < 0f) applied = Remove(-delta, ChangeReason.Regen);
            }
            SetRegenerating(applied != 0f);
        }

        internal void StopRegen() => SetRegenerating(false);


        private float Apply(float newValue, ChangeReason reason, in EffectContext ctx)
        {
            float old = Current;
            if(Mathf.Approximately(old, newValue)) return 0f;
            float max = Max;
            bool wasFull = old >= max;
            Current = newValue;
            if(reason != ChangeReason.Regen)
            {
                if(newValue < old) timeSinceLoss = 0f;
                else timeSinceGain = 0f;
            }

            var change = new ResourceChange(this, old, newValue, max, reason, in ctx);
            Announce(in change, old > 0f && newValue <= 0f, !wasFull && newValue >= max);
            return newValue - old;
        }

        private void Announce(in ResourceChange change, bool depleted, bool filled)
        {
            if(owner != null)
            {
                GameEvents.Raise(owner, new ResourceChangedEvent(owner, in change));
                if(depleted) GameEvents.Raise(owner, new ResourceDepletedEvent(owner, in change));
                if(filled) GameEvents.Raise(owner, new ResourceFilledEvent(owner, in change));
            }
            OnChanged?.Invoke(change);
            if(depleted) OnDepleted?.Invoke(change);
        }

        private void SetRegenerating(bool value)
        {
            if(regenerating == value) return;
            regenerating = value;
            if(owner == null) return;
            if(value) GameEvents.Raise(owner, new ResourceRegenStartedEvent(owner, this));
            else GameEvents.Raise(owner, new ResourceRegenStoppedEvent(owner, this));
        }


        private void HandleMaxChanged(IReadonlyStat _)
        {
            float newMax = Max;
            float oldMax = lastMax;
            lastMax = newMax;
            if(Mathf.Approximately(newMax, oldMax)) return;

            float old = Current;
            bool wasFull = old >= oldMax;
            Current = maxPolicy switch
            {
                MaxChangePolicy.KeepCurrent => Mathf.Min(Current, newMax),
                MaxChangePolicy.GainTheIncrease => Mathf.Clamp(Current + Mathf.Max(0f, newMax - oldMax), 0f, newMax),
                _ => oldMax > 0f ? Current / oldMax * newMax : newMax
            };
            var change = new ResourceChange(this, old, Current, newMax, ChangeReason.MaxChanged, default);
            Announce(in change, false, !wasFull && newMax > 0f && Current >= newMax);
        }


        public void ResetTo(float value)
        {
            RegenPaused = false;
            regenerating = false;
            Apply(Mathf.Clamp(value, 0f, Max), ChangeReason.Set, default);
            timeSinceLoss = float.MaxValue;
            timeSinceGain = float.MaxValue;
        }
    }
}