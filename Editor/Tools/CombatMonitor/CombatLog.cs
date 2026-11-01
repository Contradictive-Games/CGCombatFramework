using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal enum LogKind { Damage, Heal, Death, EffectApplied, EffectRemoved, Fired, Reload, Spawned, Despawned, Regen, RegenTick, Lifecycle, Firing, StatusEffects, ResourcesAndStats, Tags, Projectiles, Schedules, Other, YourEvents }

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
        public const string k_Step = "Monitor.RegenStep";
        private const float k_RegenGap = 0.3f;

        public static readonly CombatLog Shared = new();

        private sealed class Pool
        {
            public IUnit Unit;
            public IReadonlyResource Resource;
            public bool Running;
            public bool Decay;
            public float Started;
            public float LastTick;
            public float Total;
            public string Interrupted;
            public bool Stepping;
            public float StepStart;
            public float StepTotal;
        }

        private abstract class Subscription
        {
            protected Subscription(LogKind kind) => Kind = kind;

            public LogKind Kind { get; }
            public bool Listening { get; private set; }

            public void Listen(EventBus bus, bool listen)
            {
                if(listen == Listening) return;
                Listening = listen;
                if(listen) Subscribe(bus);
                else Unsubscribe(bus);
            }

            protected abstract void Subscribe(EventBus bus);
            protected abstract void Unsubscribe(EventBus bus);
        }

        private sealed class Subscription<T> : Subscription where T : struct, ICombatEvent
        {
            private readonly GameEventHandler<T> handler;

            public Subscription(LogKind kind, GameEventHandler<T> handler) : base(kind) => this.handler = handler;

            protected override void Subscribe(EventBus bus) => bus.Subscribe(handler);
            protected override void Unsubscribe(EventBus bus) => bus.Unsubscribe(handler);
        }

        private sealed class CustomSubscription<T> : Subscription where T : struct, ICombatEvent
        {
            private readonly CombatLog log;
            private readonly GameEventHandler<T> handler;
            private readonly (string name, Func<object, object> read)[] members;

            public CustomSubscription(CombatLog log) : base(LogKind.YourEvents)
            {
                this.log = log;
                handler = Handle;
                var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => (f.Name, (Func<object, object>)f.GetValue));
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).Select(p => (p.Name, (Func<object, object>)p.GetValue));
                members = fields.Concat(properties).ToArray();
            }

            protected override void Subscribe(EventBus bus) => bus.Subscribe(handler);
            protected override void Unsubscribe(EventBus bus) => bus.Unsubscribe(handler);

            private void Handle(in T evt)
            {
                if(log.Paused) return;
                object boxed = evt;
                IUnit unit = null;
                var text = new StringBuilder(typeof(T).Name);
                for(int i = 0; i < members.Length; i++)
                {
                    object value;
                    try
                    {
                        value = members[i].read(boxed);
                    }
                    catch(Exception)
                    {
                        value = "?";
                    }
                    if(unit == null && value is IUnit found) unit = found;
                    text.Append(i == 0 ? ": " : ", ").Append(members[i].name).Append(' ').Append(Format(value));
                }
                log.Add(new LogEntry { Kind = LogKind.YourEvents, Target = unit, Text = text.ToString() });
            }
        }

        private readonly List<Subscription> subscriptions = new();
        private readonly Dictionary<IReadonlyResource, Pool> pools = new();
        private readonly List<Pool> running = new();
        private readonly List<IReadonlyResource> buffer = new();
        private readonly HashSet<IUnit> arriving = new();
        private readonly HashSet<IUnit> departing = new();
        private EventBus bus;
        private int viewers;
        private bool background;
        private bool ticks;
        private bool customFound;
        private float step = 1f;

        public CombatLog()
        {
            subscriptions.Add(new Subscription<DamageReceivedEvent>(LogKind.Damage, OnDamage));
            subscriptions.Add(new Subscription<HealReceivedEvent>(LogKind.Heal, OnHeal));
            subscriptions.Add(new Subscription<DiedEvent>(LogKind.Death, OnDied));
            subscriptions.Add(new Subscription<StatusEffectAppliedEvent>(LogKind.EffectApplied, OnApplied));
            subscriptions.Add(new Subscription<StatusEffectRemovedEvent>(LogKind.EffectRemoved, OnRemoved));
            subscriptions.Add(new Subscription<FiredEvent>(LogKind.Fired, OnFired));
            subscriptions.Add(new Subscription<ReloadStartedEvent>(LogKind.Reload, OnReload));
            subscriptions.Add(new Subscription<ReloadCompletedEvent>(LogKind.Reload, OnReloaded));
            subscriptions.Add(new Subscription<UnitSpawnedEvent>(LogKind.Spawned, OnSpawned));
            subscriptions.Add(new Subscription<UnitDespawnedEvent>(LogKind.Despawned, OnDespawned));
            subscriptions.Add(new Subscription<ResourceChangedEvent>(LogKind.Regen, OnResourceChanged));

            subscriptions.Add(new Subscription<UnitArrivingEvent>(LogKind.Lifecycle, OnArriving));
            subscriptions.Add(new Subscription<UnitArrivedEvent>(LogKind.Lifecycle, OnArrived));
            subscriptions.Add(new Subscription<DespawnScheduledEvent>(LogKind.Lifecycle, OnDespawnScheduled));
            subscriptions.Add(new Subscription<UnitDepartingEvent>(LogKind.Lifecycle, OnDeparting));
            subscriptions.Add(new Subscription<UnitReleasedEvent>(LogKind.Lifecycle, OnReleased));
            subscriptions.Add(new Subscription<RevivedEvent>(LogKind.Lifecycle, OnRevived));

            subscriptions.Add(new Subscription<FireBlockedEvent>(LogKind.Firing, OnFireBlocked));
            subscriptions.Add(new Subscription<FiringPhaseChangedEvent>(LogKind.Firing, OnPhaseChanged));
            subscriptions.Add(new Subscription<FirableChargedEvent>(LogKind.Firing, OnCharged));
            subscriptions.Add(new Subscription<FirableEquippedEvent>(LogKind.Firing, OnEquipped));
            subscriptions.Add(new Subscription<FirableUnequippedEvent>(LogKind.Firing, OnUnequipped));
            subscriptions.Add(new Subscription<CooldownStartedEvent>(LogKind.Firing, OnCooldownStarted));
            subscriptions.Add(new Subscription<CooldownEndedEvent>(LogKind.Firing, OnCooldownEnded));
            subscriptions.Add(new Subscription<ChargesChangedEvent>(LogKind.Firing, OnChargesChanged));
            subscriptions.Add(new Subscription<AmmoChangedEvent>(LogKind.Firing, OnAmmoChanged));

            subscriptions.Add(new Subscription<StatusEffectStacksChangedEvent>(LogKind.StatusEffects, OnStacksChanged));
            subscriptions.Add(new Subscription<StatusEffectTickedEvent>(LogKind.StatusEffects, OnEffectTicked));
            subscriptions.Add(new Subscription<StatusEffectTriggeredEvent>(LogKind.StatusEffects, OnEffectTriggered));

            subscriptions.Add(new Subscription<ResourceDepletedEvent>(LogKind.ResourcesAndStats, OnDepleted));
            subscriptions.Add(new Subscription<ResourceFilledEvent>(LogKind.ResourcesAndStats, OnFilled));
            subscriptions.Add(new Subscription<StatChangedEvent>(LogKind.ResourcesAndStats, OnStatChanged));

            subscriptions.Add(new Subscription<TagAddedEvent>(LogKind.Tags, OnTagAdded));
            subscriptions.Add(new Subscription<TagRemovedEvent>(LogKind.Tags, OnTagRemoved));

            subscriptions.Add(new Subscription<ProjectileLaunchedEvent>(LogKind.Projectiles, OnLaunched));
            subscriptions.Add(new Subscription<ProjectileBouncedEvent>(LogKind.Projectiles, OnBounced));
            subscriptions.Add(new Subscription<ProjectileStoppedEvent>(LogKind.Projectiles, OnStopped));

            subscriptions.Add(new Subscription<ScheduledEvent>(LogKind.Schedules, OnScheduled));
            subscriptions.Add(new Subscription<ScheduleResolvedEvent>(LogKind.Schedules, OnScheduleResolved));
            subscriptions.Add(new Subscription<ScheduleCancelledEvent>(LogKind.Schedules, OnScheduleCancelled));

            subscriptions.Add(new Subscription<KnockbackEvent>(LogKind.Other, OnKnockback));
            subscriptions.Add(new Subscription<TeleportedEvent>(LogKind.Other, OnTeleported));
            subscriptions.Add(new Subscription<PauseChangedEvent>(LogKind.Other, OnPauseChanged));
            subscriptions.Add(new Subscription<SessionEndedEvent>(LogKind.Other, OnSessionEnded));
        }

        public readonly List<LogEntry> Entries = new();
        public int Limit = 2000;
        public bool Paused;
        public int Version { get; private set; }

        public static bool ShownByDefault(LogKind kind)
        {
            switch(kind)
            {
                case LogKind.Fired:
                case LogKind.RegenTick:
                case LogKind.Firing:
                case LogKind.StatusEffects:
                case LogKind.Tags:
                case LogKind.Projectiles:
                    return false;
                default:
                    return true;
            }
        }

        private static bool IsCategory(LogKind kind) => kind >= LogKind.Lifecycle;

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
            FindCustomEvents();
            Listen();
            var units = CombatServices.Units.All;
            for(int i = 0; i < units.Count; i++)
            {
                Watch(units[i]);
            }
        }

        public void Detach()
        {
            if(bus == null) return;
            foreach(var subscription in subscriptions)
            {
                subscription.Listen(bus, false);
            }
            bus = null;
            pools.Clear();
            running.Clear();
            arriving.Clear();
            departing.Clear();
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

        public static string NameOf(IUnit unit) => unit == null || unit is UnityEngine.Object asset && asset == null ? "Nobody" : unit.DisplayName;

        private void FindCustomEvents()
        {
            if(customFound) return;
            customFound = true;
            var framework = typeof(ICombatEvent).Assembly;
            foreach(var type in TypeCache.GetTypesDerivedFrom<ICombatEvent>())
            {
                if(!type.IsValueType || type.ContainsGenericParameters || type.Assembly == framework) continue;
                subscriptions.Add((Subscription)Activator.CreateInstance(typeof(CustomSubscription<>).MakeGenericType(type), this));
            }
        }

        private void Listen()
        {
            if(bus == null) return;
            foreach(var subscription in subscriptions)
            {
                subscription.Listen(bus, !IsCategory(subscription.Kind) || StudioPrefs.Get(k_Kinds + subscription.Kind, ShownByDefault(subscription.Kind)));
            }
        }

        private void ReadPrefs()
        {
            Limit = Mathf.Max(100, StudioPrefs.GetInt(k_Limit, 2000));
            background = StudioPrefs.Get(k_Background, true);
            ticks = StudioPrefs.Get(k_Kinds + LogKind.RegenTick, ShownByDefault(LogKind.RegenTick));
            step = Mathf.Max(0.05f, StudioPrefs.GetInt(k_Step, 1000) / 1000f);
            Listen();
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
                pools.Add(resource, new Pool { Unit = unit, Resource = resource });
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
                running.Remove(pools[resource]);
                pools.Remove(resource);
            }
        }

        private void OnResourceChanged(in ResourceChangedEvent evt)
        {
            if(evt.Change.Resource != null && pools.TryGetValue(evt.Change.Resource, out var pool)) OnResource(pool, evt.Change);
        }

        private void OnResource(Pool pool, in ResourceChange change)
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
            if(ticks)
            {
                if(!pool.Stepping)
                {
                    pool.Stepping = true;
                    pool.StepStart = now - Time.deltaTime;
                    pool.StepTotal = 0f;
                }
                pool.StepTotal += delta;
                if(now - pool.StepStart >= step) Flush(pool);
            }
            if(!pool.Decay && change.New >= change.Max) Stop(pool, "reached full");
            else if(pool.Decay && change.New <= 0f) Stop(pool, "ran out");
        }

        private void Flush(Pool pool)
        {
            if(!pool.Stepping) return;
            pool.Stepping = false;
            if(!ticks || Mathf.Approximately(pool.StepTotal, 0f)) return;
            float seconds = Mathf.Max(0.001f, pool.LastTick - pool.StepStart);
            var resource = pool.Resource;
            Add(new LogEntry { Kind = LogKind.RegenTick, Target = pool.Unit, Amount = pool.StepTotal, Text = $"{Owner(pool)}: {TypeOf(pool)} {pool.StepTotal:+0.##;-0.##} over {seconds:0.0#}s ({pool.StepTotal / seconds:+0.#;-0.#}/s) → {resource.Current:0.#}/{resource.Max:0.#}" });
        }

        private void Stop(Pool pool, string how)
        {
            Flush(pool);
            pool.Running = false;
            running.Remove(pool);
            var resource = pool.Resource;
            Add(new LogEntry { Kind = LogKind.Regen, Target = pool.Unit, Amount = pool.Total, Text = $"{Owner(pool)}: {TypeOf(pool)} {Verb(pool)} {how} at {resource.Current:0.#}/{resource.Max:0.#} ({pool.Total:+0.#;-0.#} over {pool.LastTick - pool.Started:0.0}s)" });
        }

        private static string Owner(Pool pool) => pool.Unit.IsAlive ? NameOf(pool.Unit) : $"{NameOf(pool.Unit)} (dead)";

        private static string TypeOf(Pool pool) => StudioKinds.NameOf(pool.Resource.Type);

        private static string Verb(Pool pool) => pool.Decay ? "decay" : "regen";

        private static string NameOf(Firable firable) => StudioKinds.NameOf(firable?.Definition);

        private static string NameOf(StatusEffectInstance effect) => StudioKinds.NameOf(effect?.Definition);

        private static string NameOf(IGameDefinition cause) => cause is UnityEngine.Object asset ? StudioKinds.NameOf(asset) : cause?.Id ?? "None";

        private static string NameOf(Projectile projectile) => projectile != null ? NameOf(projectile.Shot.Effect.Cause) : "Projectile";

        private static string Format(object value)
        {
            switch(value)
            {
                case null:
                    return "None";
                case IUnit unit:
                    return NameOf(unit);
                case UnityEngine.Object asset:
                    return StudioKinds.NameOf(asset);
                case IGameDefinition definition:
                    return NameOf(definition);
                case float number:
                    return number.ToString("0.##");
                case Vector3 vector:
                    return vector.ToString("0.##");
                case string text:
                    return text;
                case IEnumerable items:
                    return $"[{string.Join(", ", items.Cast<object>().Select(Format))}]";
                default:
                    return value.ToString();
            }
        }

        private void OnDamage(in DamageReceivedEvent evt)
        {
            var result = evt.Result;
            string type = result.Request.Type != null ? StudioKinds.NameOf(result.Request.Type) + " " : string.Empty;
            string cause = result.Request.Context.Cause is GameDefinition definition ? $" with {definition.DisplayName}" : string.Empty;
            string text = result.Result != HitResult.Hit
                ? $"{NameOf(evt.Source)} → {NameOf(evt.Unit)}: {result.Result}{cause}"
                : $"{NameOf(evt.Source)} → {NameOf(evt.Unit)}: {result.Dealt:0.##} {type}damage{(result.IsCrit ? " (crit)" : string.Empty)}{(result.Absorbed > 0f ? $", {result.Absorbed:0.##} absorbed" : string.Empty)}{(result.IsKillingBlow ? ", killing blow" : string.Empty)}{cause}";
            Add(new LogEntry { Kind = LogKind.Damage, Source = evt.Source, Target = evt.Unit, Amount = result.Dealt + result.Absorbed, Crit = result.IsCrit, Text = text });
        }

        private void OnHeal(in HealReceivedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Heal, Source = evt.Source, Target = evt.Unit, Amount = evt.Result.Healed, Text = $"{NameOf(evt.Source)} → {NameOf(evt.Unit)}: healed {evt.Result.Healed:0.##}" });
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
                ? $"{StudioKinds.NameOf(effect.Definition)} applied to {NameOf(evt.Unit)} by {NameOf(effect.SourceUnit)}"
                : $"{StudioKinds.NameOf(effect.Definition)} on {NameOf(evt.Unit)}: {evt.PreviousStacks} → {effect.Stacks} stacks";
            Add(new LogEntry { Kind = LogKind.EffectApplied, Source = effect.SourceUnit, Target = evt.Unit, Amount = effect.Stacks, Text = text });
        }

        private void OnRemoved(in StatusEffectRemovedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.EffectRemoved, Target = evt.Unit, Text = $"{StudioKinds.NameOf(evt.Effect.Definition)} left {NameOf(evt.Unit)}: {evt.Reason}" });
        }

        private void OnFired(in FiredEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Fired, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} fired {StudioKinds.NameOf(evt.Firable.Definition)}{(Mathf.Approximately(evt.Power, 1f) ? string.Empty : $" at {evt.Power:P0} power")}" });
        }

        private void OnReload(in ReloadStartedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Reload, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} reloading {StudioKinds.NameOf(evt.Firable?.Definition)} ({evt.Duration:0.##}s)" });
        }

        private void OnReloaded(in ReloadCompletedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Reload, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} reloaded {NameOf(evt.Firable)}" });
        }

        private void OnSpawned(in UnitSpawnedEvent evt)
        {
            Watch(evt.Unit);
            Add(new LogEntry { Kind = LogKind.Spawned, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} spawned" });
        }

        private void OnDespawned(in UnitDespawnedEvent evt)
        {
            Unwatch(evt.Unit);
            arriving.Remove(evt.Unit);
            Add(new LogEntry { Kind = LogKind.Despawned, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} despawned: {evt.Reason}" });
        }

        private void OnArriving(in UnitArrivingEvent evt)
        {
            arriving.Add(evt.Unit);
            string tags = evt.Tags != null && evt.Tags.Count > 0 ? $" as {string.Join(", ", evt.Tags.Select(t => StudioKinds.NameOf(t)))}" : string.Empty;
            Add(new LogEntry { Kind = LogKind.Lifecycle, Target = evt.Unit, Amount = evt.Duration, Text = $"{NameOf(evt.Unit)} arriving{tags} ({evt.Duration:0.##}s)" });
        }

        private void OnArrived(in UnitArrivedEvent evt)
        {
            if(!arriving.Remove(evt.Unit)) return;
            Add(new LogEntry { Kind = LogKind.Lifecycle, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} arrived" });
        }

        private void OnDespawnScheduled(in DespawnScheduledEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Lifecycle, Target = evt.Unit, Amount = evt.Delay, Text = $"{NameOf(evt.Unit)} despawns in {evt.Delay:0.##}s: {evt.Reason}" });
        }

        private void OnDeparting(in UnitDepartingEvent evt)
        {
            departing.Add(evt.Unit);
            Add(new LogEntry { Kind = LogKind.Lifecycle, Target = evt.Unit, Amount = evt.Duration, Text = $"{NameOf(evt.Unit)} departing: {evt.Reason} ({evt.Duration:0.##}s)" });
        }

        private void OnReleased(in UnitReleasedEvent evt)
        {
            if(!departing.Remove(evt.Unit)) return;
            Add(new LogEntry { Kind = LogKind.Lifecycle, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} released" });
        }

        private void OnRevived(in RevivedEvent evt)
        {
            string by = evt.Source != null && !ReferenceEquals(evt.Source, evt.Unit) ? $" by {NameOf(evt.Source)}" : string.Empty;
            Add(new LogEntry { Kind = LogKind.Lifecycle, Source = evt.Source, Target = evt.Unit, Amount = evt.Health, Text = $"{NameOf(evt.Unit)} revived{by} at {evt.Health:0.#} health" });
        }

        private void OnFireBlocked(in FireBlockedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} can't fire {NameOf(evt.Firable)}: {evt.Reason}" });
        }

        private void OnPhaseChanged(in FiringPhaseChangedEvent evt)
        {
            string elapsed = evt.Previous != FiringPhase.Idle ? $" after {evt.Elapsed:0.##}s" : string.Empty;
            string reason = evt.EndReason != FiringEndReason.None ? $" ({evt.EndReason})" : string.Empty;
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} {NameOf(evt.Firable)}: {evt.Previous} → {evt.Current}{elapsed}{reason}" });
        }

        private void OnCharged(in FirableChargedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} {NameOf(evt.Firable)} fully charged" });
        }

        private void OnEquipped(in FirableEquippedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} equipped {NameOf(evt.Firable)} in {StudioKinds.NameOf(evt.Slot)}" });
        }

        private void OnUnequipped(in FirableUnequippedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} unequipped {NameOf(evt.Firable)} from {StudioKinds.NameOf(evt.Slot)}" });
        }

        private void OnCooldownStarted(in CooldownStartedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Amount = evt.Duration, Text = $"{NameOf(evt.Unit)} {NameOf(evt.Firable)} on cooldown ({evt.Duration:0.##}s)" });
        }

        private void OnCooldownEnded(in CooldownEndedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Text = $"{NameOf(evt.Unit)} {NameOf(evt.Firable)} ready" });
        }

        private void OnChargesChanged(in ChargesChangedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Amount = evt.Charges, Text = $"{NameOf(evt.Unit)} {NameOf(evt.Firable)} charges {evt.Charges}/{evt.MaxCharges}" });
        }

        private void OnAmmoChanged(in AmmoChangedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Firing, Source = evt.Unit, Amount = evt.Current, Text = $"{NameOf(evt.Unit)} {NameOf(evt.Firable)} ammo {evt.Current}/{evt.Capacity}" });
        }

        private void OnStacksChanged(in StatusEffectStacksChangedEvent evt)
        {
            int stacks = evt.Effect?.Stacks ?? 0;
            Add(new LogEntry { Kind = LogKind.StatusEffects, Source = evt.Effect?.SourceUnit, Target = evt.Unit, Amount = stacks, Text = $"{NameOf(evt.Effect)} on {NameOf(evt.Unit)}: {evt.Previous} → {stacks} stacks" });
        }

        private void OnEffectTicked(in StatusEffectTickedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.StatusEffects, Source = evt.Effect?.SourceUnit, Target = evt.Unit, Text = $"{NameOf(evt.Effect)} ticked on {NameOf(evt.Unit)}" });
        }

        private void OnEffectTriggered(in StatusEffectTriggeredEvent evt)
        {
            string on = evt.Target != null && !ReferenceEquals(evt.Target, evt.Unit) ? $" on {NameOf(evt.Target)}" : string.Empty;
            Add(new LogEntry { Kind = LogKind.StatusEffects, Source = evt.Unit, Target = evt.Target, Text = $"{NameOf(evt.Effect)} on {NameOf(evt.Unit)} triggered{on}" });
        }

        private void OnDepleted(in ResourceDepletedEvent evt)
        {
            var change = evt.Change;
            string by = change.Source != null ? $" by {NameOf(change.Source)}" : string.Empty;
            Add(new LogEntry { Kind = LogKind.ResourcesAndStats, Source = change.Source, Target = evt.Unit, Text = $"{NameOf(evt.Unit)}: {StudioKinds.NameOf(change.Resource?.Type)} depleted{by}" });
        }

        private void OnFilled(in ResourceFilledEvent evt)
        {
            var change = evt.Change;
            if(change.Reason == ChangeReason.Set) return;
            Add(new LogEntry { Kind = LogKind.ResourcesAndStats, Source = change.Source, Target = evt.Unit, Amount = change.New, Text = $"{NameOf(evt.Unit)}: {StudioKinds.NameOf(change.Resource?.Type)} full at {change.New:0.#}" });
        }

        private void OnStatChanged(in StatChangedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.ResourcesAndStats, Target = evt.Unit, Amount = evt.Current, Text = $"{NameOf(evt.Unit)}: {StudioKinds.NameOf(evt.Stat?.Type)} {evt.Previous:0.##} → {evt.Current:0.##}" });
        }

        private void OnTagAdded(in TagAddedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Tags, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} gained {StudioKinds.NameOf(evt.Tag)}" });
        }

        private void OnTagRemoved(in TagRemovedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Tags, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} lost {StudioKinds.NameOf(evt.Tag)}" });
        }

        private void OnLaunched(in ProjectileLaunchedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Projectiles, Source = evt.Unit, Amount = evt.Velocity.magnitude, Text = $"{NameOf(evt.Unit)} launched {NameOf(evt.Cause)} at {evt.Velocity.magnitude:0.#} m/s" });
        }

        private void OnBounced(in ProjectileBouncedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Projectiles, Source = evt.Unit, Text = $"{NameOf(evt.Projectile)} from {NameOf(evt.Unit)} bounced" });
        }

        private void OnStopped(in ProjectileStoppedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Projectiles, Source = evt.Unit, Text = $"{NameOf(evt.Projectile)} from {NameOf(evt.Unit)} stopped: {evt.Reason}" });
        }

        private void OnScheduled(in ScheduledEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Schedules, Source = evt.Unit, Amount = evt.Delay, Text = $"{NameOf(evt.Unit)} scheduled {NameOf(evt.Cause)} #{evt.Key} in {evt.Delay:0.##}s" });
        }

        private void OnScheduleResolved(in ScheduleResolvedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Schedules, Source = evt.Unit, Text = $"#{evt.Key} from {NameOf(evt.Unit)} resolved" });
        }

        private void OnScheduleCancelled(in ScheduleCancelledEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Schedules, Source = evt.Unit, Text = $"#{evt.Key} from {NameOf(evt.Unit)} cancelled: {evt.Reason}" });
        }

        private void OnKnockback(in KnockbackEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Other, Source = evt.Source, Target = evt.Unit, Amount = evt.Impulse.magnitude, Text = $"{NameOf(evt.Source)} → {NameOf(evt.Unit)}: knockback {evt.Impulse.magnitude:0.##}" });
        }

        private void OnTeleported(in TeleportedEvent evt)
        {
            string by = evt.Source != null && !ReferenceEquals(evt.Source, evt.Unit) ? $" by {NameOf(evt.Source)}" : string.Empty;
            Add(new LogEntry { Kind = LogKind.Other, Source = evt.Source, Target = evt.Unit, Text = $"{NameOf(evt.Unit)} teleported {Vector3.Distance(evt.From, evt.To):0.#}m{by}" });
        }

        private void OnPauseChanged(in PauseChangedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Other, Text = evt.IsPaused ? "Combat paused" : "Combat unpaused" });
        }

        private void OnSessionEnded(in SessionEndedEvent evt)
        {
            Add(new LogEntry { Kind = LogKind.Other, Text = $"Session ended: {evt.Reason}" });
        }
    }
}
