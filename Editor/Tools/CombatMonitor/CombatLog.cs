using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal enum LogKind { Damage, Heal, Death, EffectApplied, EffectRemoved, Fired, Reload, Spawned, Despawned }

    internal sealed class LogEntry
    {
        public float Time;
        public LogKind Kind;
        public IUnit Source;
        public IUnit Target;
        public string Text;
        public float Amount;
        public bool Crit;
    }

    internal sealed class CombatLog
    {
        private readonly GameEventHandler<DamageReceivedEvent> onDamage;
        private readonly GameEventHandler<HealReceivedEvent> onHeal;
        private readonly GameEventHandler<DiedEvent> onDied;
        private readonly GameEventHandler<StatusEffectAppliedEvent> onApplied;
        private readonly GameEventHandler<StatusEffectRemovedEvent> onRemoved;
        private readonly GameEventHandler<FiredEvent> onFired;
        private readonly GameEventHandler<ReloadStartedEvent> onReload;
        private readonly GameEventHandler<UnitSpawnedEvent> onSpawned;
        private readonly GameEventHandler<UnitDespawnedEvent> onDespawned;
        private EventBus bus;

        public CombatLog()
        {
            onDamage = OnDamage;
            onHeal = OnHeal;
            onDied = OnDied;
            onApplied = OnApplied;
            onRemoved = OnRemoved;
            onFired = OnFired;
            onReload = OnReload;
            onSpawned = OnSpawned;
            onDespawned = OnDespawned;
        }

        public readonly List<LogEntry> Entries = new();
        public int Limit = 2000;
        public bool Paused;
        public int Version { get; private set; }

        public void Attach()
        {
            if(!EditorApplication.isPlaying) return;
            var events = CombatServices.Events;
            if(ReferenceEquals(events, bus)) return;
            Detach();
            bus = events;
            bus.Subscribe(onDamage);
            bus.Subscribe(onHeal);
            bus.Subscribe(onDied);
            bus.Subscribe(onApplied);
            bus.Subscribe(onRemoved);
            bus.Subscribe(onFired);
            bus.Subscribe(onReload);
            bus.Subscribe(onSpawned);
            bus.Subscribe(onDespawned);
        }

        public void Detach()
        {
            if(bus == null) return;
            bus.Unsubscribe(onDamage);
            bus.Unsubscribe(onHeal);
            bus.Unsubscribe(onDied);
            bus.Unsubscribe(onApplied);
            bus.Unsubscribe(onRemoved);
            bus.Unsubscribe(onFired);
            bus.Unsubscribe(onReload);
            bus.Unsubscribe(onSpawned);
            bus.Unsubscribe(onDespawned);
            bus = null;
        }

        public void Clear()
        {
            Entries.Clear();
            Version++;
        }

        public float Sum(IUnit unit, bool dealt, float window)
        {
            float since = Time.time - window;
            float total = 0f;
            for(int i = Entries.Count - 1; i >= 0; i--)
            {
                var entry = Entries[i];
                if(entry.Time < since) break;
                if(entry.Kind != LogKind.Damage) continue;
                if(dealt ? ReferenceEquals(entry.Source?.Root(), unit) : ReferenceEquals(entry.Target, unit)) total += entry.Amount;
            }
            return total;
        }

        public static string NameOf(IUnit unit) => unit == null ? "Nobody" : $"{unit.DisplayName}";

        private void Add(LogEntry entry)
        {
            if(Paused) return;
            entry.Time = Time.time;
            Entries.Add(entry);
            if(Entries.Count > Limit) Entries.RemoveRange(0, Entries.Count - Limit);
            Version++;
        }

        private void OnDamage(in DamageReceivedEvent evt)
        {
            var result = evt.Result;
            string type = result.Request.Type != null ? StudioKinds.NameOf(result.Request.Type) + " " : string.Empty;
            string cause = result.Request.Context.Cause is GameDefinition definition ? $" with {definition.DisplayName}" : string.Empty;
            string text = result.Result != HitResult.Hit
                ? $"{NameOf(evt.SourceUnit)} → {NameOf(evt.TargetUnit)}: {result.Result}{cause}"
                : $"{NameOf(evt.SourceUnit)} → {NameOf(evt.TargetUnit)}: {result.Dealt:0.##} {type}damage{(result.IsCrit ? " (crit)" : string.Empty)}{(result.Absorbed > 0f ? $", {result.Absorbed:0.##} absorbed" : string.Empty)}{(result.IsKillingBlow ? ", killing blow" : string.Empty)}{cause}";
            Add(new LogEntry { Kind = LogKind.Damage, Source = evt.SourceUnit, Target = evt.TargetUnit, Amount = result.Dealt + result.Absorbed, Crit = result.IsCrit, Text = text });
        }

        private void OnHeal(in HealReceivedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Heal, Source = evt.SourceUnit, Target = evt.TargetUnit, Amount = evt.Result.Healed, Text = $"{NameOf(evt.SourceUnit)} → {NameOf(evt.TargetUnit)}: healed {evt.Result.Healed:0.##}" });
        }

        private void OnDied(in DiedEvent evt)
        {
            var death = evt.Death;
            Add(new LogEntry { Kind = LogKind.Death, Source = death.Killer, Target = death.Victim, Text = $"{NameOf(death.Victim)} died{(death.Killer != null ? $" to {NameOf(death.Killer)}" : string.Empty)}" });
        }

        private void OnApplied(in StatusEffectAppliedEvent evt)
        {
            var effect = evt.Effect;
            string text = evt.PreviousStacks == 0
                ? $"{StudioKinds.NameOf(effect.Definition)} applied to {NameOf(evt.Host)} by {NameOf(effect.SourceUnit)}"
                : $"{StudioKinds.NameOf(effect.Definition)} on {NameOf(evt.Host)}: {evt.PreviousStacks} → {effect.Stacks} stacks";
            Add(new LogEntry { Kind = LogKind.EffectApplied, Source = effect.SourceUnit, Target = evt.Host, Amount = effect.Stacks, Text = text });
        }

        private void OnRemoved(in StatusEffectRemovedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.EffectRemoved, Target = evt.Host, Text = $"{StudioKinds.NameOf(evt.Effect.Definition)} left {NameOf(evt.Host)}: {evt.Reason}" });
        }

        private void OnFired(in FiredEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Fired, Source = evt.Owner, Text = $"{NameOf(evt.Owner)} fired {StudioKinds.NameOf(evt.Firable.Definition)}{(Mathf.Approximately(evt.Power, 1f) ? string.Empty : $" at {evt.Power:P0} power")}" });
        }

        private void OnReload(in ReloadStartedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Reload, Source = evt.Owner, Text = $"{NameOf(evt.Owner)} reloading {StudioKinds.NameOf(evt.Firable)} ({evt.Duration:0.##}s)" });
        }

        private void OnSpawned(in UnitSpawnedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Spawned, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} spawned" });
        }

        private void OnDespawned(in UnitDespawnedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Despawned, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} despawned: {evt.Reason}" });
        }
    }
}
