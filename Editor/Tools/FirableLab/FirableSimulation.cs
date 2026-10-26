using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal enum FireInput { HoldTrigger, ClickRate }

    [Serializable]
    internal sealed class FireLabSettings
    {
        public FirableDefinition Firable;
        [Min(0.5f), Tooltip("Seconds to simulate")] public float Duration = 10f;
        [Range(20, 240), Tooltip("Simulation steps per second")] public int TickRate = 60;
        [Tooltip("Hold Trigger fires as fast as the gates allow. Click Rate presses at a human speed")]
        public FireInput Input = FireInput.HoldTrigger;
        [Min(0.5f)] public float ClicksPerSecond = 8f;
        [Min(0f), Tooltip("Distance to the target, for falloff scalers")] public float Distance = 10f;
        [Tooltip("Every hit lands on a hitbox")] public bool UseHitbox;
        [Min(0f)] public float HitboxMultiplier = 2f;
        public CombatTag HitZone;
        [Range(1, 2000), Tooltip("Random rolls per hit when working out average damage")] public int Samples = 200;
        public int Seed = 1;
        [Tooltip("Leave empty to use the defender's own pipeline")] public DamagePipeline Pipeline;
        public SandboxSetup Attacker = new();
        public SandboxSetup Defender = new();
    }

    internal readonly struct Shot
    {
        public readonly float Time;
        public readonly float Power;

        public Shot(float time, float power)
        {
            Time = time;
            Power = power;
        }
    }

    internal sealed class FireSpan
    {
        public string Kind;
        public float Start;
        public float End;
    }

    internal sealed class FireReport
    {
        public FirableDefinition Firable;
        public float Duration;
        public string Mode;
        public readonly List<Shot> Shots = new();
        public readonly List<FireSpan> Spans = new();
        public readonly Dictionary<FireBlockReason, float> Waiting = new();
        public readonly List<DamageHit> Hits = new();
        public readonly List<EffectHit> Effects = new();
        public readonly List<string> Notes = new();
        public readonly List<Vector2> Curve = new();
        public float RawPerShot;
        public float MitigatedPerShot;
        public float Total;
        public float Burst;
        public float BurstWindow;
        public float Health = -1f;
        public float TimeToKill = -1f;
        public bool TimeToKillEstimated;
        public int ShotsToKill = -1;
        public int Magazine;
        public float Reload;
        public float Reach;

        public float ShotsPerSecond => Duration > 0f ? Shots.Count / Duration : 0f;
        public float Sustained => Duration > 0f ? Total / Duration : 0f;
    }

    internal sealed class SimulatedFirable : IFireControl
    {
        private readonly IFireModeState mode;
        private readonly IGateState[] gates;

        public SimulatedFirable(FirableDefinition definition, IUnit owner)
        {
            Definition = definition;
            Owner = owner;
            mode = (definition.Mode ?? new SemiAutoFire()).CreateState();
            gates = (definition.Gates ?? Array.Empty<IFireGate>()).Where(g => g != null).Select(g => g.CreateState(this)).ToArray();
        }

        public FirableDefinition Definition { get; }
        public IUnit Owner { get; }
        public float Time { get; private set; }
        public FiringPhase Phase => mode.Phase;
        public float Progress01 => mode.Progress01;
        public IReadOnlyList<IGateState> Gates => gates;
        public readonly List<Shot> Shots = new();

        public void Press() => mode.Press(this);
        public void Release() => mode.Release(this);

        public void Step(float deltaTime)
        {
            Time += deltaTime;
            for(int i = 0; i < gates.Length; i++)
            {
                gates[i].Tick(deltaTime);
            }
            mode.Tick(this, deltaTime);
        }

        public bool Blocked(out FireBlockReason reason)
        {
            for(int i = 0; i < gates.Length; i++)
            {
                if(gates[i].IsReady(out reason)) continue;
                if(reason == FireBlockReason.None) reason = FireBlockReason.Condition;
                return true;
            }
            reason = FireBlockReason.None;
            return false;
        }

        public bool CheckReady(bool raiseBlocked) => !Blocked(out _);

        public bool TryFire(float power = 1f, bool silent = false)
        {
            if(!CheckReady(!silent)) return false;
            CommitGates();
            EmitWithoutGates(power);
            return true;
        }

        public void CommitGates()
        {
            for(int i = 0; i < gates.Length; i++)
            {
                gates[i].Commit();
            }
        }

        public void EmitWithoutGates(float power = 1f) => Shots.Add(new Shot(Time, power));
    }

    internal sealed class FirableSimulator : IDisposable
    {
        private const float k_PowerStep = 0.05f;

        private readonly PipelineSimulation pipeline = new();
        private readonly TesterSettings tester = new();
        private readonly Dictionary<(DamageHit hit, int power), (float raw, float mitigated)> cache = new();
        private FirableDefinition current;

        public void Dispose() => pipeline.Dispose();

        public FireReport Run(FireLabSettings settings, FirableDefinition firable)
        {
            var report = new FireReport { Firable = firable, Duration = settings.Duration };
            if(firable == null) return report;
            report.Mode = BlockTypes.DisplayName((firable.Mode ?? new SemiAutoFire()).GetType());
            current = firable;
            Load(settings, firable);
            cache.Clear();

            Fire(settings, firable, report);
            FirableDamage.Collect(firable, report.Hits, report.Effects, report.Notes);
            report.Reach = FirableDamage.Reach(firable);
            Damage(settings, report);
            Notes(settings, report);
            return report;
        }

        private void Load(FireLabSettings settings, FirableDefinition firable)
        {
            tester.Pipeline = settings.Pipeline;
            tester.Attacker = settings.Attacker;
            tester.Defender = settings.Defender;
            tester.Cause = firable;
            tester.UseHitbox = settings.UseHitbox;
            tester.HitboxMultiplier = settings.HitboxMultiplier;
            tester.HitZone = settings.HitZone;
            tester.Rolls = RollMode.Random;
            tester.Seed = settings.Seed;
            pipeline.Load(tester);
        }

        private void Fire(FireLabSettings settings, FirableDefinition firable, FireReport report)
        {
            var owner = pipeline.Attacker;
            owner.Resources.Reset();
            var control = new SimulatedFirable(firable, owner);
            var reloadable = control.Gates.OfType<IReloadable>().FirstOrDefault();
            if(reloadable != null) report.Magazine = reloadable.Capacity;

            float step = 1f / Mathf.Max(1, settings.TickRate);
            int steps = Mathf.CeilToInt(settings.Duration / step);
            float nextClick = 0f;
            bool held = false;
            bool clicks = settings.Input == FireInput.ClickRate && !(firable.Mode is ChargeFire) && !(firable.Mode is ChannelFire);
            FireSpan reloading = null;
            FireSpan active = null;

            for(int i = 0; i < steps; i++)
            {
                float now = i * step;
                if(clicks)
                {
                    if(now + 0.00001f >= nextClick)
                    {
                        control.Press();
                        control.Release();
                        nextClick += 1f / Mathf.Max(0.01f, settings.ClicksPerSecond);
                    }
                }
                else if(!held)
                {
                    control.Press();
                    held = true;
                }
                else if(control.Phase == FiringPhase.Idle)
                {
                    control.Release();
                    control.Press();
                }
                else if(control.Phase == FiringPhase.Charging && control.Progress01 >= 1f)
                {
                    control.Release();
                }

                control.Step(step);
                owner.Resources.Tick(step);

                bool busy = control.Phase == FiringPhase.Bursting || control.Phase == FiringPhase.Charging || control.Phase == FiringPhase.Channeling;
                if(!busy && control.Blocked(out var reason))
                {
                    report.Waiting.TryGetValue(reason, out float waited);
                    report.Waiting[reason] = waited + step;
                }

                bool isReloading = reloadable != null && reloadable.IsReloading;
                if(isReloading && reloading == null)
                {
                    reloading = new FireSpan { Kind = "Reload", Start = now };
                    report.Spans.Add(reloading);
                }
                if(reloading != null)
                {
                    reloading.End = now + step;
                    if(!isReloading) reloading = null;
                }

                string phase = control.Phase == FiringPhase.Charging ? "Charge" : control.Phase == FiringPhase.Channeling ? "Channel" : null;
                if(phase != null && (active == null || active.Kind != phase))
                {
                    active = new FireSpan { Kind = phase, Start = now };
                    report.Spans.Add(active);
                }
                if(active != null)
                {
                    active.End = now + step;
                    if(phase == null) active = null;
                }
            }

            report.Shots.AddRange(control.Shots);
            var firstReload = report.Spans.FirstOrDefault(s => s.Kind == "Reload");
            if(firstReload != null) report.Reload = firstReload.End - firstReload.Start;
            else if(firable.Gates != null)
            {
                var ammo = firable.Gates.OfType<AmmoGate>().FirstOrDefault();
                if(ammo != null) report.Reload = FirableDamage.Read<float>(ammo, "reloadSeconds") / owner.GetRate(FirableDamage.Read<StatType>(ammo, "reloadSpeedStat"));
            }
        }

        private void Damage(FireLabSettings settings, FireReport report)
        {
            var steps = tester.EffectivePipeline != null ? tester.EffectivePipeline.Steps ?? Array.Empty<IDamageStep>() : Array.Empty<IDamageStep>();
            var order = Enumerable.Range(0, steps.Length).ToList();
            var muted = new HashSet<int>();
            var health = pipeline.Trace(steps, order, muted, new TesterRandom(RollMode.Random, settings.Seed));
            if(health.HasHealth) report.Health = health.HealthBefore;

            float PerShot(float power, bool mitigated)
            {
                float total = 0f;
                foreach(var hit in report.Hits)
                {
                    var value = Evaluate(settings, hit, power, steps, order, muted);
                    total += hit.Multiplier * (mitigated ? value.mitigated : value.raw);
                }
                return total;
            }

            foreach(var hit in report.Hits)
            {
                var value = Evaluate(settings, hit, 1f, steps, order, muted);
                hit.Raw = value.raw;
                hit.Mitigated = value.mitigated;
            }
            report.RawPerShot = PerShot(1f, false);
            report.MitigatedPerShot = PerShot(1f, true);

            float cumulative = 0f;
            report.Curve.Add(Vector2.zero);
            for(int i = 0; i < report.Shots.Count; i++)
            {
                var shot = report.Shots[i];
                cumulative += Mathf.Approximately(shot.Power, 1f) ? report.MitigatedPerShot : PerShot(shot.Power, true);
                report.Curve.Add(new Vector2(shot.Time, cumulative));
                if(report.Health > 0f && report.TimeToKill < 0f && cumulative >= report.Health)
                {
                    report.TimeToKill = shot.Time;
                    report.ShotsToKill = i + 1;
                }
            }
            report.Total = cumulative;

            var firstReload = report.Spans.FirstOrDefault(s => s.Kind == "Reload");
            report.BurstWindow = firstReload != null ? firstReload.Start : report.Duration;
            float burst = report.Curve.Where(p => p.x <= report.BurstWindow + 0.0001f).Select(p => p.y).DefaultIfEmpty(0f).Max();
            report.Burst = report.BurstWindow > 0.0001f ? burst / report.BurstWindow : 0f;
            if(report.TimeToKill < 0f && report.Health > 0f && report.Sustained > 0f)
            {
                report.TimeToKill = report.Health / report.Sustained;
                report.TimeToKillEstimated = true;
            }
        }

        private (float raw, float mitigated) Evaluate(FireLabSettings settings, DamageHit hit, float power, IReadOnlyList<IDamageStep> steps, IReadOnlyList<int> order, ICollection<int> muted)
        {
            int key = Mathf.RoundToInt(power / k_PowerStep);
            if(cache.TryGetValue((hit, key), out var cached)) return cached;
            if(!(hit.Amount is ScalableValue amount)) return default;

            int samples = Mathf.Max(1, settings.Samples);
            var random = new TesterRandom(RollMode.Random, settings.Seed);
            var data = new HitData(pipeline.Defender.Transform.position, Vector3.up, Vector3.forward, null, settings.Distance);
            var context = new EffectContext(pipeline.Attacker, current, pipeline.Defender, in data, key * k_PowerStep, 1, 0, random);
            tester.DamageType = hit.Type;
            tester.Flags = hit.Flags;

            float raw = 0f;
            float mitigated = 0f;
            for(int i = 0; i < samples; i++)
            {
                float value = Mathf.Max(0f, amount.Evaluate(in context));
                raw += value;
                if(steps.Count == 0)
                {
                    mitigated += value;
                    continue;
                }
                tester.Amount = value;
                mitigated += pipeline.Sample(steps, order, muted, 1, settings.Seed + i).Mean;
            }
            var result = (raw / samples, mitigated / samples);
            cache[(hit, key)] = result;
            return result;
        }

        private void Notes(FireLabSettings settings, FireReport report)
        {
            if(report.Shots.Count == 0)
            {
                var main = report.Waiting.OrderByDescending(p => p.Value).Select(p => p.Key).FirstOrDefault();
                report.Notes.Insert(0, main != FireBlockReason.None ? $"Never fired in {settings.Duration:0.#}s. It was blocked by {Reason(main)}" : $"Never fired in {settings.Duration:0.#}s");
            }
            if(report.Hits.Count == 0) report.Notes.Add("No Deal Damage blocks in the emissions, so damage numbers are 0");
            if(report.Hits.Any(h => h.Scaled)) report.Notes.Add($"Some damage has scalers. Numbers use the attacker and defender on the left, {settings.Distance:0.#}m apart");
            if(report.Hits.Any(h => h.Estimate)) report.Notes.Add("Some hits are estimates: random options, conditions or every direction hitting");
            var steps = tester.EffectivePipeline?.Steps ?? Array.Empty<IDamageStep>();
            if(steps.Any(s => s is ShieldAbsorbStep)) report.Notes.Add("Shields are treated as full for every hit");
            if(tester.EffectivePipeline == null) report.Notes.Add("No damage pipeline, so damage isn't reduced");
            if(report.Effects.Count > 0) report.Notes.Add("Status effects are listed below and aren't counted in DPS");
        }

        public static string Reason(FireBlockReason reason)
        {
            switch(reason)
            {
                case FireBlockReason.NotEnoughResource: return "the resource cost";
                case FireBlockReason.NoAmmo: return "running out of ammo";
                case FireBlockReason.NoCharges: return "running out of charges";
                default: return UnityEditor.ObjectNames.NicifyVariableName(reason.ToString()).ToLowerInvariant();
            }
        }
    }
}
