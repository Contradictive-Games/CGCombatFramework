using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal sealed class StepTrace
    {
        public int Index;
        public IDamageStep Step;
        public bool Muted;
        public bool Skipped;
        public float Before;
        public float After;
        public float Absorbed;
        public bool Crit;
        public HitResult Result;
        public string Rolls;
        public string Error;

        public float Delta => After - Before;
    }

    internal sealed class SimulationRun
    {
        public readonly List<StepTrace> Steps = new();
        public float Requested;
        public float Final;
        public float Absorbed;
        public bool Crit;
        public HitResult Result;
        public ResourceType Health;
        public float HealthBefore = -1f;
        public float HealthMax;

        public bool HasHealth => HealthBefore >= 0f;
        public float HealthAfter => Mathf.Max(0f, HealthBefore - Final);
    }

    internal sealed class SampleStats
    {
        public int Count;
        public float Min;
        public float Max;
        public float Mean;
        public float Median;
        public float MeanAbsorbed;
        public float CritRate;
        public float[] Values = Array.Empty<float>();
        public readonly Dictionary<HitResult, int> Results = new();
    }

    internal sealed class OrderGroup
    {
        public float Mean;
        public bool Current;
        public readonly List<int[]> Orders = new();
    }

    internal sealed class PipelineSimulation : IDisposable
    {
        public const int k_MaxExploreSteps = 7;
        private const int k_ProgressEvery = 40;

        private readonly SandboxUnit attacker = new("Attacker", ulong.MaxValue - 1);
        private readonly SandboxUnit defender = new("Defender", ulong.MaxValue - 2);
        private readonly GameObject hitboxObject;
        private readonly Hitbox hitbox;
        private TesterSettings settings;
        private ResourceType health;

        public PipelineSimulation()
        {
            hitboxObject = new GameObject("Tester Hitbox") { hideFlags = HideFlags.HideAndDontSave };
            hitbox = hitboxObject.AddComponent<Hitbox>();
        }

        public SandboxUnit Attacker => attacker;
        public SandboxUnit Defender => defender;

        public void Load(TesterSettings settings)
        {
            this.settings = settings;
            attacker.Load(settings.Attacker, "Attacker");
            defender.Load(settings.Defender, "Defender");
            hitbox.DamageMultiplier = settings.HitboxMultiplier;
            hitbox.Zone = settings.HitZone;
            health = UnitLibrary.HealthOf(settings.Defender.Unit);
            if(health == null)
            {
                foreach(var each in defender.Resources.All)
                {
                    if(each is Resource resource && resource.Type != null)
                    {
                        health = resource.Type;
                        break;
                    }
                }
            }
        }

        public SimulationRun Trace(IReadOnlyList<IDamageStep> steps, IReadOnlyList<int> order, ICollection<int> muted, TesterRandom random)
        {
            var run = new SimulationRun();
            random.Recording = true;
            Execute(steps, order, muted, random, run);
            random.Recording = false;
            return run;
        }

        public SampleStats Sample(IReadOnlyList<IDamageStep> steps, IReadOnlyList<int> order, ICollection<int> muted, int count, int seed)
        {
            count = Mathf.Max(1, count);
            var random = new TesterRandom(RollMode.Random, seed);
            var stats = new SampleStats { Count = count, Values = new float[count] };
            float sum = 0f;
            float absorbed = 0f;
            int crits = 0;
            for(int i = 0; i < count; i++)
            {
                var calc = Execute(steps, order, muted, random, null);
                stats.Values[i] = calc.Amount;
                sum += calc.Amount;
                absorbed += calc.Absorbed;
                if(calc.IsCrit) crits++;
                stats.Results.TryGetValue(calc.Result, out int seen);
                stats.Results[calc.Result] = seen + 1;
            }
            Array.Sort(stats.Values);
            stats.Min = stats.Values[0];
            stats.Max = stats.Values[count - 1];
            stats.Median = stats.Values[count / 2];
            stats.Mean = sum / count;
            stats.MeanAbsorbed = absorbed / count;
            stats.CritRate = crits / (float)count;
            return stats;
        }

        public List<OrderGroup> Explore(IReadOnlyList<IDamageStep> steps, IReadOnlyList<int> order, ICollection<int> muted, RollMode mode, int samples, int seed)
        {
            var active = new List<int>();
            foreach(int index in order)
            {
                if(!muted.Contains(index) && index >= 0 && index < steps.Count && steps[index] != null) active.Add(index);
            }

            var groups = new Dictionary<int, OrderGroup>();
            var random = new TesterRandom(mode, seed);
            int runs = mode == RollMode.Random ? Mathf.Max(1, samples) : 1;
            int total = Factorial(active.Count);
            int done = 0;
            try
            {
                foreach(var permutation in Permutations(active.ToArray()))
                {
                    float sum = 0f;
                    random.Reseed(seed);
                    for(int i = 0; i < runs; i++)
                    {
                        sum += Execute(steps, permutation, null, random, null).Amount;
                    }
                    float mean = sum / runs;
                    int key = Mathf.RoundToInt(mean * 100f);
                    if(!groups.TryGetValue(key, out var group))
                    {
                        group = new OrderGroup { Mean = mean };
                        groups.Add(key, group);
                    }
                    group.Orders.Add(permutation);
                    if(Same(permutation, active)) group.Current = true;
                    done++;
                    if(done % k_ProgressEvery == 0 && EditorUtility.DisplayCancelableProgressBar("Exploring Orders", $"{done} of {total} orders", done / (float)total)) break;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var sorted = new List<OrderGroup>(groups.Values);
            sorted.Sort((a, b) => a.Mean.CompareTo(b.Mean));
            return sorted;
        }

        public static int Factorial(int count)
        {
            int result = 1;
            for(int i = 2; i <= count; i++)
            {
                result *= i;
            }
            return result;
        }

        public void Dispose()
        {
            attacker.Dispose();
            defender.Dispose();
            Object.DestroyImmediate(hitboxObject);
        }

        private DamageCalculation Execute(IReadOnlyList<IDamageStep> steps, IReadOnlyList<int> order, ICollection<int> muted, TesterRandom random, SimulationRun run)
        {
            attacker.Resources.Reset();
            defender.Resources.Reset();

            IUnit source = settings.SelfHit ? defender : attacker;
            var hit = new HitData(defender.Transform.position, Vector3.up, Vector3.forward, settings.UseHitbox ? hitbox : null);
            var context = new EffectContext(source, settings.Cause, defender, in hit, 1f, 1, 0, random);
            var request = new DamageRequest(settings.Amount, settings.DamageType, in context, settings.Flags);
            var calc = new DamageCalculation(in request, defender);

            if(run != null)
            {
                run.Requested = settings.Amount;
                run.Health = health;
                run.HealthBefore = defender.Resources.Current(health);
                run.HealthMax = defender.Resources.Max(health);
            }

            foreach(int index in order)
            {
                var step = index >= 0 && index < steps.Count ? steps[index] : null;
                bool silenced = muted != null && muted.Contains(index);
                StepTrace trace = null;
                if(run != null)
                {
                    trace = new StepTrace { Index = index, Step = step, Muted = silenced, Before = calc.Amount };
                    run.Steps.Add(trace);
                }

                if(silenced || step == null || calc.IsCancelled)
                {
                    if(trace == null) continue;
                    trace.Skipped = calc.IsCancelled && !silenced && step != null;
                    trace.After = calc.Amount;
                    trace.Result = calc.Result;
                    continue;
                }

                bool wasCrit = calc.IsCrit;
                float absorbed = calc.Absorbed;
                try
                {
                    step.Process(ref calc);
                }
                catch(Exception exception)
                {
                    if(trace != null) trace.Error = exception.Message;
                }

                if(trace == null) continue;
                trace.After = calc.Amount;
                trace.Absorbed = calc.Absorbed - absorbed;
                trace.Crit = calc.IsCrit && !wasCrit;
                trace.Result = calc.Result;
                trace.Rolls = random.TakeRolls();
            }

            calc.Amount = Mathf.Max(0f, calc.Amount);
            if(run != null)
            {
                run.Final = calc.Amount;
                run.Absorbed = calc.Absorbed;
                run.Crit = calc.IsCrit;
                run.Result = calc.Result;
            }
            return calc;
        }

        private static bool Same(int[] a, List<int> b)
        {
            if(a.Length != b.Count) return false;
            for(int i = 0; i < a.Length; i++)
            {
                if(a[i] != b[i]) return false;
            }
            return true;
        }

        private static IEnumerable<int[]> Permutations(int[] items)
        {
            var current = (int[])items.Clone();
            var counters = new int[current.Length];
            yield return (int[])current.Clone();
            int i = 0;
            while(i < current.Length)
            {
                if(counters[i] < i)
                {
                    int swap = i % 2 == 0 ? 0 : counters[i];
                    (current[swap], current[i]) = (current[i], current[swap]);
                    yield return (int[])current.Clone();
                    counters[i]++;
                    i = 0;
                }
                else
                {
                    counters[i] = 0;
                    i++;
                }
            }
        }
    }
}
