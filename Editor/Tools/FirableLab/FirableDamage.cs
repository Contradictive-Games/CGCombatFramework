using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal sealed class DamageHit
    {
        public string Where;
        public object Amount;
        public DamageType Type;
        public DamageFlags Flags;
        public float Multiplier = 1f;
        public bool Estimate;
        public bool Scaled;
        public string Note;
        public float Raw;
        public float Mitigated;
    }

    internal sealed class EffectHit
    {
        public string Where;
        public StatusEffectDefinition Effect;
        public int Stacks;
        public float Multiplier = 1f;
        public bool Estimate;
    }

    internal static class FirableDamage
    {
        private const BindingFlags k_Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Collect(FirableDefinition firable, List<DamageHit> hits, List<EffectHit> effects, List<string> notes)
        {
            if(firable == null) return;
            var emissions = firable.Emissions ?? Array.Empty<IEmission>();
            for(int i = 0; i < emissions.Length; i++)
            {
                Walk(emissions[i], 1f, $"Emissions {i + 1}", false, null, hits, effects, notes, 0);
            }
        }

        public static float Reach(FirableDefinition firable)
        {
            float reach = 0f;
            foreach(var node in Nodes(firable?.Emissions, 0))
            {
                switch(node)
                {
                    case RaycastQuery _: reach = Mathf.Max(reach, Read<float>(node, "range")); break;
                    case SphereQuery _: reach = Mathf.Max(reach, Read<float>(node, "radius")); break;
                    case ConeQuery _: reach = Mathf.Max(reach, Read<float>(node, "range")); break;
                    case BoxQuery _: reach = Mathf.Max(reach, Read<float>(node, "length")); break;
                    case ProjectileEmission _: reach = Mathf.Max(reach, Read<float>(node, "speed") * Read<float>(node, "lifetime")); break;
                }
            }
            return reach;
        }

        public static float EffectDamage(StatusEffectDefinition effect, out int ticks)
        {
            ticks = 0;
            if(effect == null) return 0f;
            if(effect.TickInterval > 0f && !effect.IsPermanent) ticks = Mathf.FloorToInt(effect.Duration / effect.TickInterval + 0.0001f);
            float total = 0f;
            foreach(var behavior in effect.Behaviors ?? Array.Empty<IStatusEffectBehavior>())
            {
                if(!(behavior is PayloadsBehavior payloads)) continue;
                var when = Read<PayloadEvent>(payloads, "when");
                float perRun = 0f;
                foreach(var node in Nodes(Read<IPayload[]>(payloads, "payloads"), 0))
                {
                    if(node is DamagePayload damage && Read<object>(damage, "amount") is ScalableValue value) perRun += value.Base;
                }
                if((when & PayloadEvent.Applied) != 0) total += perRun;
                if((when & PayloadEvent.Tick) != 0) total += perRun * ticks;
                if((when & PayloadEvent.Expired) != 0 && !effect.IsPermanent) total += perRun;
            }
            return total;
        }

        public static T Read<T>(object target, string name)
        {
            for(var type = target?.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, k_Fields | BindingFlags.DeclaredOnly);
                if(field != null && field.GetValue(target) is T value) return value;
            }
            return default;
        }

        private static void Walk(object node, float multiplier, string where, bool estimate, string note, List<DamageHit> hits, List<EffectHit> effects, List<string> notes, int depth)
        {
            if(node == null || depth > 32) return;
            string here = $"{where} › {BlockTypes.DisplayName(node.GetType())}";
            switch(node)
            {
                case DamagePayload damage:
                    var amount = Read<object>(damage, "amount");
                    hits.Add(new DamageHit
                    {
                        Where = here,
                        Amount = amount,
                        Type = Read<DamageType>(damage, "type"),
                        Flags = Read<DamageFlags>(damage, "flags"),
                        Multiplier = multiplier,
                        Estimate = estimate,
                        Scaled = amount is ScalableValue scalable && (scalable.Scalers ?? Array.Empty<IValueScaler>()).Any(s => s != null),
                        Note = note
                    });
                    return;
                case ApplyStatusEffectPayload apply:
                    var effect = Read<StatusEffectDefinition>(apply, "effect");
                    if(effect != null) effects.Add(new EffectHit { Where = here, Effect = effect, Stacks = Math.Max(1, Read<int>(apply, "stacks")), Multiplier = multiplier, Estimate = estimate });
                    return;
                case RepeatEmission _:
                    multiplier *= Math.Max(1, Read<int>(node, "count"));
                    break;
                case RadialEmission _:
                    multiplier *= Math.Max(1, Read<int>(node, "count"));
                    note = Join(note, "if every direction hits");
                    estimate = true;
                    break;
                case WeightedRandomEmission _:
                    var options = Read<WeightedEmission[]>(node, "options") ?? Array.Empty<WeightedEmission>();
                    float total = options.Where(o => o != null).Sum(o => Mathf.Max(0f, o.Weight));
                    for(int i = 0; i < options.Length; i++)
                    {
                        if(options[i] == null || total <= 0f) continue;
                        float share = Mathf.Max(0f, options[i].Weight) / total;
                        var list = options[i].Emissions ?? Array.Empty<IEmission>();
                        for(int j = 0; j < list.Length; j++)
                        {
                            Walk(list[j], multiplier * share, $"{here} › Option {i + 1}", true, Join(note, $"{share:P0} of shots"), hits, effects, notes, depth + 1);
                        }
                    }
                    return;
                case ConditionalPayload _:
                    var then = Read<IPayload[]>(node, "then") ?? Array.Empty<IPayload>();
                    for(int i = 0; i < then.Length; i++)
                    {
                        Walk(then[i], multiplier, $"{here} › Then {i + 1}", true, Join(note, "only when its conditions pass"), hits, effects, notes, depth + 1);
                    }
                    if((Read<IPayload[]>(node, "otherwise") ?? Array.Empty<IPayload>()).Length > 0) notes.Add($"{here}: the Else branch isn't counted");
                    return;
                case ConsumeStacksPayload _:
                    note = Join(note, "only with enough stacks");
                    estimate = true;
                    break;
                case DelayPayload _:
                case DelayedEmission _:
                    note = Join(note, "delayed");
                    break;
                case ProjectileEmission _:
                    if(Read<UnityEngine.Object>(node, "prefab") == null) notes.Add($"{here}: no projectile prefab, so it fires nothing");
                    break;
            }

            foreach(var (child, label) in Children(node))
            {
                Walk(child, multiplier, $"{here} › {label}", estimate, note, hits, effects, notes, depth + 1);
            }
        }

        internal static IEnumerable<(object child, string label)> Children(object node)
        {
            for(var type = node.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach(var field in type.GetFields(k_Fields | BindingFlags.DeclaredOnly))
                {
                    if(field.IsNotSerialized) continue;
                    var value = field.GetValue(node);
                    string label = UnityEditor.ObjectNames.NicifyVariableName(field.Name);
                    if(value is IEmission || value is IPayload)
                    {
                        yield return (value, label);
                        continue;
                    }
                    if(!(value is IList list) || value is string) continue;
                    for(int i = 0; i < list.Count; i++)
                    {
                        if(list[i] is IEmission || list[i] is IPayload) yield return (list[i], $"{label} {i + 1}");
                    }
                }
            }
        }

        private static IEnumerable<object> Nodes(IEnumerable roots, int depth)
        {
            if(roots == null || depth > 32) yield break;
            foreach(var root in roots)
            {
                if(root == null) continue;
                yield return root;
                for(var type = root.GetType(); type != null && type != typeof(object); type = type.BaseType)
                {
                    foreach(var field in type.GetFields(k_Fields | BindingFlags.DeclaredOnly))
                    {
                        if(field.IsNotSerialized) continue;
                        var value = field.GetValue(root);
                        if(value == null || value is string || value is UnityEngine.Object) continue;
                        if(value is IList list)
                        {
                            foreach(var each in Nodes(list, depth + 1))
                            {
                                yield return each;
                            }
                        }
                        else if(value is IEmission || value is IPayload || value is IHitQuery || value is WeightedEmission)
                        {
                            foreach(var each in Nodes(new[] { value }, depth + 1))
                            {
                                yield return each;
                            }
                        }
                    }
                }
            }
        }

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : $"{a}, {b}";
    }
}
