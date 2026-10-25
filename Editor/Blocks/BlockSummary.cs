using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    public static class BlockSummary
    {
        private const string k_Separator = " · ";
        private const int k_FallbackParts = 3;

        private static readonly Regex token = new(@"\{(\w+)(\?)?(?::(%))?\}");

        private static readonly Dictionary<Type, string> templates = new()
        {
            { typeof(AllOfCondition), "{conditions} conditions" },
            { typeof(AnyOfCondition), "{conditions} conditions" },
            { typeof(ChanceCondition), "{chance:%}" },
            { typeof(DistanceCondition), "{minDistance}–{maxDistance}m" },
            { typeof(HasStatusEffectCondition), "{effect} · {minStacks}+ stacks · {subject?}" },
            { typeof(HasTagCondition), "{tag} · {subject?}" },
            { typeof(HitZoneCondition), "{zoneTag}" },
            { typeof(IsAliveCondition), "{subject?}" },
            { typeof(NotCondition), "{condition}" },
            { typeof(ProcDepthCondition), "below {below}" },
            { typeof(StatCompareCondition), "{stat} {comparison} {value} · {subject?}" },
            { typeof(ResourceThresholdCondition), "{resource} {comparison} {fraction:%} · {subject?}" },

            { typeof(BoxQuery), "{length}×{width}m" },
            { typeof(ChainQuery), "{jumps} jumps · {jumpRange}m" },
            { typeof(ConeQuery), "{angle}° · {range}m" },
            { typeof(EveryoneQuery), "{maxDistance?}m · {includeSource?}" },
            { typeof(RaycastQuery), "{range}m · {radius?}m radius · {spreadDegrees?}° spread · pierce {pierce?}" },
            { typeof(SphereQuery), "{radius}m · {center?}" },
            { typeof(UnitQuery), "{subject}" },

            { typeof(Delivery), "{query} · {filters?} filters · {payloads} payloads" },
            { typeof(DelayedEmission), "{delay}s" },
            { typeof(EmitFromEmission), "{from} · {direction?}" },
            { typeof(MultiEmission), "{emissions} emissions" },
            { typeof(PayloadsEmission), "{payloads} payloads" },
            { typeof(ProjectileEmission), "{prefab} · {speed}m/s · pierce {pierce?} · {bounces?} bounces" },
            { typeof(RadialEmission), "×{count} · {arc}°" },
            { typeof(RepeatEmission), "×{count}" },
            { typeof(WeightedRandomEmission), "{options} options" },

            { typeof(ApplyStatusEffectPayload), "{effect} · ×{stacks} · {subject?}" },
            { typeof(DamagePayload), "{amount} · {type} · {subject?}" },
            { typeof(DespawnPayload), "{reason} · {subject?}" },
            { typeof(DispelEffectsPayload), "{category} · max {max} · {subject?}" },
            { typeof(HealPayload), "{amount} · {subject?}" },
            { typeof(KnockbackPayload), "{force} · {direction?} · {subject?}" },
            { typeof(ModifyResourcePayload), "{amount} · {resource} · {drain?} · {subject?}" },
            { typeof(SpawnUnitPayload), "{prefab}" },
            { typeof(AddAmmoPayload), "+{amount} · {slot}" },
            { typeof(ReduceCooldownPayload), "{seconds}s · {finish?} · {slot}" },
            { typeof(ConditionalPayload), "{conditions} conditions · {then} then · {otherwise?} else" },
            { typeof(DelayPayload), "{delay}s · {payloads} payloads" },
            { typeof(EmitAtPayload), "{emitFrom} · {emissions} emissions" },
            { typeof(KillPayload), "{subject?}" },
            { typeof(RevivePayload), "{health:%} · {subject?}" },
            { typeof(TeleportPayload), "{to} · {subject?}" },
            { typeof(ConsumeStacksPayload), "{effect} · {minimumStacks}+ stacks · {payloads?} payloads" },
            { typeof(RemoveEffectPayload), "{effect} · {subject?}" },

            { typeof(ClampScaler), "{minimum}–{maximum}" },
            { typeof(DistanceFalloffScaler), "{start}–{end}m · min {minimum:%}" },
            { typeof(EffectStacksScaler), "{effect} · +{perStack:%} per stack" },
            { typeof(PercentOfResourceScaler), "{percent:%} · {resource} · {part}" },
            { typeof(PowerScaler), "×{weight}" },
            { typeof(RandomVarianceScaler), "±{variance:%}" },
            { typeof(StacksScaler), "+{perExtraStack:%} per extra stack" },
            { typeof(StatScaler), "{stat} · {mode}" },

            { typeof(BurstFire), "{shots} shots · {interval}s apart" },
            { typeof(ChannelFire), "{pulseInterval}s pulses · {maxDuration?}s max · {toggle?}" },
            { typeof(ChargeFire), "{maxChargeTime}s charge · min {minimumCharge:%}" },

            { typeof(AmmoGate), "{magazineSize} rounds · {reloadSeconds}s reload" },
            { typeof(ChargesGate), "{maxCharges} charges · {rechargeSeconds}s each" },
            { typeof(ConditionGate), "{conditions} conditions" },
            { typeof(CooldownGate), "{seconds}s" },
            { typeof(ResourceCostGate), "{cost} · {resource}" },

            { typeof(ArmorStep), "{armorStat} · halves at {halfAt}" },
            { typeof(CritStrikeStep), "{critChanceStat} · {critDamageStat}" },
            { typeof(DamageResistanceStep), "cap {cap:%}" },
            { typeof(EvadeStep), "{evadeChance} · cap {cap:%}" },
            { typeof(FlatReductionStep), "{reduction}" },
            { typeof(FriendlyFireStep), "self damage {allowSelfDamage}" },
            { typeof(HitboxMultiplierStep), "{weakPointBonus}" },
            { typeof(ImmuneWithTagStep), "{tag}" },
            { typeof(MinimumDamageStep), "{minimum} · {minimumShare?:%} of hit" },
            { typeof(ShieldAbsorbStep), "{shield} · {absorbShare:%}" },

            { typeof(DecayRegen), "-{perSecond}/s · floor {floorPercent?:%}" },
            { typeof(DelayAfterLoss), "{delay}s · {then}" },
            { typeof(FlatRegen), "{perSecond}/s" },
            { typeof(PercentOfMaxRegen), "{percentPerSecond}% max/s" },
            { typeof(RampUpRegen), "{startPerSecond}→{fullPerSecond}/s" },

            { typeof(PayloadsBehavior), "{when} · {payloads} payloads" },
            { typeof(StatModifiersBehavior), "{modifiers} modifiers" },
            { typeof(WhileBehavior), "{effect} · {conditions} conditions" },

            { typeof(OnDamageDealtTrigger), "{payloads} payloads · {critsOnly?} · {cooldown?}s cooldown" },
            { typeof(OnDamageTakenTrigger), "{when} · {payloads} payloads · {cooldown?}s cooldown" },
            { typeof(OnDiedTrigger), "{payloads} payloads" },
            { typeof(OnKillTrigger), "{payloads} payloads · {directOnly?} · {cooldown?}s cooldown" }
        };

        public static void Register(Type type, string template)
        {
            if(type != null) templates[type] = template;
        }

        public static string Of(SerializedProperty property)
        {
            var type = BlockTypes.ValueType(property);
            if(type == null) return string.Empty;
            return templates.TryGetValue(type, out var template) ? Fill(template, property) : Fallback(property);
        }

        private static string Fill(string template, SerializedProperty property)
        {
            var kept = new List<string>();
            foreach(var part in template.Split(new[] { k_Separator }, StringSplitOptions.None))
            {
                bool empty = false;
                string text = token.Replace(part, match =>
                {
                    string value = Format(property.FindPropertyRelative(match.Groups[1].Value), match.Groups[2].Success, match.Groups[3].Success);
                    if(string.IsNullOrEmpty(value)) empty = true;
                    return value;
                });
                if(!empty) kept.Add(text);
            }
            return string.Join(k_Separator, kept);
        }

        private static string Fallback(SerializedProperty property)
        {
            var parts = new List<string>();
            foreach(var child in BlockTypes.Children(property))
            {
                if(parts.Count >= k_FallbackParts) break;
                if(child.propertyType == SerializedPropertyType.Generic || child.propertyType == SerializedPropertyType.ManagedReference) continue;
                string value = Format(child, true, false);
                if(string.IsNullOrEmpty(value)) continue;
                bool number = child.propertyType == SerializedPropertyType.Float || child.propertyType == SerializedPropertyType.Integer;
                parts.Add(number ? $"{child.displayName} {value}" : value);
            }
            return string.Join(k_Separator, parts);
        }

        private static string Format(SerializedProperty property, bool optional, bool percent)
        {
            if(property == null) return null;
            switch(property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return Number(property.floatValue, optional, percent);
                case SerializedPropertyType.Integer:
                    return Number(property.intValue, optional, percent);
                case SerializedPropertyType.Boolean:
                    if(optional) return property.boolValue ? property.displayName : null;
                    return property.boolValue ? "on" : "off";
                case SerializedPropertyType.Enum:
                    return EnumText(property, optional);
                case SerializedPropertyType.ObjectReference:
                    return NameOf(property.objectReferenceValue);
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(property.stringValue) ? null : property.stringValue;
                case SerializedPropertyType.ManagedReference:
                    var type = BlockTypes.ValueType(property);
                    return type == null ? null : BlockTypes.DisplayName(type);
                case SerializedPropertyType.Generic:
                    if(property.isArray) return optional && property.arraySize == 0 ? null : property.arraySize.ToString(CultureInfo.InvariantCulture);
                    return Scalable(property, percent);
                default:
                    return null;
            }
        }

        private static string Scalable(SerializedProperty property, bool percent)
        {
            var baseValue = property.FindPropertyRelative(nameof(ScalableValue.Base));
            var scalers = property.FindPropertyRelative(nameof(ScalableValue.Scalers));
            if(baseValue == null || scalers == null) return null;
            string text = Number(baseValue.floatValue, false, percent);
            return scalers.arraySize > 0 ? "~" + text : text;
        }

        private static string Number(float value, bool optional, bool percent)
        {
            if(optional && Mathf.Approximately(value, 0f)) return null;
            return percent
                ? (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%"
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string EnumText(SerializedProperty property, bool optional)
        {
            int index = property.enumValueIndex;
            if(index >= 0)
            {
                if(optional && index == 0) return null;
                return property.enumDisplayNames[index];
            }

            var field = BlockTypes.FieldOf(property);
            if(field == null || !field.FieldType.IsEnum) return null;
            if(optional && property.enumValueFlag == 0) return null;
            var names = Enum.ToObject(field.FieldType, property.enumValueFlag).ToString().Split(new[] { ", " }, StringSplitOptions.None);
            for(int i = 0; i < names.Length; i++)
            {
                names[i] = ObjectNames.NicifyVariableName(names[i]);
            }
            return string.Join(" + ", names);
        }

        private static string NameOf(UnityEngine.Object value)
        {
            if(value == null) return null;
            return value is GameDefinition definition ? definition.DisplayName : value.name;
        }
    }
}
