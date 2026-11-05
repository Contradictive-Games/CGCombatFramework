using System;
using System.Collections.Generic;

namespace CG.CombatFramework.Editor
{
    internal static class BuiltInStyles
    {
        private static readonly Dictionary<Type, string> styles = new()
        {
            { typeof(StatsComponent), "cg-kind-stat" },
            { typeof(ResourceComponent), "cg-kind-resource" },
            { typeof(HealthComponent), "cg-kind-damage" },
            { typeof(DespawnOnDeath), "cg-kind-damage" },
            { typeof(StatusEffectsComponent), "cg-kind-effect" },
            { typeof(FirableLoadout), "cg-kind-firable" },
            { typeof(UnitViewBinder), "cg-kind-view" },
            { typeof(RigidbodyKnockback), "cg-kind-modifier" },
            { typeof(TransformAimProvider), "cg-kind-aim" },
            { typeof(GroundPlaneAimProvider), "cg-kind-aim" },
            { typeof(Unit), "cg-kind-unit" },
            { typeof(FirableDefinition), "cg-kind-firable" },
            { typeof(StatusEffectDefinition), "cg-kind-effect" },
            { typeof(DamagePipeline), "cg-kind-pipeline" },
            { typeof(UnitStatProfile), "cg-kind-profile" },
            { typeof(StatType), "cg-kind-stat" },
            { typeof(ResourceType), "cg-kind-resource" },
            { typeof(DamageType), "cg-kind-damage" },
            { typeof(StatModifierDefinition), "cg-kind-modifier" },
            { typeof(CombatTag), "cg-kind-tag" },
            { typeof(Faction), "cg-kind-faction" },
            { typeof(FirableSlot), "cg-kind-slot" }
        };

        public static IReadOnlyDictionary<Type, string> All => styles;

        public static string Of(Type type) => type != null && styles.TryGetValue(type, out var style) ? style : null;
    }
}
