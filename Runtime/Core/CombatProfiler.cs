using Unity.Profiling;

namespace CG.CombatFramework
{
    public static class CombatProfiler
    {
        public const string TickName = "CG.Combat.Tick";
        public const string FirablesName = "CG.Combat.Firables";
        public const string DeliveryName = "CG.Combat.Delivery";
        public const string DamageName = "CG.Combat.Damage";
        public const string StatusEffectsName = "CG.Combat.StatusEffects";
        public const string ProjectilesName = "CG.Combat.Projectiles";

        public static readonly ProfilerMarker Tick = new(TickName);
        public static readonly ProfilerMarker Firables = new(FirablesName);
        public static readonly ProfilerMarker Delivery = new(DeliveryName);
        public static readonly ProfilerMarker Damage = new(DamageName);
        public static readonly ProfilerMarker StatusEffects = new(StatusEffectsName);
        public static readonly ProfilerMarker Projectiles = new(ProjectilesName);
    }
}