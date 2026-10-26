using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    [Serializable]
    internal sealed class EffectLabSettings
    {
        public StatusEffectDefinition Effect;
        [Min(1), Tooltip("How many times the effect is applied")] public int Applications = 1;
        [Min(0f), Tooltip("Seconds between applications")] public float Interval = 1f;
        [Min(1)] public int Stacks = 1;
        [Min(0f)] public float Power = 1f;
        [Min(0f), Tooltip("Seconds to simulate. 0 = until the effect ends, up to 60s")] public float Duration;
        [Range(10, 240), Tooltip("Simulation steps per second")] public int TickRate = 60;
        [Tooltip("Damage on the host goes through its damage pipeline")] public bool UsePipeline = true;
        [Tooltip("Leave empty to use the host's own pipeline")] public DamagePipeline Pipeline;
        [Min(0f), Tooltip("The source hits the host this often. 0 = never. Good for testing On Damage Taken triggers")] public float IncomingEvery;
        [Min(0f)] public float IncomingDamage = 10f;
        [Min(0f), Tooltip("The host hits the source this often. 0 = never. Good for testing On Damage Dealt triggers")] public float OutgoingEvery;
        [Min(0f)] public float OutgoingDamage = 10f;
        public int Seed = 1;
        public SandboxSetup Host = new();
        public SandboxSetup Source = new();
    }

    internal enum EffectEventKind { Applied, Stacks, Removed, Damage, Heal, Hit, Died }

    internal sealed class EffectEvent
    {
        public float Time;
        public EffectEventKind Kind;
        public string Text;
        public float Amount;
        public bool OnSource;
        public bool Periodic;
    }

    internal sealed class StatTrack
    {
        public StatType Stat;
        public float Base;
        public float Min = float.MaxValue;
        public float Max = float.MinValue;
        public float End;

        public bool Changed => Mathf.Abs(Max - Min) > 0.0001f || Mathf.Abs(Base - End) > 0.0001f;
    }

    internal sealed class EffectReport
    {
        public float Duration;
        public float HealthMax;
        public float DamageTaken;
        public float Healed;
        public float DamageToSource;
        public float HealedSource;
        public int Ticks;
        public int MaxStacks;
        public float EndedAt = -1f;
        public string EndReason;
        public bool HostDied;
        public readonly List<EffectEvent> Events = new();
        public readonly List<Vector2> Stacks = new();
        public readonly List<Vector2> Health = new();
        public readonly List<StatTrack> Stats = new();
        public readonly List<string> Notes = new();
    }

    internal sealed class ManualTickSource : ITickSource
    {
        private readonly List<ITickable> tickables = new();

        public void Register(ITickable tickable)
        {
            if(tickable != null && !tickables.Contains(tickable)) tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable) => tickables.Remove(tickable);

        public void Tick(float deltaTime)
        {
            for(int i = 0; i < tickables.Count; i++)
            {
                tickables[i].Tick(deltaTime);
            }
        }
    }

    internal sealed class SandboxHealth : IHealth
    {
        private readonly SandboxUnit owner;
        private readonly IResource resource;
        private readonly DamagePipeline pipeline;

        public SandboxHealth(SandboxUnit owner, IResource resource, DamagePipeline pipeline)
        {
            this.owner = owner;
            this.resource = resource;
            this.pipeline = pipeline;
        }

        public IReadonlyResource Resource => resource;
        public bool IsDead { get; private set; }

        public event Action<DamageResult> OnDamaged;
        public event Action<HealResult> OnHealed;
        public event Action<DeathInfo> OnDied;
        public event Action OnRevived;

        public DamageResult ApplyDamage(in DamageRequest request)
        {
            if(IsDead || resource == null) return DamageResult.Ignored(in request);
            var calc = pipeline != null ? pipeline.Run(in request, owner) : new DamageCalculation(in request, owner);
            float dealt = resource.Remove(calc.Amount, ChangeReason.Damage, request.Context);
            bool killed = resource.IsDepleted;
            var result = new DamageResult(in request, calc.Result, dealt, calc.Absorbed, calc.Amount - dealt, calc.IsCrit, killed);
            var source = request.Context.SourceUnit;
            OnDamaged?.Invoke(result);
            GameEvents.Raise(owner, new DamageReceivedEvent(source, owner, in result));
            GameEvents.Raise(source, new DamageDealtEvent(source, owner, in result));
            if(killed)
            {
                IsDead = true;
                resource.RegenPaused = true;
                var info = new DeathInfo(owner, source?.Root(), in request.Context, result);
                GameEvents.Raise(owner, new DiedEvent(in info));
                OnDied?.Invoke(info);
            }
            return result;
        }

        public HealResult ApplyHeal(float amount, in EffectContext ctx)
        {
            if(IsDead || resource == null) return HealResult.Ignored();
            float healed = resource.Add(amount, ChangeReason.Heal, ctx);
            if(healed <= 0f) return HealResult.Ignored();
            var result = new HealResult(healed, amount - healed, false, in ctx);
            OnHealed?.Invoke(result);
            GameEvents.Raise(owner, new HealReceivedEvent(ctx.SourceUnit, owner, in result));
            return result;
        }

        public void Kill(in EffectContext ctx)
        {
            if(IsDead || resource == null) return;
            resource.Set(0f, ChangeReason.Damage, in ctx);
            IsDead = true;
            resource.RegenPaused = true;
            OnDied?.Invoke(new DeathInfo(owner, ctx.SourceUnit, in ctx, null));
        }

        public void Revive(float normalized = 1f)
        {
            if(!IsDead || resource == null) return;
            IsDead = false;
            resource.RegenPaused = false;
            resource.Set(resource.Max * Mathf.Clamp01(normalized), ChangeReason.Heal);
            OnRevived?.Invoke();
        }
    }

    internal sealed class EffectSimulator
    {
        private const float k_AutoLimit = 60f;
        private const float k_Tail = 0.5f;
        private const float k_VirtualHealth = 1000000f;

        private float clock;

        public EffectReport Run(EffectLabSettings settings)
        {
            var report = new EffectReport();
            var effect = settings.Effect;
            if(effect == null) return report;

            var tickField = typeof(CombatServices).GetField("tickSource", BindingFlags.NonPublic | BindingFlags.Static);
            var previousTicks = tickField?.GetValue(null);
            var previousRandom = CombatServices.Random;
            var ticks = new ManualTickSource();
            if(tickField != null) CombatServices.TickSource = ticks;
            else report.Notes.Add("Delay payloads can't run here, so anything delayed is skipped");
            CombatServices.Random = new TesterRandom(RollMode.Random, settings.Seed);

            var host = new SandboxUnit("Host", ulong.MaxValue - 10);
            var source = new SandboxUnit("Source", ulong.MaxValue - 11);
            StatusEffectsComponent hostEffects = null;
            StatusEffectsComponent sourceEffects = null;
            try
            {
                host.Load(settings.Host, "Host");
                source.Load(settings.Source, "Source");
                var pipeline = settings.UsePipeline ? settings.Pipeline != null ? settings.Pipeline : UnitLibrary.PipelineOf(settings.Host.Unit) : null;
                var hostHealth = Health(host, settings.Host, pipeline, out bool virtualPool);
                var sourceHealth = Health(source, settings.Source, null, out _);
                hostEffects = Effects(host);
                sourceEffects = Effects(source);
                Watch(report, hostEffects, hostHealth, sourceHealth, () => clock);

                var stats = TrackedStats(effect, host);
                foreach(var stat in stats)
                {
                    float value = host.Stats.GetValue(stat, stat.DefaultValue);
                    report.Stats.Add(new StatTrack { Stat = stat, Base = value, Min = value, Max = value, End = value });
                }

                float step = 1f / Mathf.Max(1, settings.TickRate);
                float limit = settings.Duration > 0f ? settings.Duration : k_AutoLimit;
                float lastApplication = (settings.Applications - 1) * settings.Interval;
                float nextApply = 0f;
                int applied = 0;
                float nextIncoming = settings.IncomingEvery;
                float nextOutgoing = settings.OutgoingEvery;
                float quietSince = -1f;
                clock = 0f;
                report.Health.Add(new Vector2(0f, hostHealth.Resource?.Current ?? 0f));
                report.HealthMax = hostHealth.Resource?.Max ?? 0f;
                int lastStacks = 0;

                while(clock <= limit + 0.00001f)
                {
                    while(applied < settings.Applications && clock + 0.00001f >= nextApply)
                    {
                        hostEffects.Apply(effect, source, settings.Stacks, settings.Power);
                        applied++;
                        nextApply += settings.Interval;
                    }
                    if(settings.IncomingEvery > 0f && clock + 0.00001f >= nextIncoming)
                    {
                        Hit(source, host, hostHealth, settings.IncomingDamage, report, clock, false);
                        nextIncoming += settings.IncomingEvery;
                    }
                    if(settings.OutgoingEvery > 0f && clock + 0.00001f >= nextOutgoing)
                    {
                        Hit(host, source, sourceHealth, settings.OutgoingDamage, report, clock, true);
                        nextOutgoing += settings.OutgoingEvery;
                    }

                    hostEffects.Tick(step);
                    sourceEffects.Tick(step);
                    ticks.Tick(step);
                    host.Resources.Tick(step, host.IsAlive);
                    source.Resources.Tick(step, source.IsAlive);
                    clock += step;

                    int stacks = hostEffects.GetStacks(effect);
                    if(stacks != lastStacks)
                    {
                        report.Stacks.Add(new Vector2(clock, stacks));
                        lastStacks = stacks;
                    }
                    report.MaxStacks = Mathf.Max(report.MaxStacks, stacks);
                    report.Health.Add(new Vector2(clock, hostHealth.Resource?.Current ?? 0f));
                    foreach(var track in report.Stats)
                    {
                        float value = host.Stats.GetValue(track.Stat, track.Stat.DefaultValue);
                        track.Min = Mathf.Min(track.Min, value);
                        track.Max = Mathf.Max(track.Max, value);
                        track.End = value;
                    }

                    if(settings.Duration > 0f || clock < lastApplication) continue;
                    bool quiet = !hostEffects.Has(effect);
                    if(!quiet)
                    {
                        quietSince = -1f;
                        continue;
                    }
                    if(quietSince < 0f) quietSince = clock;
                    if(clock - quietSince >= k_Tail) break;
                }

                report.Duration = clock;
                if(report.EndedAt < 0f && hostEffects.Has(effect)) report.Notes.Add(settings.Duration > 0f ? $"Still active after {settings.Duration:0.#}s" : $"Still active after {k_AutoLimit:0}s, so it may be permanent");
                if(effect.IsPermanent && settings.Duration <= 0f) report.Notes.Add("This effect is permanent, so it only ends when dispelled or the host dies");
                if(pipeline == null && settings.UsePipeline) report.Notes.Add("The host has no damage pipeline, so damage isn't reduced");
                if(virtualPool) report.Notes.Add("The host has no health pool, so damage is counted against a very large one");
            }
            finally
            {
                if(hostEffects != null) ((IUnitComponent)hostEffects).OnDespawned(DespawnReason.Unloaded);
                if(sourceEffects != null) ((IUnitComponent)sourceEffects).OnDespawned(DespawnReason.Unloaded);
                if(hostEffects != null) ((IUnitComponent)hostEffects).Unbind();
                if(sourceEffects != null) ((IUnitComponent)sourceEffects).Unbind();
                CombatScheduler.Clear();
                if(tickField != null) tickField.SetValue(null, previousTicks);
                CombatServices.Random = previousRandom;
                host.Dispose();
                source.Dispose();
            }
            return report;
        }

        private static SandboxHealth Health(SandboxUnit unit, SandboxSetup setup, DamagePipeline pipeline, out bool virtualPool)
        {
            virtualPool = false;
            var type = UnitLibrary.HealthOf(setup.Unit);
            IResource resource = null;
            if(type != null) unit.Resources.TryGet(type, out resource);
            if(resource == null)
            {
                Resource fallback = null;
                foreach(var each in unit.Resources.All)
                {
                    if(!(each is Resource found)) continue;
                    if(found.Type != null && DefinitionAssets.Key(StudioKinds.NameOf(found.Type)).Contains("health"))
                    {
                        resource = found;
                        break;
                    }
                    fallback ??= found;
                }
                resource ??= fallback;
            }
            if(resource == null)
            {
                var pool = new Resource(null, k_VirtualHealth);
                pool.ResetTo(k_VirtualHealth);
                resource = pool;
                virtualPool = true;
            }
            var health = new SandboxHealth(unit, resource, pipeline);
            unit.Add(health);
            return health;
        }

        private static StatusEffectsComponent Effects(SandboxUnit unit)
        {
            var component = unit.GameObject.AddComponent<StatusEffectsComponent>();
            unit.Add(component);
            ((IUnitComponent)component).Initialize(unit);
            ((IUnitComponent)component).Bind();
            return component;
        }

        private static void Watch(EffectReport report, StatusEffectsComponent effects, SandboxHealth host, SandboxHealth source, Func<float> now)
        {
            effects.OnApplied += (instance, previous) =>
            {
                string text = previous == 0 ? $"{StudioKinds.NameOf(instance.Definition)} applied ({instance.Stacks} stack{(instance.Stacks == 1 ? string.Empty : "s")})" : $"Reapplied: {previous} → {instance.Stacks} stacks";
                report.Events.Add(new EffectEvent { Time = now(), Kind = previous == 0 ? EffectEventKind.Applied : EffectEventKind.Stacks, Text = text, Amount = instance.Stacks });
            };
            effects.OnRemoved += (instance, reason) =>
            {
                report.Events.Add(new EffectEvent { Time = now(), Kind = EffectEventKind.Removed, Text = $"{StudioKinds.NameOf(instance.Definition)} removed: {reason}" });
                if(report.EndedAt >= 0f) return;
                report.EndedAt = now();
                report.EndReason = reason.ToString();
            };
            host.OnDamaged += result =>
            {
                bool periodic = result.Request.Has(DamageFlags.Periodic);
                if(periodic) report.Ticks++;
                report.DamageTaken += result.Dealt;
                string type = result.Request.Type != null ? StudioKinds.NameOf(result.Request.Type) + " " : string.Empty;
                string extra = result.Result != HitResult.Hit ? $" ({result.Result})" : result.IsCrit ? " (crit)" : result.Absorbed > 0f ? $" ({result.Absorbed:0.##} absorbed)" : string.Empty;
                report.Events.Add(new EffectEvent { Time = now(), Kind = EffectEventKind.Damage, Amount = result.Dealt, Periodic = periodic, Text = $"Host takes {result.Dealt:0.##} {type}damage{extra}" });
            };
            host.OnHealed += result =>
            {
                report.Healed += result.Healed;
                report.Events.Add(new EffectEvent { Time = now(), Kind = EffectEventKind.Heal, Amount = result.Healed, Text = $"Host heals {result.Healed:0.##}" });
            };
            host.OnDied += _ =>
            {
                report.HostDied = true;
                report.Events.Add(new EffectEvent { Time = now(), Kind = EffectEventKind.Died, Text = "Host died" });
            };
            source.OnDamaged += result =>
            {
                report.DamageToSource += result.Dealt;
                report.Events.Add(new EffectEvent { Time = now(), Kind = EffectEventKind.Damage, Amount = result.Dealt, OnSource = true, Text = $"Source takes {result.Dealt:0.##} damage" });
            };
            source.OnHealed += result =>
            {
                report.HealedSource += result.Healed;
                report.Events.Add(new EffectEvent { Time = now(), Kind = EffectEventKind.Heal, Amount = result.Healed, OnSource = true, Text = $"Source heals {result.Healed:0.##}" });
            };
        }

        private static void Hit(SandboxUnit from, SandboxUnit to, SandboxHealth health, float amount, EffectReport report, float time, bool outgoing)
        {
            var context = new EffectContext(from, null, to);
            report.Events.Add(new EffectEvent { Time = time, Kind = EffectEventKind.Hit, Text = outgoing ? $"Host hits the source for {amount:0.##}" : $"Source hits the host for {amount:0.##}", OnSource = outgoing });
            health.ApplyDamage(new DamageRequest(amount, null, in context, DamageFlags.None));
        }

        private static List<StatType> TrackedStats(StatusEffectDefinition effect, SandboxUnit host)
        {
            var stats = new HashSet<StatType>();
            if(host.Profile != null)
            {
                foreach(var entry in host.Profile.Stats ?? Array.Empty<StatEntry>())
                {
                    if(entry.Type != null) stats.Add(entry.Type);
                }
            }
            using(var serialized = new UnityEditor.SerializedObject(effect))
            {
                foreach(var property in CombatTools.Walk(serialized))
                {
                    if(property.propertyType == UnityEditor.SerializedPropertyType.ObjectReference && property.objectReferenceValue is StatType stat) stats.Add(stat);
                }
            }
            return stats.OrderBy(s => StudioKinds.NameOf(s)).ToList();
        }
    }
}
