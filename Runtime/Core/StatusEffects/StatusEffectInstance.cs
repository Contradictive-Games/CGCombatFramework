using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    public sealed class StatusEffectInstance
    {
        private const float k_TickEpsilon = 0.0001f;

        private readonly List<float> stackTimers = new();
        private float tickTimer;

        public StatusEffectDefinition Definition { get; private set; }
        public IUnit Host { get; private set; }
        public UnitHandle Source { get; private set; }
        public int Stacks { get; private set; }
        public float Duration { get; private set; }
        public float Remaining { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsActive { get; private set; }

        internal int Version { get; private set; }

        public IUnit SourceUnit => Source.Get();
        public float Remaining01 => Duration > 0f ? Mathf.Clamp01(Remaining / Duration) : 1f;

        internal StatusEffectInstance(){ }

        public EffectContext CreateContext(float power = 1f) => new(SourceUnit, Definition, Host, default, power, Stacks);

        internal void Begin(StatusEffectDefinition definition, IUnit host, IUnit source, int stacks)
        {
            Version++;
            Definition = definition;
            Host = host;
            Source = new UnitHandle(source);
            Stacks = Mathf.Clamp(stacks, 1, definition.MaxStacks);
            Duration = Remaining = definition.Duration;
            Elapsed = 0f;
            tickTimer = 0f;
            stackTimers.Clear();
            if(definition.Stacking == EffectStackingMode.Independent)
            {
                for(int i = 0; i < Stacks; i++)
                {
                    stackTimers.Add(definition.Duration);
                }
            }
            IsActive = true;
        }

        internal int Restack(int count)
        {
            int previous = Stacks;

            switch (Definition.Stacking)
            {
                case EffectStackingMode.Refresh:
                    Stacks = Mathf.Min(Stacks + count, Definition.MaxStacks);
                    Duration = Remaining = Definition.Duration;
                    break;
                case EffectStackingMode.AddDuration:
                    Stacks = Mathf.Min(Stacks + count, Definition.MaxStacks);
                    Remaining += Definition.Duration * count;
                    if(Definition.MaxDuration > 0f) Remaining = Mathf.Min(Remaining, Definition.MaxDuration);
                    Duration = Mathf.Max(Duration, Remaining);
                    break;
                case EffectStackingMode.Independent:
                    for(int i = 0; i < count; i++)
                    {
                        if(stackTimers.Count < Definition.MaxStacks) stackTimers.Add(Definition.Duration);
                        else {
                            int idx = IndexOfOldest(stackTimers);
                            stackTimers[idx] = Definition.Duration;
                        }
                    }
                    Stacks = stackTimers.Count;
                    Duration = Remaining = Definition.Duration;
                    break;
            }
            return previous;
        }

        internal bool Advance(float deltaTime)
        {
            Elapsed += deltaTime;

            if(Definition.TickInterval > 0f)
            {
                tickTimer += deltaTime;
                while(IsActive && tickTimer + k_TickEpsilon >= Definition.TickInterval)
                {
                    tickTimer -= Definition.TickInterval;
                    NotifyTick();
                }
                if(!IsActive) return false;
            }

            if(Definition.IsPermanent) return false;
            if(Definition.Stacking == EffectStackingMode.Independent) return AdvanceStacks(deltaTime);

            Remaining -= deltaTime;
            return Remaining <= 0f;
        }

        private bool AdvanceStacks(float deltaTime)
        {
            float longest = 0f;
            for(int i = stackTimers.Count - 1; i >= 0; i--)
            {
                float left = stackTimers[i] - deltaTime;
                if(left <= 0f)
                {
                    stackTimers.RemoveAt(i);
                    continue;
                }
                stackTimers[i] = left;
                if(left > longest) longest = left;
            }

            Remaining = longest;
            if(stackTimers.Count == 0) return true;

            int previous = Stacks;
            Stacks = stackTimers.Count;
            if(Stacks != previous) NotifyStacksChanged(previous);
            return false;
        }

        internal void NotifyApplied()
        {
            var behaviors = Definition.Behaviors;
            for(int i = 0; i < behaviors.Length && IsActive; i++)
            {
                behaviors[i]?.OnApplied(this);
            }
            if(IsActive && Definition.TickOnApplication) NotifyTick();
        }

        internal void NotifyStacksChanged(int previous)
        {
            var behaviors = Definition.Behaviors;
            for(int i = 0; i < behaviors.Length && IsActive; i++)
            {
                behaviors[i]?.OnStacksChanged(this, previous);
            }
        }

        internal void NotifyTick()
        {
            var behaviors = Definition.Behaviors;
            for(int i = 0; i < behaviors.Length && IsActive; i++)
            {
                behaviors[i]?.OnTick(this);
            }
        }

        internal void End(EffectRemoveReason reason)
        {
            IsActive = false;
            var behaviors = Definition.Behaviors;
            for(int i = 0; i < behaviors.Length; i++)
            {
                behaviors[i]?.OnRemoved(this, reason);
            }
        }

        internal void Clear()
        {
            Definition = null;
            Host = null;
            Source = default;
            Stacks = 0;
            stackTimers.Clear();
            IsActive = false;
        }


        internal static int IndexOfOldest(IReadOnlyList<float> timers)
        {
            int index = 0;
            for(int i = 1; i < timers.Count; i++)
            {
                if(timers[i] < timers[index]) index = i;
            }
            return index;
        }


    }
}