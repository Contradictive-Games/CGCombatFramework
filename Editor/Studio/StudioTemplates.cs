using System;
using System.Reflection;
using UnityEngine;

namespace CG.CombatFramework.Editor
{
    internal static class StudioTemplates
    {
        private const BindingFlags k_Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static readonly StudioTemplate[] Firables =
        {
            new StudioTemplate("Pistol", asset => Firable(asset, new SemiAutoFire(),
                Gates(Cooldown(0.25f), Ammo(12, 1.2f)),
                Emit(Hostile(Raycast(40f, 0f), Damage(12f))))),

            new StudioTemplate("Assault Rifle", asset => Firable(asset, new FullAutoFire(),
                Gates(Cooldown(0.1f), Ammo(30, 2f)),
                Emit(Hostile(Raycast(60f, 2f), Damage(8f))))),

            new StudioTemplate("Burst Rifle", asset => Firable(asset, Make<BurstFire>(("shots", 3), ("interval", 0.08f)),
                Gates(Cooldown(0.45f), Ammo(24, 2f)),
                Emit(Hostile(Raycast(50f, 1f), Damage(9f))))),

            new StudioTemplate("Shotgun", asset => Firable(asset, new SemiAutoFire(),
                Gates(Cooldown(0.8f), Ammo(6, 2.2f)),
                Emit(Make<RepeatEmission>(("count", 8), ("emissions", Emit(Hostile(Raycast(18f, 8f), Damage(6f, DamageFlags.None, new DistanceFalloffScaler())))))))),

            new StudioTemplate("Grenade Launcher", asset => Firable(asset, new SemiAutoFire(),
                Gates(Cooldown(1f), Ammo(4, 2.5f)),
                Emit(Make<ProjectileEmission>(
                    ("speed", 16f),
                    ("lifetime", 4f),
                    ("gravity", 9.81f),
                    ("arcToAimPoint", true),
                    ("onStop", Emit(Hostile(Make<SphereQuery>(("radius", 4f)), Damage(35f, DamageFlags.None, Falloff(1f, 4f))))))))),

            new StudioTemplate("Melee Swing", asset => Firable(asset, new SemiAutoFire(),
                Gates(Cooldown(0.6f)),
                Emit(Hostile(Make<ConeQuery>(("angle", 100f), ("range", 2.5f)), Damage(20f), Make<KnockbackPayload>(("force", new ScalableValue(6f))))))),

            new StudioTemplate("Heal", asset => Firable(asset, new SemiAutoFire(),
                Gates(Cooldown(4f), Make<ResourceCostGate>(("cost", new ScalableValue(25f)))),
                Emit(Deliver(Make<UnitQuery>(("subject", Subject.Source)), Make<HealPayload>(("amount", new ScalableValue(30f))))))),

            new StudioTemplate("Channel Beam", asset => Firable(asset, Make<ChannelFire>(("pulseInterval", 0.2f), ("maxDuration", 3f)),
                Gates(Cooldown(5f)),
                Emit(Hostile(Raycast(20f, 0f), Damage(4f)))))
        };

        public static readonly StudioTemplate[] Effects =
        {
            new StudioTemplate("Damage Over Time", asset => Effect(asset, 5f, 1f, EffectStackingMode.Refresh, 1,
                When(PayloadEvent.Tick, Damage(4f, DamageFlags.Periodic)))),

            new StudioTemplate("Stacking Poison", asset => Effect(asset, 6f, 1f, EffectStackingMode.Independent, 5,
                When(PayloadEvent.Tick, Damage(2f, DamageFlags.Periodic, new StacksScaler())))),

            new StudioTemplate("Regeneration", asset => Effect(asset, 10f, 1f, EffectStackingMode.Refresh, 1,
                When(PayloadEvent.Tick, Make<HealPayload>(("amount", new ScalableValue(5f)))))),

            new StudioTemplate("Stat Buff", asset => Effect(asset, 8f, 0f, EffectStackingMode.Refresh, 1,
                new StatModifiersBehavior())),

            new StudioTemplate("Delayed Explosion", asset => Effect(asset, 3f, 0f, EffectStackingMode.Refresh, 1,
                When(PayloadEvent.Expired, Make<EmitAtPayload>(
                    ("emitFrom", EmitFrom.Target),
                    ("emissions", Emit(Hostile(Make<SphereQuery>(("radius", 4f)), Damage(30f)))))))),

            new StudioTemplate("Thorns", asset => Effect(asset, 0f, 0f, EffectStackingMode.Refresh, 1,
                Make<OnDamageTakenTrigger>(("payloads", Then(Damage(5f)))))),

            new StudioTemplate("Lifesteal", asset => Effect(asset, 0f, 0f, EffectStackingMode.Refresh, 1,
                Make<OnDamageDealtTrigger>(("payloads", Then(Make<HealPayload>(
                    ("subject", Subject.Source),
                    ("amount", new ScalableValue(0.2f) { Scalers = new IValueScaler[] { new TriggeredAmountScaler() } }))))))),
        };

