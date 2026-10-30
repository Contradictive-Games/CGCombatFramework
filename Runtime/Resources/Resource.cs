using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public enum MaxChangePolicy { KeepPercent, KeepCurrent, GainTheIncrease }

    public sealed class Resource : IResource, ITickable, IStatListener
    {
        private readonly IReadOnlyList<IRegenRule> regen;
        private readonly MaxChangePolicy maxPolicy;
        private readonly IUnit owner;
        private readonly ListenerList<IResourceListener> listeners = new();
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
                HandleMaxChanged();
            }
        }

        public void AddListener(IResourceListener listener) => listeners.Add(listener);
        public void RemoveListener(IResourceListener listener) => listeners.Remove(listener);


        public void BindMaxStat(IReadonlyStat stat)
        {
            UnbindMaxStat();
            maxStat = stat;
            maxStat?.AddListener(this);
            HandleMaxChanged();
        }

        public void UnbindMaxStat()
        {
            maxStat?.RemoveListener(this);
            maxStat = null;
        }

        void IStatListener.OnStatChanged(in StatChangedEvent evt) => HandleMaxChanged();

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

            if(owner != null || listeners.Count > 0) Announce(old, newValue, max, reason, in ctx, old > 0f && newValue <= 0f, !wasFull && newValue >= max);
            return newValue - old;
        }

        private void Announce(float old, float newValue, float max, ChangeReason reason, in EffectContext ctx, bool depleted, bool filled)
        {
            var change = new ResourceChange(owner, this, old, newValue, max, reason);
            var changed = new ResourceChangedEvent(owner, in change);
            if(owner != null)
            {
                GameEvents.Raise(owner, in changed);
                if(depleted) GameEvents.Raise(owner, new ResourceDepletedEvent(owner, in change, in ctx));
                if(filled) GameEvents.Raise(owner, new ResourceFilledEvent(owner, in change));
            }

            var items = listeners.Items;
            if(items.Length == 0) return;
            for(int i = 0; i < items.Length; i++)
            {
                items[i].OnResourceChanged(in changed);
            }
            if(!depleted) return;
            var emptied = new ResourceDepletedEvent(owner, in change, in ctx);
            for(int i = 0; i < items.Length; i++)
            {
                items[i].OnResourceDepleted(in emptied);
            }
        }

        private void SetRegenerating(bool value)
        {
            if(regenerating == value) return;
            regenerating = value;
            if(owner == null) return;
            if(value) GameEvents.Raise(owner, new ResourceRegenStartedEvent(owner, this));
            else GameEvents.Raise(owner, new ResourceRegenStoppedEvent(owner, this));
        }


        private void HandleMaxChanged()
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
            if(owner != null || listeners.Count > 0) Announce(old, Current, newMax, ChangeReason.MaxChanged, default, false, !wasFull && newMax > 0f && Current >= newMax);
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
