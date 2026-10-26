using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal enum LogKind { Damage, Heal, Death, EffectApplied, EffectRemoved, Fired, Reload, Spawned, Despawned, Regen, RegenTick }

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
        public const string k_Kinds = "Monitor.Kind.";
        public const string k_Background = "Monitor.Background";
        public const string k_Limit = "Monitor.Limit";
        private const float k_RegenGap = 0.3f;

        public static readonly CombatLog Shared = new();

        private sealed class Pool
        {
            public IUnit Unit;
            public IReadonlyResource Resource;
            public Action<ResourceChange> Handler;
            public bool Running;
            public bool Decay;
            public float Started;
            public float LastTick;
            public float Total;
            public string Interrupted;
        }

        private readonly GameEventHandler<DamageReceivedEvent> onDamage;
        private readonly GameEventHandler<HealReceivedEvent> onHeal;
        private readonly GameEventHandler<DiedEvent> onDied;
        private readonly GameEventHandler<StatusEffectAppliedEvent> onApplied;
        private readonly GameEventHandler<StatusEffectRemovedEvent> onRemoved;
        private readonly GameEventHandler<FiredEvent> onFired;
        private readonly GameEventHandler<ReloadStartedEvent> onReload;
        private readonly GameEventHandler<UnitSpawnedEvent> onSpawned;
        private readonly GameEventHandler<UnitDespawnedEvent> onDespawned;
        private readonly Dictionary<IReadonlyResource, Pool> pools = new();
        private readonly List<Pool> running = new();
        private readonly List<IReadonlyResource> buffer = new();
        private EventBus bus;
        private int viewers;
        private bool background;
        private bool ticks;

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

        public static bool ShownByDefault(LogKind kind) => kind != LogKind.Fired && kind != LogKind.RegenTick;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Shared.Update;
            StudioPrefs.Changed += Shared.ReadPrefs;
            Shared.ReadPrefs();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if(change == PlayModeStateChange.EnteredPlayMode) Shared.Clear();
            else if(change == PlayModeStateChange.ExitingPlayMode) Shared.Detach();
        }

        public void AddViewer() => viewers++;

        public void RemoveViewer() => viewers = Mathf.Max(0, viewers - 1);

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
            var units = CombatServices.Units.All;
            for(int i = 0; i < units.Count; i++)
            {
                Watch(units[i]);
            }
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
            foreach(var pool in pools.Values)
            {
                pool.Resource.OnChanged -= pool.Handler;
            }
            pools.Clear();
            running.Clear();
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

        private void ReadPrefs()
        {
            Limit = Mathf.Max(100, StudioPrefs.GetInt(k_Limit, 2000));
            background = StudioPrefs.Get(k_Background, true);
            ticks = StudioPrefs.Get(k_Kinds + LogKind.RegenTick, ShownByDefault(LogKind.RegenTick));
            if(Entries.Count > Limit)
            {
                Entries.RemoveRange(0, Entries.Count - Limit);
                Version++;
            }
        }

        private void Update()
        {
            if(!EditorApplication.isPlaying || viewers == 0 && !background)
            {
                Detach();
                return;
            }
            Attach();
            if(running.Count == 0) return;
            float now = Time.time;
            for(int i = running.Count - 1; i >= 0; i--)
            {
                var pool = running[i];
                if(now - pool.LastTick > k_RegenGap) Stop(pool, pool.Interrupted != null ? $"stopped after it {pool.Interrupted}" : "stopped");
            }
        }

        private void Add(LogEntry entry)
        {
            if(Paused) return;
            entry.Time = Time.time;
            Entries.Add(entry);
            if(Entries.Count > Limit) Entries.RemoveRange(0, Entries.Count - Limit);
            Version++;
        }

        private void Watch(IUnit unit)
        {
            if(unit == null || !unit.TryGet<IResourceProvider>(out var provider)) return;
            foreach(var resource in provider.All)
            {
                if(resource == null || pools.ContainsKey(resource)) continue;
                var pool = new Pool { Unit = unit, Resource = resource };
                pool.Handler = change => OnResource(pool, change);
                resource.OnChanged += pool.Handler;
                pools.Add(resource, pool);
            }
        }

        private void Unwatch(IUnit unit)
        {
            buffer.Clear();
            foreach(var pair in pools)
            {
                if(ReferenceEquals(pair.Value.Unit, unit)) buffer.Add(pair.Key);
            }
            foreach(var resource in buffer)
            {
                var pool = pools[resource];
                resource.OnChanged -= pool.Handler;
                running.Remove(pool);
                pools.Remove(resource);
            }
        }

        private void OnResource(Pool pool, ResourceChange change)
        {
            if(change.Reason == ChangeReason.MaxChanged) return;
            if(change.Reason != ChangeReason.Regen)
            {
                if(pool.Running && change.New < change.Old) pool.Interrupted = change.Reason == ChangeReason.Damage ? "took damage" : $"lost some to {change.Reason.ToString().ToLowerInvariant()}";
                return;
            }

            float delta = change.New - change.Old;
            float now = Time.time;
            if(!pool.Running)
            {
                pool.Running = true;
                pool.Decay = delta < 0f;
                pool.Started = now;
                pool.Total = 0f;
                running.Add(pool);
                Add(new LogEntry { Kind = LogKind.Regen, Target = pool.Unit, Text = $"{Owner(pool)}: {TypeOf(pool)} {Verb(pool)} started at {change.Old:0.#}/{change.Max:0.#}" });
            }
            pool.Interrupted = null;
            pool.Total += delta;
            pool.LastTick = now;
            if(ticks) Add(new LogEntry { Kind = LogKind.RegenTick, Target = pool.Unit, Amount = delta, Text = $"{Owner(pool)}: {TypeOf(pool)} {delta:+0.###;-0.###} → {change.New:0.#}/{change.Max:0.#}" });
            if(!pool.Decay && change.New >= change.Max) Stop(pool, "reached full");
            else if(pool.Decay && change.New <= 0f) Stop(pool, "ran out");
        }

        private void Stop(Pool pool, string how)
        {
            pool.Running = false;
            running.Remove(pool);
            var resource = pool.Resource;
            Add(new LogEntry { Kind = LogKind.Regen, Target = pool.Unit, Amount = pool.Total, Text = $"{Owner(pool)}: {TypeOf(pool)} {Verb(pool)} {how} at {resource.Current:0.#}/{resource.Max:0.#} ({pool.Total:+0.#;-0.#} over {pool.LastTick - pool.Started:0.0}s)" });
        }

        private static string Owner(Pool pool) => pool.Unit.IsAlive ? NameOf(pool.Unit) : $"{NameOf(pool.Unit)} (dead)";

        private static string TypeOf(Pool pool) => StudioKinds.NameOf(pool.Resource.Type);

        private static string Verb(Pool pool) => pool.Decay ? "decay" : "regen";

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
            Watch(evt.Unit);
            Add(new LogEntry { Kind = LogKind.Spawned, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} spawned" });
        }

        private void OnDespawned(in UnitDespawnedEvent evt)
        {
            Unwatch(evt.Unit);
            Add(new LogEntry { Kind = LogKind.Despawned, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} despawned: {evt.Reason}" });
        }
    }
}