        public static readonly StudioTemplate[] Pipelines =
        {
            new StudioTemplate("Standard", asset => Pipeline(asset,
                new FriendlyFireStep(),
                new ImmuneWithTagStep(),
                new EvadeStep(),
                new CritStrikeStep(),
                new HitboxMultiplierStep(),
                new ArmorStep(),
                new DamageResistanceStep(),
                new FlatReductionStep(),
                new ShieldAbsorbStep(),
                new MinimumDamageStep())),

            new StudioTemplate("Arcade", asset => Pipeline(asset,
                new FriendlyFireStep(),
                new HitboxMultiplierStep(),
                new MinimumDamageStep()))
        };

        private static void Firable(ScriptableObject asset, IFireMode mode, IFireGate[] gates, IEmission[] emissions)
        {
            var firable = (FirableDefinition)asset;
            firable.Mode = mode;
            firable.Gates = gates;
            firable.Emissions = emissions;
        }

        private static void Effect(ScriptableObject asset, float duration, float tickInterval, EffectStackingMode stacking, int maxStacks, params IStatusEffectBehavior[] behaviors)
        {
            var effect = (StatusEffectDefinition)asset;
            effect.Duration = duration;
            effect.TickInterval = tickInterval;
            effect.Stacking = stacking;
            effect.MaxStacks = maxStacks;
            effect.Behaviors = behaviors;
        }

        private static void Pipeline(ScriptableObject asset, params IDamageStep[] steps) => ((DamagePipeline)asset).Steps = steps;

        private static IFireGate[] Gates(params IFireGate[] gates) => gates;
        private static IEmission[] Emit(params IEmission[] emissions) => emissions;
        private static IPayload[] Then(params IPayload[] payloads) => payloads;

        private static CooldownGate Cooldown(float seconds) => Make<CooldownGate>(("seconds", seconds));
        private static AmmoGate Ammo(int magazine, float reload) => Make<AmmoGate>(("magazineSize", magazine), ("reloadSeconds", reload));
        private static RaycastQuery Raycast(float range, float spread) => Make<RaycastQuery>(("range", range), ("spreadDegrees", spread));
        private static PayloadsBehavior When(PayloadEvent when, params IPayload[] payloads) => Make<PayloadsBehavior>(("when", when), ("payloads", payloads));

        private static DistanceFalloffScaler Falloff(float start, float end)
        {
            return Make<DistanceFalloffScaler>(("measureFrom", FallOffFrom.QueryOrigin), ("start", start), ("end", end));
        }

        private static DamagePayload Damage(float amount, DamageFlags flags = DamageFlags.None, params IValueScaler[] scalers)
        {
            return Make<DamagePayload>(("amount", new ScalableValue(amount) { Scalers = scalers }), ("flags", flags));
        }

        private static Delivery Hostile(IHitQuery query, params IPayload[] payloads)
        {
            return Make<Delivery>(("query", query), ("filters", new ICondition[] { new RelationCondition() }), ("payloads", payloads));
        }

        private static Delivery Deliver(IHitQuery query, params IPayload[] payloads)
        {
            return Make<Delivery>(("query", query), ("payloads", payloads));
        }

        internal static T Make<T>(params (string field, object value)[] values) where T : new()
        {
            var block = new T();
            foreach(var (field, value) in values)
            {
                Set(block, field, value);
            }
            return block;
        }

        internal static void Set(object target, string name, object value)
        {
            for(var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, k_Fields);
                if(field == null) continue;
                bool convert = value is IConvertible && !field.FieldType.IsEnum && !field.FieldType.IsInstanceOfType(value);
                field.SetValue(target, convert ? Convert.ChangeType(value, field.FieldType) : value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }
    }
}
