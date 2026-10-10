using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework
{
    [AddComponentMenu(Globals.k_PACKAGE_NAME + "/Unit Components/Status Effects Component")]
    public sealed class StatusEffectsComponent : MonoBehaviour, IStatusEffects, IUnitComponent, ITickable
    {
        private StatusEffectDefinition[] innateEffects = Array.Empty<StatusEffectDefinition>();

        private readonly List<StatusEffectInstance> active = new();
        private readonly List<StatusEffectInstance> tickBuffer = new();
        private readonly List<StatusEffectInstance> pendingReturn = new();
        private IUnit owner;
        private IHealth health;
        private int depth;
        private bool spawning;

        public IReadOnlyList<StatusEffectInstance> Active => active;

        public event Action<StatusEffectInstance, int> OnApplied;
        public event Action<StatusEffectInstance, EffectRemoveReason> OnRemoved;

        public void Initialize(IUnit owner) => this.owner = owner;

        public void Bind()
        {
            if (owner.TryGet(out health)) health.OnDied += HandleDied;
        }

        public void Unbind()
        {
            if (health != null) health.OnDied -= HandleDied;
        }

        public void OnSpawned(in SpawnData data)
        {
            spawning = true;
            try
            {
                for (int i = 0; i < innateEffects.Length; i++)
                {
                    Apply(innateEffects[i], owner);
                }
                var starting = data.StartingEffects;
                if (starting != null)
                {
                    for (int i = 0; i < starting.Count; i++)
                    {
                        Apply(starting[i], owner);
                    }
                }
            }
            finally
            {
                spawning = false;
            }
        }

        public void OnDespawned(DespawnReason reason) => RemoveWhere(EffectRemoveReason.Despawned, null);

        public StatusEffectHandle Apply(StatusEffectDefinition definition, IUnit source, int stacks = 1)
        {
            if (definition == null || stacks < 1 || !owner.IsSpawned) return default;
            if (!spawning && !owner.IsAlive) return default;
            if (owner.Tags.HasAny(definition.BlockedBy)) return default;

            depth++;
            try
            {
                var existing = Find(definition, source);
                if (existing != null)
                {
                    int previous = existing.Restack(stacks);
                    if (existing.Stacks != previous) existing.NotifyStacksChanged(previous);
                    if (!existing.IsActive) return default;
                    RaiseApplied(existing, previous);
                    return new StatusEffectHandle(existing);
                }

                var instance = StatusEffectPool.Rent();
                instance.Begin(definition, owner, source, stacks);
                active.Add(instance);

                var tags = definition.GrantedTags;
                for (int i = 0; i < tags.Count; i++) owner.Tags.Add(tags[i]);

                instance.NotifyApplied();
                if (!instance.IsActive) return default;
                RaiseApplied(instance, 0);
                return new StatusEffectHandle(instance);
            }
            finally
            {
                Exit();
            }
        }

        public bool Remove(StatusEffectHandle handle, EffectRemoveReason reason = EffectRemoveReason.Dispelled)
        {
            var instance = handle.Get();
            if (instance == null || !ReferenceEquals(instance.Host, owner)) return false;
            depth++;
            try
            {
                RemoveInternal(instance, reason);
            }
            finally
            {
                Exit();
            }
            return true;
        }

        public int RemoveAll(StatusEffectDefinition definition, EffectRemoveReason reason = EffectRemoveReason.Dispelled)
        {
            return definition != null ? RemoveWhere(reason, definition) : 0;
        }

        public int Dispel(CombatTag category, int max = int.MaxValue)
        {
            if (category == null || max <= 0) return 0;
            int removed = 0;
            depth++;
            try
            {
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    if (i >= active.Count) continue;
                    if (!active[i].Definition.HasCategory(category)) continue;
                    RemoveInternal(active[i], EffectRemoveReason.Dispelled);
                    removed++;
                }
            }
            finally
            {
                Exit();
            }
            return removed;
        }

        public bool Has(StatusEffectDefinition deifnition)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Definition == deifnition) return true;
            }
            return false;
        }

        public int GetStacks(StatusEffectDefinition definition)
        {
            int total = 0;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Definition == definition) total += active[i].Stacks;
            }
            return total;
        }

        public void Tick(float deltaTime)
        {
            if (active.Count == 0) return;
            depth++;

            try
            {
                for (int i = 0; i < active.Count; i++)
                {
                    tickBuffer.Add(active[i]);
                }

                for (int i = 0; i < tickBuffer.Count; i++)
                {
                    var effect = tickBuffer[i];
                    if (effect.IsActive && effect.Advance(deltaTime)) RemoveInternal(effect, EffectRemoveReason.Expired);
                }
            }
            finally
            {
                tickBuffer.Clear();
                Exit();
            }
        }

        private void HandleDied(DeathInfo _) => RemoveWhere(EffectRemoveReason.Died, null);

        private int RemoveWhere(EffectRemoveReason reason, StatusEffectDefinition definition)
        {
            int removed = 0;
            depth++;
            try
            {
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    if (i >= active.Count) continue;
                    var effect = active[i];
                    if (definition != null && effect.Definition != definition) continue;
                    if (reason == EffectRemoveReason.Died && effect.Definition.KeepOnDeath) continue;
                    RemoveInternal(effect, reason);
                    removed++;
                }
            }
            finally
            {
                Exit();
            }

            return removed;
        }

        private void RemoveInternal(StatusEffectInstance effect, EffectRemoveReason reason)
        {
            if (!effect.IsActive) return;
            active.Remove(effect);
            var tags = effect.Definition.GrantedTags;
            for (int i = 0; i < tags.Count; i++)
            {
                owner.Tags.Remove(tags[i]);
            }

            effect.End(reason);
            OnRemoved?.Invoke(effect, reason);

            GameEvents.Raise(owner, new StatusEffectRemovedEvent(owner, effect, reason));
            pendingReturn.Add(effect);
        }

        private StatusEffectInstance Find(StatusEffectDefinition definition, IUnit source)
        {
            for(int i = 0; i < active.Count; i++)
            {
                var effect = active[i];
                if(effect.Definition != definition) continue;
                if(!definition.StackPerSource || ReferenceEquals(effect.Source.Unit, source)) return effect;
            }
            return null;
        }


        private void RaiseApplied(StatusEffectInstance effect, int previousStacks)
        {
            OnApplied?.Invoke(effect, previousStacks);
            GameEvents.Raise(owner, new StatusEffectAppliedEvent(owner, effect, previousStacks));
        }

        private void Exit()
        {
            if(--depth > 0 || pendingReturn.Count == 0) return;
            for(int i = 0; i < pendingReturn.Count; i++)
            {
                StatusEffectPool.Return(pendingReturn[i]);
            }
            pendingReturn.Clear();
        }
    }
}